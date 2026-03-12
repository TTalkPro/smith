namespace Smith.Migration;

/// <summary>
/// 迁移记录追踪接口：管理 schema_migrations 表的读写操作
/// </summary>
public interface IMigrationTracker
{
    /// <summary>确保 schema_migrations 表存在，不存在则创建</summary>
    Task EnsureTableExistsAsync(CancellationToken ct = default);

    /// <summary>获取当前最高已执行版本号</summary>
    Task<int> GetCurrentVersionAsync(CancellationToken ct = default);

    /// <summary>获取指定脚本类型的最高已执行版本号</summary>
    Task<int> GetCurrentVersionAsync(ScriptType? scriptType, CancellationToken ct = default);

    /// <summary>获取所有已成功执行的版本号列表</summary>
    Task<List<int>> GetAppliedVersionsAsync(CancellationToken ct = default);

    /// <summary>记录迁移成功执行</summary>
    Task RecordAsync(MigrationFile migration, int elapsedMs, CancellationToken ct = default);

    /// <summary>记录迁移执行失败</summary>
    Task RecordFailureAsync(MigrationFile migration, string errorMessage, CancellationToken ct = default);

    /// <summary>获取迁移执行历史记录</summary>
    Task<List<MigrationRecord>> GetHistoryAsync(int limit = 20, CancellationToken ct = default);
}

/// <summary>
/// 迁移历史记录模型
/// </summary>
public record MigrationRecord(
    int Version,
    string Description,
    string ScriptName,
    DateTime InstalledOn,
    int ExecutionTimeMs,
    string Checksum,
    bool Success);
