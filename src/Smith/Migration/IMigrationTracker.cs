namespace Smith.Migration;

/// <summary>
/// 迁移版本追踪接口（schema_migrations 表操作）
/// </summary>
public interface IMigrationTracker
{
    /// <summary>
    /// 确保 schema_migrations 表存在
    /// </summary>
    Task EnsureTableExistsAsync(CancellationToken ct = default);

    /// <summary>
    /// 获取当前数据库版本（最大的成功迁移版本号）
    /// </summary>
    Task<int> GetCurrentVersionAsync(CancellationToken ct = default);

    /// <summary>
    /// 获取所有已应用的版本号列表
    /// </summary>
    Task<List<int>> GetAppliedVersionsAsync(CancellationToken ct = default);

    /// <summary>
    /// 记录成功的迁移
    /// </summary>
    Task RecordAsync(MigrationFile migration, int elapsedMs, CancellationToken ct = default);

    /// <summary>
    /// 记录失败的迁移
    /// </summary>
    Task RecordFailureAsync(MigrationFile migration, string errorMessage, CancellationToken ct = default);

    /// <summary>
    /// 获取迁移历史记录
    /// </summary>
    Task<List<MigrationRecord>> GetHistoryAsync(int limit = 20, CancellationToken ct = default);
}

/// <summary>
/// 迁移历史记录
/// </summary>
public record MigrationRecord(
    int Version,
    string Description,
    string ScriptName,
    DateTime InstalledOn,
    int ExecutionTimeMs,
    string Checksum,
    bool Success);
