using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Smith.Migration;

/// <summary>
/// 脚本类型：区分 Migration 和 Seed 文件
/// </summary>
public enum ScriptType
{
    /// <summary>标准数据库迁移 (DDL)</summary>
    Migration,
    
    /// <summary>必需种子数据 (参考数据)</summary>
    SeedRequired,
    
    /// <summary>示例种子数据 (演示/测试数据)</summary>
    SeedExample
}

/// <summary>
/// 迁移文件模型：解析文件名、计算校验和、加载 SQL 内容
/// 支持标准 Migration (001_xxx.sql) 和 Seed (S001_xxx.sql) 格式
/// </summary>
public partial class MigrationFile : IComparable<MigrationFile>
{
    // Reason: 匹配 "001_create_xxx.sql" 格式，提取版本号和名称部分
    [GeneratedRegex(@"^(\d+)_(.+)\.sql$", RegexOptions.IgnoreCase)]
    private static partial Regex MigrationFileNamePattern();
    
    // Reason: 匹配 "S001_xxx.sql" 格式，提取 S 前缀、版本号和名称部分
    [GeneratedRegex(@"^S(\d{3})_(.+)\.sql$", RegexOptions.IgnoreCase)]
    private static partial Regex SeedFileNamePattern();

    public string FilePath { get; }
    public string FileName { get; }
    public int Version { get; }
    public string Name { get; }
    public string Description { get; }
    
    /// <summary>
    /// 脚本类型：Migration 或 Seed
    /// </summary>
    public ScriptType Type { get; }

    private string? _content;
    private string? _checksum;

    private MigrationFile(string filePath, int version, string name, ScriptType type)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        Version = version;
        Name = name;
        Type = type;
        Description = GenerateDescription(name);
    }

    /// <summary>
    /// 从文件路径解析迁移文件，失败返回 null
    /// </summary>
    public static MigrationFile? Parse(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        
        // 首先尝试匹配标准 Migration 格式: 001_xxx.sql
        var migrationMatch = MigrationFileNamePattern().Match(fileName);
        if (migrationMatch.Success)
        {
            var version = int.Parse(migrationMatch.Groups[1].Value);
            var name = migrationMatch.Groups[2].Value;
            return new MigrationFile(filePath, version, name, ScriptType.Migration);
        }
        
        // 然后尝试匹配 Seed 格式: S001_xxx.sql
        var seedMatch = SeedFileNamePattern().Match(fileName);
        if (seedMatch.Success)
        {
            var version = int.Parse(seedMatch.Groups[1].Value);
            var name = seedMatch.Groups[2].Value;
            return new MigrationFile(filePath, version, name, ScriptType.SeedRequired);
        }
        
        return null;
    }

    /// <summary>
    /// 从目录加载所有迁移文件，按版本排序
    /// </summary>
    public static List<MigrationFile> LoadFromDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return [];

        return Directory.GetFiles(directoryPath, "*.sql")
            .Select(Parse)
            .Where(m => m is not null)
            .OrderBy(m => m!.Version)
            .ToList()!;
    }

    /// <summary>
    /// 获取 SQL 内容（懒加载，读取后缓存）
    /// </summary>
    public string GetContent()
    {
        _content ??= File.ReadAllText(FilePath, Encoding.UTF8);
        return _content;
    }

    /// <summary>
    /// 计算文件 SHA256 校验和（懒加载，计算后缓存）
    /// </summary>
    public string GetChecksum()
    {
        if (_checksum is not null)
            return _checksum;

        var content = GetContent();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        _checksum = Convert.ToHexStringLower(hash);
        return _checksum;
    }

    /// <summary>
    /// 将下划线分隔的名称转为可读描述
    /// 例如: "create_users_table" -> "Create users table"
    /// </summary>
    private static string GenerateDescription(string name)
    {
        var words = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return string.Empty;

        words[0] = char.ToUpper(words[0][0]) + words[0][1..];
        return string.Join(' ', words);
    }

    public int CompareTo(MigrationFile? other) => Version.CompareTo(other?.Version ?? 0);

    public override string ToString() => $"[{Version:D3}] {Description}";
}
