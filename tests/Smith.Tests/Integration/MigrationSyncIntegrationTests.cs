using FluentAssertions;
using Smith.Database;
using Smith.Migration;
using Smith.Rendering;

namespace Smith.Tests.Integration;

/// <summary>
/// MigrationSyncService 集成测试：验证在真实数据库上的同步逻辑
/// </summary>
public class MigrationSyncIntegrationTests : IAsyncLifetime
{
    private readonly TestDatabaseFixture _fixture = new();
    private readonly QuietRenderer _renderer = new();

    private string FixturesPath => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task Sync_DetectsAlreadyExecutedMigrations()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var tracker = new PostgresMigrationTracker(conn);
        var inspector = new PostgresSchemaInspector(conn);
        var syncService = new MigrationSyncService(tracker, inspector, _renderer);

        // 手动执行迁移 SQL 但不记录到 tracker
        await tracker.EnsureTableExistsAsync();
        var migrations = MigrationFile.LoadFromDirectory(FixturesPath);
        foreach (var m in migrations)
        {
            await using var cmd = new Npgsql.NpgsqlCommand(m.GetContent(), conn);
            await cmd.ExecuteNonQueryAsync();
        }

        // sync 应该检测到这些迁移已执行
        var result = await syncService.AnalyzeAsync(migrations);

        // Reason: fixture 001 创建扩展，002 创建表和索引 - 都应被检测为已同步
        // fixture 003 有 ALTER TABLE + CREATE INDEX，INDEX 应被检测到
        result.Synced.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Sync_ApplyRecordsMigrations()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var tracker = new PostgresMigrationTracker(conn);
        var inspector = new PostgresSchemaInspector(conn);
        var syncService = new MigrationSyncService(tracker, inspector, _renderer);

        // 手动执行迁移 SQL 但不记录
        await tracker.EnsureTableExistsAsync();
        var migrations = MigrationFile.LoadFromDirectory(FixturesPath);
        foreach (var m in migrations)
        {
            await using var cmd = new Npgsql.NpgsqlCommand(m.GetContent(), conn);
            await cmd.ExecuteNonQueryAsync();
        }

        // 分析并应用同步
        var result = await syncService.AnalyzeAsync(migrations);
        await syncService.ApplyAsync(result.Synced);

        // 验证记录已写入
        var applied = await tracker.GetAppliedVersionsAsync();
        foreach (var synced in result.Synced)
            applied.Should().Contain(synced.Migration.Version);
    }

    [Fact]
    public async Task Sync_IdentifiesMissingObjects()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var tracker = new PostgresMigrationTracker(conn);
        var inspector = new PostgresSchemaInspector(conn);
        var syncService = new MigrationSyncService(tracker, inspector, _renderer);

        await tracker.EnsureTableExistsAsync();

        // 不执行任何迁移，直接分析
        var migrations = MigrationFile.LoadFromDirectory(FixturesPath);
        var result = await syncService.AnalyzeAsync(migrations);

        // 所有迁移都不应被检测为已同步（数据库是空的）
        result.Synced.Should().BeEmpty();
        result.Skipped.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Sync_RecordsWithZeroExecutionTime()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var tracker = new PostgresMigrationTracker(conn);
        var inspector = new PostgresSchemaInspector(conn);
        var syncService = new MigrationSyncService(tracker, inspector, _renderer);

        // 手动执行
        await tracker.EnsureTableExistsAsync();
        var migration = MigrationFile.LoadFromDirectory(FixturesPath)[1]; // 002_create_users_table
        await using (var cmd = new Npgsql.NpgsqlCommand(migration.GetContent(), conn))
            await cmd.ExecuteNonQueryAsync();

        var result = await syncService.AnalyzeAsync([migration]);
        await syncService.ApplyAsync(result.Synced);

        var history = await tracker.GetHistoryAsync();
        var record = history.FirstOrDefault(h => h.Version == migration.Version);
        record.Should().NotBeNull();
        // Reason: 同步的迁移使用 0ms 执行时间标识为补录
        record!.ExecutionTimeMs.Should().Be(0);
    }
}
