using Npgsql;

namespace Smith.Tests.Integration;

/// <summary>
/// 集成测试数据库 fixture：创建临时测试数据库，测试完成后删除
/// </summary>
public class TestDatabaseFixture : IAsyncLifetime
{
    // Reason: 使用唯一名称避免并行测试冲突
    public string DatabaseName { get; } = $"smith_test_{Guid.NewGuid():N}"[..30];
    public string Host => "localhost";
    public int Port => 5432;
    public string User => "postgres";

    public string ConnectionString =>
        $"Host={Host};Port={Port};Username={User};Database={DatabaseName}";

    public string AdminConnectionString =>
        $"Host={Host};Port={Port};Username={User};Database=postgres";

    public async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    public async Task InitializeAsync()
    {
        await using var adminConn = new NpgsqlConnection(AdminConnectionString);
        await adminConn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{DatabaseName}\" ENCODING 'UTF8'", adminConn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        // Reason: 先断开所有连接才能删除数据库
        await using var adminConn = new NpgsqlConnection(AdminConnectionString);
        await adminConn.OpenAsync();

        await using var terminateCmd = new NpgsqlCommand(
            $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{DatabaseName}' AND pid <> pg_backend_pid()",
            adminConn);
        await terminateCmd.ExecuteNonQueryAsync();

        await using var dropCmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{DatabaseName}\"", adminConn);
        await dropCmd.ExecuteNonQueryAsync();
    }
}
