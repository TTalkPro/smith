using Microsoft.Data.Sqlite;

namespace Smith.Migration;

/// <summary>
/// SQLite 迁移记录追踪实现：管理 schema_migrations 表的读写操作
/// </summary>
public class SqliteMigrationTracker : IMigrationTracker
{
    private readonly SqliteConnection _connection;

    public SqliteMigrationTracker(SqliteConnection connection)
    {
        _connection = connection;
    }

    public async Task EnsureTableExistsAsync(CancellationToken ct = default)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version         INTEGER NOT NULL,
                script_type     TEXT DEFAULT 'Migration',
                description     TEXT,
                script_name     TEXT,
                installed_on    TEXT DEFAULT (datetime('now')),
                execution_time_ms INTEGER,
                checksum        TEXT,
                success         INTEGER DEFAULT 1,
                PRIMARY KEY (version, script_type)
            )
            """;
        await using var cmd = new SqliteCommand(sql, _connection);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> GetCurrentVersionAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT COALESCE(MAX(version), 0) FROM schema_migrations WHERE success = 1";
        await using var cmd = new SqliteCommand(sql, _connection);
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    public async Task<int> GetCurrentVersionAsync(ScriptType? scriptType, CancellationToken ct = default)
    {
        if (scriptType is null)
            return await GetCurrentVersionAsync(ct);

        var typeFilter = scriptType == ScriptType.Migration ? "Migration" : "SeedRequired";
        const string sql = "SELECT COALESCE(MAX(version), 0) FROM schema_migrations WHERE success = 1 AND script_type = @type";
        await using var cmd = new SqliteCommand(sql, _connection);
        cmd.Parameters.AddWithValue("@type", typeFilter);
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    public async Task<List<int>> GetAppliedVersionsAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT version FROM schema_migrations WHERE success = 1 ORDER BY version";
        await using var cmd = new SqliteCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var versions = new List<int>();
        while (await reader.ReadAsync(ct))
            versions.Add(reader.GetInt32(0));
        return versions;
    }

    public async Task RecordAsync(MigrationFile migration, int elapsedMs, CancellationToken ct = default)
    {
        var scriptType = migration.Type.ToDbValue();

        const string sql = """
            INSERT INTO schema_migrations (version, script_type, description, script_name, execution_time_ms, checksum, success)
            VALUES (@version, @scriptType, @description, @scriptName, @elapsed, @checksum, 1)
            ON CONFLICT (version, script_type) DO UPDATE SET
                description = excluded.description,
                script_name = excluded.script_name,
                installed_on = datetime('now'),
                execution_time_ms = excluded.execution_time_ms,
                checksum = excluded.checksum,
                success = 1
            """;
        await using var cmd = new SqliteCommand(sql, _connection);
        cmd.Parameters.AddWithValue("@version", migration.Version);
        cmd.Parameters.AddWithValue("@scriptType", scriptType);
        cmd.Parameters.AddWithValue("@description", migration.Description);
        cmd.Parameters.AddWithValue("@scriptName", migration.FileName);
        cmd.Parameters.AddWithValue("@elapsed", elapsedMs);
        cmd.Parameters.AddWithValue("@checksum", migration.GetChecksum());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RecordFailureAsync(MigrationFile migration, string errorMessage, CancellationToken ct = default)
    {
        var scriptType = migration.Type.ToDbValue();

        const string sql = """
            INSERT INTO schema_migrations (version, script_type, description, script_name, checksum, success)
            VALUES (@version, @scriptType, @description, @scriptName, @checksum, 0)
            ON CONFLICT (version, script_type) DO NOTHING
            """;
        await using var cmd = new SqliteCommand(sql, _connection);
        cmd.Parameters.AddWithValue("@version", migration.Version);
        cmd.Parameters.AddWithValue("@scriptType", scriptType);
        cmd.Parameters.AddWithValue("@description", $"FAILED: {errorMessage}");
        cmd.Parameters.AddWithValue("@scriptName", migration.FileName);
        cmd.Parameters.AddWithValue("@checksum", migration.GetChecksum());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<MigrationRecord>> GetHistoryAsync(int limit = 20, CancellationToken ct = default)
    {
        const string sql = """
            SELECT version, description, script_name, installed_on, execution_time_ms, checksum, success
            FROM schema_migrations
            ORDER BY version DESC
            LIMIT @limit
            """;
        await using var cmd = new SqliteCommand(sql, _connection);
        cmd.Parameters.AddWithValue("@limit", limit);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var records = new List<MigrationRecord>();
        while (await reader.ReadAsync(ct))
        {
            records.Add(new MigrationRecord(
                Version: reader.GetInt32(0),
                Description: reader.GetString(1),
                ScriptName: reader.GetString(2),
                InstalledOn: DateTime.Parse(reader.GetString(3)),
                ExecutionTimeMs: reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                Checksum: reader.IsDBNull(5) ? "" : reader.GetString(5),
                Success: reader.GetInt32(6) == 1
            ));
        }
        return records;
    }
}
