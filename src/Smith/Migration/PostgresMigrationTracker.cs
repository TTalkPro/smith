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
                version         INTEGER PRIMARY KEY,
                description     VARCHAR(255),
                script_name     VARCHAR(255),
                installed_on    TIMESTAMPTZ DEFAULT NOW(),
                execution_time_ms INTEGER,
                checksum        VARCHAR(64),
                success         BOOLEAN DEFAULT TRUE
            )
            """;
        await using var cmd = new NpgsqlCommand(sql, _connection);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> GetCurrentVersionAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT COALESCE(MAX(version), 0) FROM schema_migrations WHERE success = TRUE";
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
        // Reason: 使用 ON CONFLICT UPDATE 实现幂等记录，重复执行不会报错
        const string sql = """
            INSERT INTO schema_migrations (version, description, script_name, execution_time_ms, checksum, success)
            VALUES ($1, $2, $3, $4, $5, TRUE)
            ON CONFLICT (version) DO UPDATE SET
                description = EXCLUDED.description,
                script_name = EXCLUDED.script_name,
                installed_on = NOW(),
                execution_time_ms = EXCLUDED.execution_time_ms,
                checksum = EXCLUDED.checksum,
                success = TRUE
            """;
        await using var cmd = new NpgsqlCommand(sql, _connection);
        cmd.Parameters.AddWithValue(migration.Version);
        cmd.Parameters.AddWithValue(migration.Description);
        cmd.Parameters.AddWithValue(migration.FileName);
        cmd.Parameters.AddWithValue(elapsedMs);
        cmd.Parameters.AddWithValue(migration.GetChecksum());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RecordFailureAsync(MigrationFile migration, string errorMessage, CancellationToken ct = default)
    {
        // Reason: 失败记录使用 DO NOTHING，避免覆盖已有的成功记录
        const string sql = """
            INSERT INTO schema_migrations (version, description, script_name, checksum, success)
            VALUES ($1, $2, $3, $4, FALSE)
            ON CONFLICT (version) DO NOTHING
            """;
        await using var cmd = new NpgsqlCommand(sql, _connection);
        cmd.Parameters.AddWithValue(migration.Version);
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
