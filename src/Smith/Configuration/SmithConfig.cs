using System.Text.RegularExpressions;

namespace Smith.Configuration;

/// <summary>
/// Smith 配置模型，包含数据库连接和路径信息
/// </summary>
public partial class SmithConfig
{
    // Reason: DDL 语句（CREATE/DROP DATABASE）不能使用参数化查询，必须验证名称安全性
    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex SafeDatabaseNamePattern();

    /// <summary>
    /// 验证数据库名称只包含安全字符（字母、数字、下划线）
    /// </summary>
    public static void ValidateDatabaseName(string name)
    {
        if (!SafeDatabaseNamePattern().IsMatch(name))
            throw new ArgumentException($"数据库名称包含非法字符: {name}（只允许字母、数字和下划线）");
    }

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string User { get; set; } = "postgres";
    public string? Password { get; set; }
    public string? Database { get; set; }
    public string? DatabasePath { get; set; }
    public bool Verbose { get; set; }

    /// <summary>
    /// 构建 Npgsql 连接字符串
    /// </summary>
    public string GetConnectionString()
    {
        if (string.IsNullOrEmpty(Database))
            throw new InvalidOperationException("数据库名称未指定");

        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = Host,
            Port = Port,
            Username = User,
            Database = Database
        };

        if (!string.IsNullOrEmpty(Password))
            builder.Password = Password;

        return builder.ConnectionString;
    }

    /// <summary>
    /// 构建连接到 postgres 管理数据库的连接字符串（用于 create/drop 操作）
    /// </summary>
    public string GetAdminConnectionString()
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = Host,
            Port = Port,
            Username = User,
            Database = "postgres"
        };

        if (!string.IsNullOrEmpty(Password))
            builder.Password = Password;

        return builder.ConnectionString;
    }

    /// <summary>
    /// 获取迁移脚本目录路径
    /// </summary>
    public string GetMigrationsPath()
    {
        var basePath = DatabasePath ?? Directory.GetCurrentDirectory();
        return Path.Combine(basePath, "migrations");
    }

    /// <summary>
    /// 获取种子数据目录路径
    /// </summary>
    public string GetSeedsPath(string category = "required")
    {
        var basePath = DatabasePath ?? Directory.GetCurrentDirectory();
        return Path.Combine(basePath, "seeds", category);
    }
}
