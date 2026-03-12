using System.Data.Common;
using Microsoft.Data.Sqlite;
using Smith.Configuration;

namespace Smith.Database;

/// <summary>
/// SQLite 连接工厂实现
/// </summary>
public class SqliteConnectionFactory : IConnectionFactory
{
    private readonly SmithConfig _config;

    public SqliteConnectionFactory(SmithConfig config)
    {
        _config = config;
    }

    /// <summary>创建并打开 SQLite 数据库连接</summary>
    public async Task<DbConnection> CreateConnectionAsync(CancellationToken ct = default)
    {
        var conn = new SqliteConnection(_config.GetConnectionString());
        await conn.OpenAsync(ct);
        return conn;
    }

    /// <summary>SQLite 没有独立的管理数据库，返回同一连接</summary>
    public async Task<DbConnection> CreateAdminConnectionAsync(CancellationToken ct = default)
    {
        return await CreateConnectionAsync(ct);
    }
}
