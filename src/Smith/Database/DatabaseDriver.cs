namespace Smith.Database;

/// <summary>
/// 数据库驱动类型枚举，用于选择对应的数据库实现
/// </summary>
public enum DatabaseDriver
{
    /// <summary>PostgreSQL 数据库</summary>
    PostgreSQL,

    /// <summary>SQLite 数据库</summary>
    Sqlite
}
