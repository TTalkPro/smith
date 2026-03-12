namespace Smith.Migration;

/// <summary>
/// ScriptType 枚举的扩展方法，提供显示标签和数据库值的统一转换
/// </summary>
public static class ScriptTypeExtensions
{
    /// <summary>
    /// 转换为中文显示标签（用于用户界面输出）
    /// </summary>
    public static string ToLabel(this ScriptType type) => type switch
    {
        ScriptType.Migration => "迁移",
        ScriptType.SeedRequired => "种子数据",
        _ => "脚本"
    };

    /// <summary>
    /// 可空类型的中文显示标签（null 表示全部类型）
    /// </summary>
    public static string ToLabel(this ScriptType? type) => type switch
    {
        ScriptType.Migration => "迁移",
        ScriptType.SeedRequired => "种子数据",
        _ => "脚本"
    };

    /// <summary>
    /// 转换为数据库存储值（用于 schema_migrations 表的 script_type 列）
    /// </summary>
    public static string ToDbValue(this ScriptType type) =>
        type == ScriptType.Migration ? "Migration" : "SeedRequired";
}
