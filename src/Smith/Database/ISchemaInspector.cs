namespace Smith.Database;

/// <summary>
/// Schema 检查接口，用于检测数据库对象是否存在
/// </summary>
public interface ISchemaInspector
{
    /// <summary>检查表是否存在</summary>
    Task<bool> TableExistsAsync(string name, string schema = "public", CancellationToken ct = default);

    /// <summary>检查函数是否存在</summary>
    Task<bool> FunctionExistsAsync(string name, string schema = "public", CancellationToken ct = default);

    /// <summary>检查扩展是否存在</summary>
    Task<bool> ExtensionExistsAsync(string name, CancellationToken ct = default);

    /// <summary>检查索引是否存在</summary>
    Task<bool> IndexExistsAsync(string name, string schema = "public", CancellationToken ct = default);

    /// <summary>检查触发器是否存在</summary>
    Task<bool> TriggerExistsAsync(string name, string schema = "public", CancellationToken ct = default);

    /// <summary>检查视图是否存在</summary>
    Task<bool> ViewExistsAsync(string name, string schema = "public", CancellationToken ct = default);

    /// <summary>根据对象类型检查数据库对象是否存在</summary>
    Task<bool> ObjectExistsAsync(DatabaseObject obj, CancellationToken ct = default);

    /// <summary>获取用户表数量</summary>
    Task<int> GetTableCountAsync(CancellationToken ct = default);
}
