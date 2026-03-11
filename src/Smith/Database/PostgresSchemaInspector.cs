using Npgsql;

namespace Smith.Database;

/// <summary>
/// PostgreSQL catalog 查询实现，检测数据库对象是否存在
/// </summary>
public class PostgresSchemaInspector : ISchemaInspector
{
    private readonly NpgsqlConnection _connection;

    public PostgresSchemaInspector(NpgsqlConnection connection)
    {
        _connection = connection;
    }

    public async Task<bool> TableExistsAsync(string name, string schema = "public", CancellationToken ct = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_schema = $1 AND table_name = $2 AND table_type = 'BASE TABLE'
            )
            """;
        return await QueryBoolAsync(sql, schema, name, ct);
    }

    public async Task<bool> FunctionExistsAsync(string name, string schema = "public", CancellationToken ct = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM pg_proc p
                JOIN pg_namespace n ON p.pronamespace = n.oid
                WHERE n.nspname = $1 AND p.proname = $2
            )
            """;
        return await QueryBoolAsync(sql, schema, name, ct);
    }

    public async Task<bool> ExtensionExistsAsync(string name, CancellationToken ct = default)
    {
        const string sql = "SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = $1)";
        await using var cmd = new NpgsqlCommand(sql, _connection);
        cmd.Parameters.AddWithValue(name);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is bool b && b;
    }

    public async Task<bool> IndexExistsAsync(string name, string schema = "public", CancellationToken ct = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM pg_indexes
                WHERE schemaname = $1 AND indexname = $2
            )
            """;
        return await QueryBoolAsync(sql, schema, name, ct);
    }

    public async Task<bool> TriggerExistsAsync(string name, string schema = "public", CancellationToken ct = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM pg_trigger t
                JOIN pg_class c ON t.tgrelid = c.oid
                JOIN pg_namespace n ON c.relnamespace = n.oid
                WHERE n.nspname = $1 AND t.tgname = $2
            )
            """;
        return await QueryBoolAsync(sql, schema, name, ct);
    }

    public async Task<bool> ViewExistsAsync(string name, string schema = "public", CancellationToken ct = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM pg_views WHERE schemaname = $1 AND viewname = $2
                UNION ALL
                SELECT 1 FROM pg_matviews WHERE schemaname = $1 AND matviewname = $2
            )
            """;
        return await QueryBoolAsync(sql, schema, name, ct);
    }

    public async Task<bool> ObjectExistsAsync(DatabaseObject obj, CancellationToken ct = default)
    {
        return obj.Type switch
        {
            DatabaseObjectType.Table => await TableExistsAsync(obj.Name, obj.Schema, ct),
            DatabaseObjectType.Function => await FunctionExistsAsync(obj.Name, obj.Schema, ct),
            DatabaseObjectType.Extension => await ExtensionExistsAsync(obj.Name, ct),
            DatabaseObjectType.Index => await IndexExistsAsync(obj.Name, obj.Schema, ct),
            DatabaseObjectType.Trigger => await TriggerExistsAsync(obj.Name, obj.Schema, ct),
            DatabaseObjectType.View => await ViewExistsAsync(obj.Name, obj.Schema, ct),
            _ => false
        };
    }

    public async Task<int> GetTableCountAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT COUNT(*) FROM information_schema.tables
            WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
            """;
        await using var cmd = new NpgsqlCommand(sql, _connection);
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    private async Task<bool> QueryBoolAsync(string sql, string schema, string name, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, _connection);
        cmd.Parameters.AddWithValue(schema);
        cmd.Parameters.AddWithValue(name);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is bool b && b;
    }
}
