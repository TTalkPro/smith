using Npgsql;

namespace Smith.Database;

/// <summary>
/// 数据库连接工厂接口
/// </summary>
public interface IConnectionFactory
{
    /// <summary>
    /// 创建并打开一个数据库连接
    /// </summary>
    Task<NpgsqlConnection> CreateConnectionAsync(CancellationToken ct = default);

    /// <summary>
    /// 创建并打开到 postgres 管理数据库的连接
    /// </summary>
    Task<NpgsqlConnection> CreateAdminConnectionAsync(CancellationToken ct = default);
}
