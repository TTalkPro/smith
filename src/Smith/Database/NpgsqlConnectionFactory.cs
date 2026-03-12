using System.Data.Common;
using Npgsql;
using Smith.Configuration;

namespace Smith.Database;

/// <summary>
/// Npgsql 连接工厂实现
/// </summary>
public class NpgsqlConnectionFactory : IConnectionFactory
{
    private readonly SmithConfig _config;

    public NpgsqlConnectionFactory(SmithConfig config)
    {
        _config = config;
    }

    /// <summary>创建并打开 PostgreSQL 数据库连接</summary>
    public async Task<DbConnection> CreateConnectionAsync(CancellationToken ct = default)
    {
        var conn = new NpgsqlConnection(_config.GetConnectionString());
        await conn.OpenAsync(ct);
        return conn;
    }

    /// <summary>创建并打开到 postgres 管理数据库的连接</summary>
    public async Task<DbConnection> CreateAdminConnectionAsync(CancellationToken ct = default)
    {
        var conn = new NpgsqlConnection(_config.GetAdminConnectionString());
        await conn.OpenAsync(ct);
        return conn;
    }
}
