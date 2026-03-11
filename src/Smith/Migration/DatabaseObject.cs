namespace Smith.Database;

/// <summary>
/// 数据库对象类型枚举
/// </summary>
public enum DatabaseObjectType
{
    Table,
    Function,
    Extension,
    Index,
    Trigger,
    View
}

/// <summary>
/// 数据库对象模型（表、函数、索引等）
/// </summary>
public record DatabaseObject(DatabaseObjectType Type, string Name, string Schema = "public")
{
    public override string ToString() => Schema == "public"
        ? $"{Type.ToString().ToLower()} {Name}"
        : $"{Type.ToString().ToLower()} {Schema}.{Name}";
}
