using System.Data.Common;

namespace Smith.Database;

/// <summary>
/// 数据库连接工厂接口
/// </summary>
public interface IConnectionFactory
{
    /// <summary>
    /// 创建并打开一个数据库连接
    /// </summary>
    Task<DbConnection> CreateConnectionAsync(CancellationToken ct = default);

    /// <summary>
    /// 创建并打开到管理数据库的连接（PostgreSQL: postgres 库，SQLite: 同一连接）
    /// </summary>
    Task<DbConnection> CreateAdminConnectionAsync(CancellationToken ct = default);
}
