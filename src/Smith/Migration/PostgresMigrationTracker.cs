using Npgsql;

namespace Smith.Migration;

/// <summary>
/// PostgreSQL schema_migrations 表管理实现
/// </summary>
public class PostgresMigrationTracker : IMigrationTracker
{
    private readonly NpgsqlConnection _connection;

    public PostgresMigrationTracker(NpgsqlConnection connection)
    {
        _connection = connection;
    }

    public async Task EnsureTableExistsAsync(CancellationToken ct = default)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version         INTEGER NOT NULL,
                script_type    VARCHAR(20) DEFAULT 'Migration',
                description     VARCHAR(255),
                script_name     VARCHAR(255),
                installed_on    TIMESTAMPTZ DEFAULT NOW(),
                execution_time_ms INTEGER,
                checksum        VARCHAR(64),
                success         BOOLEAN DEFAULT TRUE,
                PRIMARY KEY (version, script_type)
            )
            """;
        await using var cmd = new NpgsqlCommand(sql, _connection);
        await cmd.ExecuteNonQueryAsync(ct);
        
        try
        {
            const string alterSql = """
                ALTER TABLE schema_migrations 
                ADD COLUMN IF NOT EXISTS script_type VARCHAR(20) DEFAULT 'Migration'
                """;
            await using var alterCmd = new NpgsqlCommand(alterSql, _connection);
            await alterCmd.ExecuteNonQueryAsync(ct);
        }
        catch
        {
        }
    }

    public async Task<int> GetCurrentVersionAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT COALESCE(MAX(version), 0) FROM schema_migrations WHERE success = TRUE";
        await using var cmd = new NpgsqlCommand(sql, _connection);
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    public async Task<int> GetCurrentVersionAsync(ScriptType? scriptType, CancellationToken ct = default)
    {
        if (scriptType is null)
        {
            return await GetCurrentVersionAsync(ct);
        }
        
        var typeFilter = scriptType == ScriptType.Migration ? "Migration" : "SeedRequired";
        var sql = $"SELECT COALESCE(MAX(version), 0) FROM schema_migrations WHERE success = TRUE AND script_type = '{typeFilter}'";
        await using var cmd = new NpgsqlCommand(sql, _connection);
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    public async Task<List<int>> GetAppliedVersionsAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT version FROM schema_migrations WHERE success = TRUE ORDER BY version";
        await using var cmd = new NpgsqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var versions = new List<int>();
        while (await reader.ReadAsync(ct))
            versions.Add(reader.GetInt32(0));
        return versions;
    }

    public async Task RecordAsync(MigrationFile migration, int elapsedMs, CancellationToken ct = default)
    {
        var scriptType = migration.Type == ScriptType.Migration ? "Migration" : "SeedRequired";
        
        const string sql = """
            INSERT INTO schema_migrations (version, script_type, description, script_name, execution_time_ms, checksum, success)
            VALUES ($1, $2, $3, $4, $5, $6, TRUE)
            ON CONFLICT (version, script_type) DO UPDATE SET
                description = EXCLUDED.description,
                script_name = EXCLUDED.script_name,
                installed_on = NOW(),
                execution_time_ms = EXCLUDED.execution_time_ms,
                checksum = EXCLUDED.checksum,
                success = TRUE
            """;
        await using var cmd = new NpgsqlCommand(sql, _connection);
        cmd.Parameters.AddWithValue(migration.Version);
        cmd.Parameters.AddWithValue(scriptType);
        cmd.Parameters.AddWithValue(migration.Description);
        cmd.Parameters.AddWithValue(migration.FileName);
        cmd.Parameters.AddWithValue(elapsedMs);
        cmd.Parameters.AddWithValue(migration.GetChecksum());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RecordFailureAsync(MigrationFile migration, string errorMessage, CancellationToken ct = default)
    {
        var scriptType = migration.Type == ScriptType.Migration ? "Migration" : "SeedRequired";
        
        const string sql = """
            INSERT INTO schema_migrations (version, script_type, description, script_name, checksum, success)
            VALUES ($1, $2, $3, $4, $5, FALSE)
            ON CONFLICT (version, script_type) DO NOTHING
            """;
        await using var cmd = new NpgsqlCommand(sql, _connection);
        cmd.Parameters.AddWithValue(migration.Version);
        cmd.Parameters.AddWithValue(scriptType);
        cmd.Parameters.AddWithValue($"FAILED: {errorMessage}");
        cmd.Parameters.AddWithValue(migration.FileName);
        cmd.Parameters.AddWithValue(migration.GetChecksum());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<MigrationRecord>> GetHistoryAsync(int limit = 20, CancellationToken ct = default)
    {
        const string sql = """
            SELECT version, description, script_name, installed_on, execution_time_ms, checksum, success
            FROM schema_migrations
            ORDER BY version DESC
            LIMIT $1
            """;
        await using var cmd = new NpgsqlCommand(sql, _connection);
        cmd.Parameters.AddWithValue(limit);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var records = new List<MigrationRecord>();
        while (await reader.ReadAsync(ct))
        {
            records.Add(new MigrationRecord(
                Version: reader.GetInt32(0),
                Description: reader.GetString(1),
                ScriptName: reader.GetString(2),
                InstalledOn: reader.GetDateTime(3),
                ExecutionTimeMs: reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                Checksum: reader.IsDBNull(5) ? "" : reader.GetString(5),
                Success: reader.GetBoolean(6)
            ));
        }
        return records;
    }
}
