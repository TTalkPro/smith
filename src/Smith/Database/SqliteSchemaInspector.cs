using Microsoft.Data.Sqlite;

namespace Smith.Database;

/// <summary>
/// SQLite Schema 检查实现，通过 sqlite_master 表检测数据库对象是否存在
/// </summary>
public class SqliteSchemaInspector : ISchemaInspector
{
    private readonly SqliteConnection _connection;

    public SqliteSchemaInspector(SqliteConnection connection)
    {
        _connection = connection;
    }

    /// <summary>检查表是否存在</summary>
    public Task<bool> TableExistsAsync(string name, string schema = "public", CancellationToken ct = default) =>
        ExistsInMasterAsync("table", name, ct);

    /// <summary>SQLite 不支持自定义函数，始终返回 false</summary>
    public Task<bool> FunctionExistsAsync(string name, string schema = "public", CancellationToken ct = default) =>
        Task.FromResult(false);

    /// <summary>SQLite 不支持扩展，始终返回 false</summary>
    public Task<bool> ExtensionExistsAsync(string name, CancellationToken ct = default) =>
        Task.FromResult(false);

    /// <summary>检查索引是否存在</summary>
    public Task<bool> IndexExistsAsync(string name, string schema = "public", CancellationToken ct = default) =>
        ExistsInMasterAsync("index", name, ct);

    /// <summary>检查触发器是否存在</summary>
    public Task<bool> TriggerExistsAsync(string name, string schema = "public", CancellationToken ct = default) =>
        ExistsInMasterAsync("trigger", name, ct);

    /// <summary>检查视图是否存在</summary>
    public Task<bool> ViewExistsAsync(string name, string schema = "public", CancellationToken ct = default) =>
        ExistsInMasterAsync("view", name, ct);

    /// <summary>
    /// 根据对象类型分发到对应的检查方法
    /// </summary>
    public async Task<bool> ObjectExistsAsync(DatabaseObject obj, CancellationToken ct = default) =>
        obj.Type switch
        {
            DatabaseObjectType.Table => await TableExistsAsync(obj.Name, obj.Schema, ct),
            DatabaseObjectType.Function => await FunctionExistsAsync(obj.Name, obj.Schema, ct),
            DatabaseObjectType.Extension => await ExtensionExistsAsync(obj.Name, ct),
            DatabaseObjectType.Index => await IndexExistsAsync(obj.Name, obj.Schema, ct),
            DatabaseObjectType.Trigger => await TriggerExistsAsync(obj.Name, obj.Schema, ct),
            DatabaseObjectType.View => await ViewExistsAsync(obj.Name, obj.Schema, ct),
            _ => false
        };

    /// <summary>获取 public schema 中的用户表数量</summary>
    public async Task<int> GetTableCountAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
        await using var cmd = new SqliteCommand(sql, _connection);
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// 通用的 sqlite_master 存在性检查
    /// </summary>
    private async Task<bool> ExistsInMasterAsync(string type, string name, CancellationToken ct)
    {
        const string sql = "SELECT COUNT(*) FROM sqlite_master WHERE type=@type AND name=@name";
        await using var cmd = new SqliteCommand(sql, _connection);
        cmd.Parameters.AddWithValue("@type", type);
        cmd.Parameters.AddWithValue("@name", name);
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result) > 0;
    }
}
