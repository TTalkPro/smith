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

        objects.AddRange(ExtractTables(cleanSql));
        objects.AddRange(ExtractFunctions(cleanSql));
        objects.AddRange(ExtractExtensions(cleanSql));
        objects.AddRange(ExtractIndexes(cleanSql));
        objects.AddRange(ExtractTriggers(cleanSql));
        objects.AddRange(ExtractViews(cleanSql));

        // Reason: 去重，同一对象可能在不同的 CREATE 语句中出现（如 IF NOT EXISTS）
        return objects.DistinctBy(o => o.ToString()).ToList();
    }

    private static string StripComments(string sql)
    {
        var result = LineCommentPattern().Replace(sql, "");
        result = BlockCommentPattern().Replace(result, "");
        return result;
    }

    private static IEnumerable<DatabaseObject> ExtractTables(string sql)
    {
        return CreateTablePattern().Matches(sql).Select(m =>
            new DatabaseObject(DatabaseObjectType.Table, m.Groups[2].Value, m.Groups[1].Success ? m.Groups[1].Value : "public"));
    }

    private static IEnumerable<DatabaseObject> ExtractFunctions(string sql)
    {
        return CreateFunctionPattern().Matches(sql).Select(m =>
            new DatabaseObject(DatabaseObjectType.Function, m.Groups[2].Value, m.Groups[1].Success ? m.Groups[1].Value : "public"));
    }

    private static IEnumerable<DatabaseObject> ExtractExtensions(string sql)
    {
        return CreateExtensionPattern().Matches(sql).Select(m =>
            new DatabaseObject(DatabaseObjectType.Extension, m.Groups[1].Value));
    }

    private static IEnumerable<DatabaseObject> ExtractIndexes(string sql)
    {
        return CreateIndexPattern().Matches(sql).Select(m =>
            new DatabaseObject(DatabaseObjectType.Index, m.Groups[1].Value));
    }

    private static IEnumerable<DatabaseObject> ExtractTriggers(string sql)
    {
        return CreateTriggerPattern().Matches(sql).Select(m =>
            new DatabaseObject(DatabaseObjectType.Trigger, m.Groups[1].Value));
    }

    private static IEnumerable<DatabaseObject> ExtractViews(string sql)
    {
        return CreateViewPattern().Matches(sql).Select(m =>
            new DatabaseObject(DatabaseObjectType.View, m.Groups[2].Value, m.Groups[1].Success ? m.Groups[1].Value : "public"));
    }
}
