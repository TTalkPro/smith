using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Smith.Migration;

/// <summary>
/// 迁移文件模型：解析文件名、计算校验和、加载 SQL 内容
/// </summary>
public partial class MigrationFile : IComparable<MigrationFile>
{
    // Reason: 匹配 "001_create_xxx.sql" 格式，提取版本号和名称部分
    [GeneratedRegex(@"^(\d+)_(.+)\.sql$", RegexOptions.IgnoreCase)]
    private static partial Regex FileNamePattern();

    public string FilePath { get; }
    public string FileName { get; }
    public int Version { get; }
    public string Name { get; }
    public string Description { get; }

    private string? _content;
    private string? _checksum;

    private MigrationFile(string filePath, int version, string name)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        Version = version;
        Name = name;
        Description = GenerateDescription(name);
    }

    /// <summary>
    /// 从文件路径解析迁移文件，失败返回 null
    /// </summary>
    public static MigrationFile? Parse(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var match = FileNamePattern().Match(fileName);
        if (!match.Success)
            return null;

        var version = int.Parse(match.Groups[1].Value);
        var name = match.Groups[2].Value;
        return new MigrationFile(filePath, version, name);
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
