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

    public async Task<NpgsqlConnection> CreateConnectionAsync(CancellationToken ct = default)
    {
        var conn = new NpgsqlConnection(_config.GetConnectionString());
        await conn.OpenAsync(ct);
        return conn;
    }

    public async Task<NpgsqlConnection> CreateAdminConnectionAsync(CancellationToken ct = default)
    {
        var conn = new NpgsqlConnection(_config.GetAdminConnectionString());
        await conn.OpenAsync(ct);
        return conn;
    }
}
