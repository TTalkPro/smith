using System.Text.RegularExpressions;
using Smith.Database;

namespace Smith.Migration;

/// <summary>
/// SQL 解析器：从 SQL 文本中检测 CREATE 语句并提取数据库对象
/// </summary>
public static partial class SqlObjectDetector
{
    // Reason: 先移除注释避免误匹配注释中的 CREATE 语句
    [GeneratedRegex(@"--[^\n]*", RegexOptions.Compiled)]
    private static partial Regex LineCommentPattern();

    [GeneratedRegex(@"/\*[\s\S]*?\*/", RegexOptions.Compiled)]
    private static partial Regex BlockCommentPattern();

    [GeneratedRegex(@"CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?(?:(\w+)\.)?(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CreateTablePattern();

    [GeneratedRegex(@"CREATE\s+(?:OR\s+REPLACE\s+)?FUNCTION\s+(?:(\w+)\.)?(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CreateFunctionPattern();

    // Reason: 扩展名可含连字符（如 uuid-ossp），匹配引号内或无引号的名称
    [GeneratedRegex(@"CREATE\s+EXTENSION\s+(?:IF\s+NOT\s+EXISTS\s+)?""?([\w-]+)""?", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CreateExtensionPattern();

    [GeneratedRegex(@"CREATE\s+(?:UNIQUE\s+)?INDEX\s+(?:IF\s+NOT\s+EXISTS\s+)?(?:CONCURRENTLY\s+)?(\w+)\s+ON", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CreateIndexPattern();

    [GeneratedRegex(@"CREATE\s+TRIGGER\s+(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CreateTriggerPattern();

    [GeneratedRegex(@"CREATE\s+(?:OR\s+REPLACE\s+)?(?:MATERIALIZED\s+)?VIEW\s+(?:(\w+)\.)?(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CreateViewPattern();

    /// <summary>
    /// 从 SQL 文本中提取所有 CREATE 语句创建的数据库对象
    /// </summary>
    public static List<DatabaseObject> ExtractObjects(string sql)
    {
        var cleanSql = StripComments(sql);
        var objects = new List<DatabaseObject>();

        objects.AddRange(ExtractWithSchema(cleanSql, CreateTablePattern(), DatabaseObjectType.Table));
        objects.AddRange(ExtractWithSchema(cleanSql, CreateFunctionPattern(), DatabaseObjectType.Function));
        objects.AddRange(ExtractSimple(cleanSql, CreateExtensionPattern(), DatabaseObjectType.Extension));
        objects.AddRange(ExtractSimple(cleanSql, CreateIndexPattern(), DatabaseObjectType.Index));
        objects.AddRange(ExtractSimple(cleanSql, CreateTriggerPattern(), DatabaseObjectType.Trigger));
        objects.AddRange(ExtractWithSchema(cleanSql, CreateViewPattern(), DatabaseObjectType.View));

        // Reason: 去重，同一对象可能在不同的 CREATE 语句中出现（如 IF NOT EXISTS）
        return objects.DistinctBy(o => o.ToString()).ToList();
    }

    /// <summary>
    /// 移除 SQL 中的行注释和块注释
    /// </summary>
    private static string StripComments(string sql)
    {
        var result = LineCommentPattern().Replace(sql, "");
        result = BlockCommentPattern().Replace(result, "");
        return result;
    }

    /// <summary>
    /// 从 SQL 中提取带 Schema 前缀的对象（表、函数、视图）
    /// 匹配模式中 Group[1] 为 schema，Group[2] 为对象名
    /// </summary>
    private static IEnumerable<DatabaseObject> ExtractWithSchema(
        string sql, Regex pattern, DatabaseObjectType type) =>
        pattern.Matches(sql).Select(m =>
            new DatabaseObject(type, m.Groups[2].Value,
                m.Groups[1].Success ? m.Groups[1].Value : "public"));

    /// <summary>
    /// 从 SQL 中提取无 Schema 前缀的对象（扩展、索引、触发器）
    /// 匹配模式中 Group[1] 为对象名
    /// </summary>
    private static IEnumerable<DatabaseObject> ExtractSimple(
        string sql, Regex pattern, DatabaseObjectType type) =>
        pattern.Matches(sql).Select(m =>
            new DatabaseObject(type, m.Groups[1].Value));
}
