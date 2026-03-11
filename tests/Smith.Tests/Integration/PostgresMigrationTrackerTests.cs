using FluentAssertions;
using Smith.Migration;

namespace Smith.Tests.Integration;

/// <summary>
/// PostgresMigrationTracker 集成测试：验证 schema_migrations 表的 CRUD 操作
/// </summary>
public class PostgresMigrationTrackerTests : IAsyncLifetime
{
    private readonly TestDatabaseFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task EnsureTableExists_CreatesTable()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var tracker = new PostgresMigrationTracker(conn);

        await tracker.EnsureTableExistsAsync();

        // 验证表存在
        await using var cmd = new Npgsql.NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'schema_migrations')", conn);
        var exists = (bool)(await cmd.ExecuteScalarAsync())!;
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureTableExists_Idempotent()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var tracker = new PostgresMigrationTracker(conn);

        await tracker.EnsureTableExistsAsync();
        await tracker.EnsureTableExistsAsync(); // 第二次调用不应报错

        var version = await tracker.GetCurrentVersionAsync();
        version.Should().Be(0);
    }

    [Fact]
    public async Task GetCurrentVersion_EmptyTable_ReturnsZero()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var tracker = new PostgresMigrationTracker(conn);
        await tracker.EnsureTableExistsAsync();

        var version = await tracker.GetCurrentVersionAsync();
        version.Should().Be(0);
    }

    [Fact]
    public async Task RecordAndGetCurrentVersion_ReturnsLatestVersion()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var tracker = new PostgresMigrationTracker(conn);
        await tracker.EnsureTableExistsAsync();

        var fixturesPath = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var migrations = MigrationFile.LoadFromDirectory(fixturesPath);

        await tracker.RecordAsync(migrations[0], 10);
        await tracker.RecordAsync(migrations[1], 20);

        var version = await tracker.GetCurrentVersionAsync();
        version.Should().Be(2);
    }

    [Fact]
    public async Task GetAppliedVersions_ReturnsAllApplied()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var tracker = new PostgresMigrationTracker(conn);
        await tracker.EnsureTableExistsAsync();

        var fixturesPath = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var migrations = MigrationFile.LoadFromDirectory(fixturesPath);

        await tracker.RecordAsync(migrations[0], 10);
        await tracker.RecordAsync(migrations[2], 30);

        var versions = await tracker.GetAppliedVersionsAsync();
        versions.Should().BeEquivalentTo([1, 3]);
    }

    [Fact]
    public async Task Record_UpsertOnConflict_UpdatesExisting()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var tracker = new PostgresMigrationTracker(conn);
        await tracker.EnsureTableExistsAsync();

        var fixturesPath = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var migration = MigrationFile.LoadFromDirectory(fixturesPath)[0];

        // 记录两次，第二次应该覆盖
        await tracker.RecordAsync(migration, 10);
        await tracker.RecordAsync(migration, 99);

        var history = await tracker.GetHistoryAsync(10);
        history.Should().ContainSingle();
        history[0].ExecutionTimeMs.Should().Be(99);
    }

    [Fact]
    public async Task RecordFailure_RecordsFailedMigration()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var tracker = new PostgresMigrationTracker(conn);
        await tracker.EnsureTableExistsAsync();

        var fixturesPath = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var migration = MigrationFile.LoadFromDirectory(fixturesPath)[0];

        await tracker.RecordFailureAsync(migration, "syntax error");

        var history = await tracker.GetHistoryAsync();
        history.Should().ContainSingle();
        history[0].Success.Should().BeFalse();
        history[0].Description.Should().Contain("FAILED");
    }

    [Fact]
    public async Task RecordFailure_DoesNotOverwriteSuccess()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var tracker = new PostgresMigrationTracker(conn);
        await tracker.EnsureTableExistsAsync();

        var fixturesPath = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var migration = MigrationFile.LoadFromDirectory(fixturesPath)[0];

        // 先记录成功
        await tracker.RecordAsync(migration, 10);
        // 再记录失败（ON CONFLICT DO NOTHING）
        await tracker.RecordFailureAsync(migration, "some error");

        var history = await tracker.GetHistoryAsync();
        history.Should().ContainSingle();
        history[0].Success.Should().BeTrue(); // 成功记录不被覆盖
    }

    [Fact]
    public async Task GetHistory_ReturnsOrderedByVersionDesc()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var tracker = new PostgresMigrationTracker(conn);
        await tracker.EnsureTableExistsAsync();

        var fixturesPath = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var migrations = MigrationFile.LoadFromDirectory(fixturesPath);

        await tracker.RecordAsync(migrations[0], 10);
        await tracker.RecordAsync(migrations[1], 20);
        await tracker.RecordAsync(migrations[2], 30);

        var history = await tracker.GetHistoryAsync();
        history.Should().HaveCount(3);
        history[0].Version.Should().Be(3);
        history[1].Version.Should().Be(2);
        history[2].Version.Should().Be(1);
    }

    [Fact]
    public async Task GetHistory_RespectsLimit()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var tracker = new PostgresMigrationTracker(conn);
        await tracker.EnsureTableExistsAsync();

        var fixturesPath = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var migrations = MigrationFile.LoadFromDirectory(fixturesPath);

        foreach (var m in migrations)
            await tracker.RecordAsync(m, 10);

        var history = await tracker.GetHistoryAsync(limit: 2);
        history.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetHistory_IncludesChecksum()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var tracker = new PostgresMigrationTracker(conn);
        await tracker.EnsureTableExistsAsync();

        var fixturesPath = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var migration = MigrationFile.LoadFromDirectory(fixturesPath)[0];

        await tracker.RecordAsync(migration, 10);

        var history = await tracker.GetHistoryAsync();
        history[0].Checksum.Should().HaveLength(64);
        history[0].Checksum.Should().Be(migration.GetChecksum());
    }
}
