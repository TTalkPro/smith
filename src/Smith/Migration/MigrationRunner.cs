using System.Diagnostics;
using Npgsql;
using Smith.Rendering;

namespace Smith.Migration;

/// <summary>
/// 迁移执行编排器：加载文件、过滤待执行、按序执行、记录结果
/// </summary>
public class MigrationRunner
{
    private readonly NpgsqlConnection _connection;
    private readonly IMigrationTracker _tracker;
    private readonly IConsoleRenderer _renderer;

    public MigrationRunner(NpgsqlConnection connection, IMigrationTracker tracker, IConsoleRenderer renderer)
    {
        _connection = connection;
        _tracker = tracker;
        _renderer = renderer;
    }

    /// <summary>
    /// 执行迁移：加载文件 → 过滤已执行 → 逐个执行 → 记录结果
    /// </summary>
    /// <param name="migrationsPath">迁移文件目录</param>
    /// <param name="targetVersion">目标版本（null = 全部执行）</param>
    /// <param name="dryRun">预览模式，不实际执行</param>
    /// <returns>成功执行的迁移数量</returns>
    public async Task<int> RunAsync(string migrationsPath, int? targetVersion = null, bool dryRun = false, CancellationToken ct = default)
    {
        await _tracker.EnsureTableExistsAsync(ct);

        var currentVersion = await _tracker.GetCurrentVersionAsync(ct);
        var allMigrations = MigrationFile.LoadFromDirectory(migrationsPath);

        // Reason: 只执行版本号大于当前版本的迁移，并可选限制到目标版本
        var pending = allMigrations
            .Where(m => m.Version > currentVersion)
            .Where(m => targetVersion is null || m.Version <= targetVersion)
            .ToList();

        if (pending.Count == 0)
        {
            _renderer.Info("数据库已是最新版本");
            return 0;
        }

        _renderer.Info($"当前版本: {currentVersion}，待执行: {pending.Count} 个迁移");

        if (dryRun)
        {
            _renderer.Warning("预览模式 (dry-run)，不会实际执行");
            foreach (var m in pending)
                _renderer.Info($"  {m}");
            return pending.Count;
        }

        var successCount = 0;
        foreach (var migration in pending)
        {
            await RunSingleAsync(migration, ct);
            successCount++;
        }

        _renderer.Success($"已成功执行 {successCount} 个迁移");
        return successCount;
    }

    /// <summary>
    /// 在事务中执行单个迁移
    /// </summary>
    private async Task RunSingleAsync(MigrationFile migration, CancellationToken ct)
    {
        _renderer.Info($"执行: {migration}");
        var sw = Stopwatch.StartNew();

        await using var transaction = await _connection.BeginTransactionAsync(ct);
        try
        {
            var sql = migration.GetContent();
            await using var cmd = new NpgsqlCommand(sql, _connection, transaction);
            cmd.CommandTimeout = 300; // 5 分钟超时
            await cmd.ExecuteNonQueryAsync(ct);

            await transaction.CommitAsync(ct);
            sw.Stop();

            var elapsedMs = (int)sw.ElapsedMilliseconds;
            await _tracker.RecordAsync(migration, elapsedMs, ct);

            _renderer.Success($"  完成 ({elapsedMs}ms)");
        }
        catch (Exception ex)
        {
            sw.Stop();
            await transaction.RollbackAsync(ct);

            // Reason: 记录失败但不影响后续操作，让调用者决定是否中止
            await _tracker.RecordFailureAsync(migration, ex.Message, ct);
            _renderer.Error($"  失败: {ex.Message}");
            throw;
        }
    }
}
