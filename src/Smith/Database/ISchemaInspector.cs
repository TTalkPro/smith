namespace Smith.Database;

/// <summary>
/// Schema 检查接口，用于检测数据库对象是否存在
/// </summary>
public interface ISchemaInspector
{
    Task<bool> TableExistsAsync(string name, string schema = "public", CancellationToken ct = default);
    Task<bool> FunctionExistsAsync(string name, string schema = "public", CancellationToken ct = default);
    Task<bool> ExtensionExistsAsync(string name, CancellationToken ct = default);
    Task<bool> IndexExistsAsync(string name, string schema = "public", CancellationToken ct = default);
    Task<bool> TriggerExistsAsync(string name, string schema = "public", CancellationToken ct = default);
    Task<bool> ViewExistsAsync(string name, string schema = "public", CancellationToken ct = default);
    Task<bool> ObjectExistsAsync(DatabaseObject obj, CancellationToken ct = default);
    Task<int> GetTableCountAsync(CancellationToken ct = default);
}
