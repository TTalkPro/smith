using System.Data.Common;
using Microsoft.Data.Sqlite;
using Npgsql;
using Smith.Configuration;
using Smith.Migration;

namespace Smith.Database;

/// <summary>
/// 根据数据库驱动类型创建对应的服务实现
/// </summary>
public static class DbServices
{
    /// <summary>创建数据库连接工厂</summary>
    public static IConnectionFactory CreateConnectionFactory(SmithConfig config) =>
        config.Driver switch
        {
            DatabaseDriver.Sqlite => new SqliteConnectionFactory(config),
            _ => new NpgsqlConnectionFactory(config)
        };

    /// <summary>创建迁移记录追踪器</summary>
    public static IMigrationTracker CreateTracker(DbConnection connection, SmithConfig config) =>
        config.Driver switch
        {
            DatabaseDriver.Sqlite => new SqliteMigrationTracker((SqliteConnection)connection),
            _ => new PostgresMigrationTracker((NpgsqlConnection)connection)
        };

    /// <summary>创建 Schema 检查器</summary>
    public static ISchemaInspector CreateInspector(DbConnection connection, SmithConfig config) =>
        config.Driver switch
        {
            DatabaseDriver.Sqlite => new SqliteSchemaInspector((SqliteConnection)connection),
            _ => new PostgresSchemaInspector((NpgsqlConnection)connection)
        };

    /// <summary>创建 Schema 版本检测器</summary>
    public static ISchemaDetector CreateSchemaDetector(DbConnection connection, SmithConfig config) =>
        config.Driver switch
        {
            DatabaseDriver.Sqlite => new SqliteSchemaDetector(),
            _ => new PostgresSchemaDetector((NpgsqlConnection)connection)
        };
}
