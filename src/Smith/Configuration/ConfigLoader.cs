using Microsoft.Extensions.Configuration;
using Smith.Database;

namespace Smith.Configuration;

/// <summary>
/// 配置加载器：CLI 参数 > 环境变量 > JSON 文件 > 默认值
/// </summary>
public static class ConfigLoader
{
    private const string EnvPrefix = "SMITH_";

    /// <summary>
    /// 从多个来源加载配置，优先级：CLI > 环境变量 > JSON > 默认值
    /// </summary>
    public static SmithConfig Load(
        string? cliHost = null,
        int? cliPort = null,
        string? cliUser = null,
        string? cliPassword = null,
        string? cliDatabase = null,
        string? cliDatabasePath = null,
        bool cliVerbose = false)
    {
        // Reason: JSON 配置文件作为最低优先级的持久化配置来源
        var jsonConfig = LoadJsonConfig();

        var config = new SmithConfig
        {
            Host = cliHost
                ?? GetEnv("HOST")
                ?? jsonConfig?["host"]
                ?? "localhost",

            Port = cliPort
                ?? ParseIntEnv("PORT")
                ?? ParseInt(jsonConfig?["port"])
                ?? 5432,

            User = cliUser
                ?? GetEnv("USER")
                ?? jsonConfig?["user"]
                ?? "postgres",

            Password = cliPassword
                ?? GetEnv("PASSWORD")
                ?? jsonConfig?["password"],

            Database = cliDatabase
                ?? GetEnv("DATABASE")
                ?? jsonConfig?["database"],

            DatabasePath = cliDatabasePath
                ?? GetEnv("DATABASE_PATH")
                ?? jsonConfig?["database_path"],

            Verbose = cliVerbose
        };

        config.Driver = DetectDriver(
            GetEnv("DRIVER") ?? jsonConfig?["driver"],
            config.Database);

        return config;
    }

    /// <summary>
    /// 自动检测数据库驱动类型：优先使用显式指定，否则根据文件扩展名推断
    /// </summary>
    private static DatabaseDriver DetectDriver(string? explicitDriver, string? database)
    {
        if (explicitDriver != null)
        {
            return explicitDriver.ToLower() switch
            {
                "sqlite" => DatabaseDriver.Sqlite,
                "postgres" or "postgresql" => DatabaseDriver.PostgreSQL,
                _ => DatabaseDriver.PostgreSQL
            };
        }

        if (database != null &&
            (database.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
             database.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase) ||
             database.EndsWith(".sqlite3", StringComparison.OrdinalIgnoreCase) ||
             database == ":memory:"))
        {
            return DatabaseDriver.Sqlite;
        }

        return DatabaseDriver.PostgreSQL;
    }

    /// <summary>获取 SMITH_ 前缀的环境变量</summary>
    private static string? GetEnv(string name)
    {
        return Environment.GetEnvironmentVariable($"{EnvPrefix}{name}");
    }

    /// <summary>解析整数类型的环境变量</summary>
    private static int? ParseIntEnv(string name)
    {
        var value = GetEnv(name);
        return int.TryParse(value, out var result) ? result : null;
    }

    /// <summary>安全解析整数字符串</summary>
    private static int? ParseInt(string? value)
    {
        return int.TryParse(value, out var result) ? result : null;
    }

    /// <summary>从当前目录加载 smith.json 配置文件</summary>
    private static IConfigurationSection? LoadJsonConfig()
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "smith.json");
        if (!File.Exists(configPath))
            return null;

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: true)
            .Build();

        return configuration.GetSection("smith");
    }
}
