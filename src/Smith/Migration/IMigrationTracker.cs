namespace Smith.Migration;

public interface IMigrationTracker
{
    Task EnsureTableExistsAsync(CancellationToken ct = default);

    Task<int> GetCurrentVersionAsync(CancellationToken ct = default);
    
    Task<int> GetCurrentVersionAsync(ScriptType? scriptType, CancellationToken ct = default);

    Task<List<int>> GetAppliedVersionsAsync(CancellationToken ct = default);

    Task RecordAsync(MigrationFile migration, int elapsedMs, CancellationToken ct = default);

    Task RecordFailureAsync(MigrationFile migration, string errorMessage, CancellationToken ct = default);

    Task<List<MigrationRecord>> GetHistoryAsync(int limit = 20, CancellationToken ct = default);
}

public record MigrationRecord(
    int Version,
    string Description,
    string ScriptName,
    DateTime InstalledOn,
    int ExecutionTimeMs,
    string Checksum,
    bool Success);
