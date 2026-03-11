using Npgsql;

namespace Smith.Migration;

public class SchemaDetector
{
    private readonly NpgsqlConnection _connection;

    public SchemaDetector(NpgsqlConnection connection)
    {
        _connection = connection;
    }

    public async Task<SchemaVersion> DetectCurrentVersionAsync(CancellationToken ct = default)
    {
        var hasScriptType = await HasColumnAsync("script_type", ct);
        var isCompositePk = await IsCompositePrimaryKeyAsync(ct);

        if (hasScriptType && isCompositePk)
        {
            return SchemaVersion.V2;
        }
        
        return SchemaVersion.V1;
    }

    public async Task<SchemaDiff> GetDiffAsync(SchemaVersion targetVersion, CancellationToken ct = default)
    {
        var currentVersion = await DetectCurrentVersionAsync(ct);
        var changes = new List<string>();

        if (currentVersion == targetVersion)
        {
            return new SchemaDiff(currentVersion, targetVersion, changes);
        }

        if (currentVersion == SchemaVersion.V1 && targetVersion == SchemaVersion.V2)
        {
            changes.Add("添加 script_type 列 (VARCHAR(20) DEFAULT 'Migration')");
            changes.Add("修改主键: PRIMARY KEY (version) → PRIMARY KEY (version, script_type)");
        }

        return new SchemaDiff(currentVersion, targetVersion, changes);
    }

    public async Task<bool> NeedsUpgradeAsync(CancellationToken ct = default)
    {
        if (!await TableExistsAsync(ct))
        {
            return false;
        }
        
        var currentVersion = await DetectCurrentVersionAsync(ct);
        return currentVersion < SchemaDefinitions.LatestVersion;
    }

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
        var columnCount = Convert.ToInt32(result);
        
        return columnCount > 1;
    }
}

public record SchemaDiff(
    SchemaVersion CurrentVersion,
    SchemaVersion TargetVersion,
    List<string> Changes);
