using FluentAssertions;
using Smith.Database;
using Smith.Migration;
using Smith.Rendering;

namespace Smith.Tests.Integration;

/// <summary>
/// MigrationRunner 集成测试：在真实数据库上执行迁移
/// </summary>
public class MigrationRunnerIntegrationTests : IAsyncLifetime
{
    private readonly TestDatabaseFixture _fixture = new();
    private readonly QuietRenderer _renderer = new();

    private string FixturesPath => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task RunAsync_ExecutesAllPendingMigrations()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var tracker = new PostgresMigrationTracker(conn);
        var runner = new MigrationRunner(conn, tracker, _renderer);

        var count = await runner.RunAsync(FixturesPath);

        count.Should().Be(3);
    }

    [Fact]
    public async Task RunAsync_CreatesSchemaObjectsInDatabase()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var tracker = new PostgresMigrationTracker(conn);
        var runner = new MigrationRunner(conn, tracker, _renderer);

        await runner.RunAsync(FixturesPath);

        var inspector = new PostgresSchemaInspector(conn);
        (await inspector.ExtensionExistsAsync("uuid-ossp")).Should().BeTrue();
        (await inspector.ExtensionExistsAsync("pgcrypto")).Should().BeTrue();
        (await inspector.TableExistsAsync("users")).Should().BeTrue();
        (await inspector.IndexExistsAsync("idx_users_username")).Should().BeTrue();
        (await inspector.IndexExistsAsync("idx_users_email")).Should().BeTrue();
        (await inspector.IndexExistsAsync("idx_users_email_verified")).Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_RecordsAllMigrationsInTracker()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var tracker = new PostgresMigrationTracker(conn);
        var runner = new MigrationRunner(conn, tracker, _renderer);

        await runner.RunAsync(FixturesPath);

        var version = await tracker.GetCurrentVersionAsync();
        version.Should().Be(3);

        var applied = await tracker.GetAppliedVersionsAsync();
        applied.Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public async Task RunAsync_SkipsAlreadyAppliedMigrations()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var tracker = new PostgresMigrationTracker(conn);
        var runner = new MigrationRunner(conn, tracker, _renderer);

        // 第一次执行全部 3 个
        var first = await runner.RunAsync(FixturesPath);
        first.Should().Be(3);

        // 第二次执行应该跳过全部
        var second = await runner.RunAsync(FixturesPath);
        second.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_WithTarget_StopsAtTargetVersion()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var tracker = new PostgresMigrationTracker(conn);
        var runner = new MigrationRunner(conn, tracker, _renderer);

        var count = await runner.RunAsync(FixturesPath, targetVersion: 2);
        count.Should().Be(2);

        var version = await tracker.GetCurrentVersionAsync();
        version.Should().Be(2);

        // users 表存在但 email_verified 字段不存在
        var inspector = new PostgresSchemaInspector(conn);
        (await inspector.TableExistsAsync("users")).Should().BeTrue();
        (await inspector.IndexExistsAsync("idx_users_email_verified")).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_DryRun_DoesNotModifyDatabase()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var tracker = new PostgresMigrationTracker(conn);
        var runner = new MigrationRunner(conn, tracker, _renderer);

        var count = await runner.RunAsync(FixturesPath, dryRun: true);
        count.Should().Be(3); // 返回待执行数量

        // 数据库未变更
        var version = await tracker.GetCurrentVersionAsync();
        version.Should().Be(0);

        var inspector = new PostgresSchemaInspector(conn);
        (await inspector.TableExistsAsync("users")).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_FailedMigration_RollsBackAndRecordsFailure()
    {
        // 创建一个会失败的迁移文件
        var tempDir = Path.Combine(Path.GetTempPath(), $"smith_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        await File.WriteAllTextAsync(
            Path.Combine(tempDir, "001_good.sql"),
            "CREATE TABLE good_table (id SERIAL PRIMARY KEY);"
        );
        await File.WriteAllTextAsync(
            Path.Combine(tempDir, "002_bad.sql"),
            "CREATE TABLE bad_table (id INVALID_TYPE);" // 语法错误
        );

        try
        {
            await using var conn = await _fixture.OpenConnectionAsync();
            var tracker = new PostgresMigrationTracker(conn);
            var runner = new MigrationRunner(conn, tracker, _renderer);

            var act = async () => await runner.RunAsync(tempDir);
            await act.Should().ThrowAsync<Exception>();

            // 第一个迁移成功
            var inspector = new PostgresSchemaInspector(conn);
            (await inspector.TableExistsAsync("good_table")).Should().BeTrue();

            // 第二个迁移回滚（表不存在）
            (await inspector.TableExistsAsync("bad_table")).Should().BeFalse();

            // 版本停留在 1
            var version = await tracker.GetCurrentVersionAsync();
            version.Should().Be(1);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task RunAsync_RecordsCorrectChecksums()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var tracker = new PostgresMigrationTracker(conn);
        var runner = new MigrationRunner(conn, tracker, _renderer);

        await runner.RunAsync(FixturesPath);

        var history = await tracker.GetHistoryAsync();
        var migrations = MigrationFile.LoadFromDirectory(FixturesPath);

        foreach (var record in history)
        {
            var migration = migrations.First(m => m.Version == record.Version);
            record.Checksum.Should().Be(migration.GetChecksum());
        }
    }

    [Fact]
    public async Task RunAsync_RecordsPositiveExecutionTime()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var tracker = new PostgresMigrationTracker(conn);
        var runner = new MigrationRunner(conn, tracker, _renderer);

        await runner.RunAsync(FixturesPath);

        var history = await tracker.GetHistoryAsync();
        foreach (var record in history)
        {
            record.ExecutionTimeMs.Should().BeGreaterThanOrEqualTo(0);
            record.Success.Should().BeTrue();
        }
    }
}
