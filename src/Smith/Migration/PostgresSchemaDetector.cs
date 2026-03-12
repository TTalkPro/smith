using Npgsql;

namespace Smith.Migration;

/// <summary>
/// PostgreSQL Schema 版本检测器：通过 information_schema 检查表结构来判断版本
/// </summary>
public class PostgresSchemaDetector : ISchemaDetector
{
    private readonly NpgsqlConnection _connection;

    public PostgresSchemaDetector(NpgsqlConnection connection)
    {
        _connection = connection;
    }

    /// <summary>
    /// 通过检查 script_type 列和复合主键来判断当前 Schema 版本
    /// </summary>
    public async Task<SchemaVersion> DetectCurrentVersionAsync(CancellationToken ct = default)
    {
        var hasScriptType = await HasColumnAsync("script_type", ct);
        var isCompositePk = await IsCompositePrimaryKeyAsync(ct);

        return (hasScriptType && isCompositePk) ? SchemaVersion.V2 : SchemaVersion.V1;
    }

    /// <summary>
    /// 获取当前版本与目标版本之间的差异描述
    /// </summary>
    public async Task<SchemaDiff> GetDiffAsync(SchemaVersion targetVersion, CancellationToken ct = default)
    {
        var currentVersion = await DetectCurrentVersionAsync(ct);
        var changes = new List<string>();

        if (currentVersion == SchemaVersion.V1 && targetVersion == SchemaVersion.V2)
        {
            changes.Add("添加 script_type 列 (VARCHAR(20) DEFAULT 'Migration')");
            changes.Add("修改主键: PRIMARY KEY (version) → PRIMARY KEY (version, script_type)");
        }

        return new SchemaDiff(currentVersion, targetVersion, changes);
    }

    /// <summary>
    /// 检查 schema_migrations 表是否存在且版本低于最新
    /// </summary>
    public async Task<bool> NeedsUpgradeAsync(CancellationToken ct = default)
    {
        if (!await TableExistsAsync(ct))
            return false;

        var currentVersion = await DetectCurrentVersionAsync(ct);
        return currentVersion < SchemaDefinitions.LatestVersion;
    }

    /// <summary>检查 schema_migrations 表是否存在</summary>
    private async Task<bool> TableExistsAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_name = 'schema_migrations'
            """;
        await using var cmd = new NpgsqlCommand(sql, _connection);
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result) > 0;
    }

    /// <summary>检查 schema_migrations 表是否包含指定列</summary>
    private async Task<bool> HasColumnAsync(string columnName, CancellationToken ct)
    {
        var sql = $"""
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_name = 'schema_migrations'
            AND column_name = '{columnName}'
            """;
        await using var cmd = new NpgsqlCommand(sql, _connection);
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result) > 0;
    }

    /// <summary>检查 schema_migrations 表是否使用复合主键</summary>
    private async Task<bool> IsCompositePrimaryKeyAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
                ON tc.constraint_name = kcu.constraint_name
            WHERE tc.table_name = 'schema_migrations'
            AND tc.constraint_type = 'PRIMARY KEY'
            """;
        await using var cmd = new NpgsqlCommand(sql, _connection);
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result) > 1;
    }
}
