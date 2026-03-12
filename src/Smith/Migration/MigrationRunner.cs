using System.Data.Common;
using System.Diagnostics;
using Smith.Rendering;

namespace Smith.Migration;

/// <summary>
/// 迁移执行器：加载待执行的迁移文件，按版本顺序在事务中执行，
/// 并通过 MigrationTracker 记录执行结果
/// </summary>
public class MigrationRunner
{
    private readonly DbConnection _connection;
    private readonly IMigrationTracker _tracker;
    private readonly ISchemaDetector _schemaDetector;
    private readonly IConsoleRenderer _renderer;

    public MigrationRunner(
        DbConnection connection, IMigrationTracker tracker,
        ISchemaDetector schemaDetector, IConsoleRenderer renderer)
    {
        _connection = connection;
        _tracker = tracker;
        _schemaDetector = schemaDetector;
        _renderer = renderer;
    }

    /// <summary>
    /// 执行迁移：检查 Schema 版本 → 加载文件 → 过滤已执行 → 按序执行
    /// </summary>
    /// <returns>成功执行的数量，Schema 版本过旧时返回 -1</returns>
    public async Task<int> RunAsync(
        string migrationsPath, int? targetVersion = null,
        bool dryRun = false, ScriptType? scriptType = null,
        CancellationToken ct = default)
    {
        if (await CheckSchemaUpgradeNeeded(ct))
            return -1;

        await _tracker.EnsureTableExistsAsync(ct);

        var pending = await LoadPendingMigrations(migrationsPath, targetVersion, scriptType, ct);

        if (pending.Count == 0)
        {
            _renderer.Info($"数据库{scriptType.ToLabel()}已是最新版本");
            return 0;
        }

        _renderer.Info($"当前版本: {await _tracker.GetCurrentVersionAsync(scriptType, ct)}，待执行: {pending.Count} 个{scriptType.ToLabel()}");

        if (dryRun)
            return PreviewPending(pending);

        return await ExecutePending(pending, scriptType, ct);
    }

    /// <summary>
    /// 检查 schema_migrations 表是否需要升级
    /// </summary>
    private async Task<bool> CheckSchemaUpgradeNeeded(CancellationToken ct)
    {
        if (!await _schemaDetector.NeedsUpgradeAsync(ct))
            return false;

        var version = await _schemaDetector.DetectCurrentVersionAsync(ct);
        _renderer.Error($"Schema 版本过旧: {version}");
        _renderer.Error("请先运行: smith upgrade-schema run -d <database>");
        return true;
    }

    /// <summary>
    /// 加载并过滤出待执行的迁移文件
    /// </summary>
    private async Task<List<MigrationFile>> LoadPendingMigrations(
        string migrationsPath, int? targetVersion,
        ScriptType? scriptType, CancellationToken ct)
    {
        var allMigrations = MigrationFile.LoadFromDirectory(migrationsPath);

        if (scriptType.HasValue)
            allMigrations = allMigrations.Where(m => m.Type == scriptType.Value).ToList();

        var currentVersion = await _tracker.GetCurrentVersionAsync(scriptType, ct);

        return allMigrations
            .Where(m => m.Version > currentVersion)
            .Where(m => targetVersion is null || m.Version <= targetVersion)
            .ToList();
    }

    /// <summary>
    /// 预览模式：列出将要执行的迁移
    /// </summary>
    private int PreviewPending(List<MigrationFile> pending)
    {
        _renderer.Warning("预览模式 (dry-run)，不会实际执行");
        foreach (var m in pending)
            _renderer.Info($"  [{m.Type}] {m}");
        return pending.Count;
    }

    /// <summary>
    /// 逐个执行待处理的迁移
    /// </summary>
    private async Task<int> ExecutePending(
        List<MigrationFile> pending, ScriptType? scriptType, CancellationToken ct)
    {
        var successCount = 0;
        foreach (var migration in pending)
        {
            await ExecuteSingle(migration, ct);
            successCount++;
        }

        _renderer.Success($"已成功执行 {successCount} 个{scriptType.ToLabel()}");
        return successCount;
    }

    /// <summary>
    /// 在事务中执行单个迁移文件，记录执行结果
    /// </summary>
    private async Task ExecuteSingle(MigrationFile migration, CancellationToken ct)
    {
        _renderer.Info($"执行: [{migration.Type}] {migration}");
        var sw = Stopwatch.StartNew();

        await using var transaction = await _connection.BeginTransactionAsync(ct);
        try
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = migration.GetContent();
            cmd.Transaction = transaction;
            cmd.CommandTimeout = 300;
            await cmd.ExecuteNonQueryAsync(ct);

            await transaction.CommitAsync(ct);
            sw.Stop();

            await _tracker.RecordAsync(migration, (int)sw.ElapsedMilliseconds, ct);
            _renderer.Success($"  完成 ({sw.ElapsedMilliseconds}ms)");
        }
        catch (Exception ex)
        {
            sw.Stop();
            await transaction.RollbackAsync(ct);
            await _tracker.RecordFailureAsync(migration, ex.Message, ct);
            _renderer.Error($"  失败: {ex.Message}");
            throw;
        }
    }
}
