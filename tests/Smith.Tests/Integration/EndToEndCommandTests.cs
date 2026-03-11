using FluentAssertions;
using Smith.Configuration;
using Smith.Database;
using Smith.Migration;
using Smith.Rendering;

namespace Smith.Tests.Integration;

/// <summary>
/// 端到端命令测试：模拟完整的命令执行流程
/// </summary>
public class EndToEndCommandTests : IAsyncLifetime
{
    private readonly TestDatabaseFixture _fixture = new();
    private readonly QuietRenderer _renderer = new();

    private string FixturesPath => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task FullWorkflow_MigrateUp_StatusShow_StatusSync()
    {
        // 1. 执行迁移
        await using var conn1 = await _fixture.OpenConnectionAsync();
        var tracker1 = new PostgresMigrationTracker(conn1);
        var runner = new MigrationRunner(conn1, tracker1, _renderer);

        var migrated = await runner.RunAsync(FixturesPath);
        migrated.Should().Be(3);

        // 2. 验证状态
        var version = await tracker1.GetCurrentVersionAsync();
        version.Should().Be(3);

        var applied = await tracker1.GetAppliedVersionsAsync();
        applied.Should().HaveCount(3);

        // 3. 再次执行应该无操作
        var noOp = await runner.RunAsync(FixturesPath);
        noOp.Should().Be(0);
    }

    [Fact]
    public async Task FullWorkflow_ManualExecution_ThenSync()
    {
        // 模拟场景：DBA 手动执行了 SQL，需要同步记录

        // 1. 只记录前两个迁移
        await using var conn = await _fixture.OpenConnectionAsync();
        var tracker = new PostgresMigrationTracker(conn);
        var runner = new MigrationRunner(conn, tracker, _renderer);

        await runner.RunAsync(FixturesPath, targetVersion: 2);

        // 2. 手动执行第三个迁移（不通过 runner）
        var migration3 = MigrationFile.LoadFromDirectory(FixturesPath).Last();
        await using (var cmd = new Npgsql.NpgsqlCommand(migration3.GetContent(), conn))
            await cmd.ExecuteNonQueryAsync();

        // 3. 验证版本仍然是 2
        var version = await tracker.GetCurrentVersionAsync();
        version.Should().Be(2);

        // 4. 执行 sync
        var inspector = new PostgresSchemaInspector(conn);
        var syncService = new MigrationSyncService(tracker, inspector, _renderer);

        var pendingMigrations = MigrationFile.LoadFromDirectory(FixturesPath)
            .Where(m => m.Version > 2)
            .ToList();

        var result = await syncService.AnalyzeAsync(pendingMigrations);
        // Reason: fixture 003 有 CREATE INDEX，应被检测到
        result.Synced.Should().HaveCount(1);

        await syncService.ApplyAsync(result.Synced);

        // 5. 验证版本现在是 3
        var finalVersion = await tracker.GetCurrentVersionAsync();
        finalVersion.Should().Be(3);
    }

    [Fact]
    public async Task ConfigLoader_BuildsValidConnection()
    {
        var config = ConfigLoader.Load(
            cliHost: _fixture.Host,
            cliPort: _fixture.Port,
            cliUser: _fixture.User,
            cliDatabase: _fixture.DatabaseName
        );

        config.GetConnectionString().Should().Contain(_fixture.DatabaseName);

        var factory = new NpgsqlConnectionFactory(config);
        await using var conn = await factory.CreateConnectionAsync();
        conn.State.Should().Be(System.Data.ConnectionState.Open);
    }

    [Fact]
    public async Task DatabaseRebuildWorkflow_DropCreateMigrate()
    {
        // 先执行迁移在测试数据库上
        await using var conn1 = await _fixture.OpenConnectionAsync();
        var tracker1 = new PostgresMigrationTracker(conn1);
        var runner1 = new MigrationRunner(conn1, tracker1, _renderer);
        await runner1.RunAsync(FixturesPath);
        await conn1.CloseAsync();

        // 模拟 rebuild：断开 → 删除 → 重建
        await using var adminConn = new Npgsql.NpgsqlConnection(_fixture.AdminConnectionString);
        await adminConn.OpenAsync();

        // 断开连接
        await using (var cmd = new Npgsql.NpgsqlCommand(
            $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{_fixture.DatabaseName}' AND pid <> pg_backend_pid()",
            adminConn))
            await cmd.ExecuteNonQueryAsync();

        // 删除
        await using (var cmd = new Npgsql.NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_fixture.DatabaseName}\"", adminConn))
            await cmd.ExecuteNonQueryAsync();

        // 重建
        await using (var cmd = new Npgsql.NpgsqlCommand($"CREATE DATABASE \"{_fixture.DatabaseName}\" ENCODING 'UTF8'", adminConn))
            await cmd.ExecuteNonQueryAsync();
        await adminConn.CloseAsync();

        // Reason: 数据库被删除重建后，连接池中的旧连接已失效，必须清除
        Npgsql.NpgsqlConnection.ClearAllPools();

        // 重新迁移
        await using var conn2 = await _fixture.OpenConnectionAsync();
        var tracker2 = new PostgresMigrationTracker(conn2);
        var runner2 = new MigrationRunner(conn2, tracker2, _renderer);
        var count = await runner2.RunAsync(FixturesPath);
        count.Should().Be(3);

        // 验证所有对象存在
        var inspector = new PostgresSchemaInspector(conn2);
        (await inspector.TableExistsAsync("users")).Should().BeTrue();
        (await inspector.IndexExistsAsync("idx_users_email_verified")).Should().BeTrue();
    }

    [Fact]
    public async Task MigrateUp_AfterPartialFailure_ContinuesFromLastSuccess()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"smith_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        // 创建 3 个迁移，第 2 个会失败
        await File.WriteAllTextAsync(
            Path.Combine(tempDir, "001_create_table.sql"),
            "CREATE TABLE resume_test (id SERIAL PRIMARY KEY);"
        );
        await File.WriteAllTextAsync(
            Path.Combine(tempDir, "002_bad_sql.sql"),
            "SELECT * FROM nonexistent_table_xyz;" // 运行时错误
        );
        await File.WriteAllTextAsync(
            Path.Combine(tempDir, "003_add_column.sql"),
            "ALTER TABLE resume_test ADD COLUMN name TEXT;"
        );

        try
        {
            await using var conn = await _fixture.OpenConnectionAsync();
            var tracker = new PostgresMigrationTracker(conn);
            var runner = new MigrationRunner(conn, tracker, _renderer);

            // 第一次执行 - 第 2 个迁移失败
            try { await runner.RunAsync(tempDir); } catch { /* expected */ }

            var version = await tracker.GetCurrentVersionAsync();
            version.Should().Be(1); // 只有第 1 个成功

            // 修复第 2 个迁移
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "002_bad_sql.sql"),
                "CREATE TABLE fixed_table (id SERIAL PRIMARY KEY);"
            );

            // 第二次执行 - 继续从版本 1 之后开始
            var count = await runner.RunAsync(tempDir);
            count.Should().Be(2); // 执行 002 和 003

            var finalVersion = await tracker.GetCurrentVersionAsync();
            finalVersion.Should().Be(3);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
