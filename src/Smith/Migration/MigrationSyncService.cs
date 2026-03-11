using Smith.Database;
using Smith.Rendering;

namespace Smith.Migration;

/// <summary>
/// 智能同步服务：检测已执行但未记录的迁移，通过 Schema 检查验证后补录
/// </summary>
public class MigrationSyncService
{
    private readonly IMigrationTracker _tracker;
    private readonly ISchemaInspector _inspector;
    private readonly IConsoleRenderer _renderer;

    public MigrationSyncService(IMigrationTracker tracker, ISchemaInspector inspector, IConsoleRenderer renderer)
    {
        _tracker = tracker;
        _inspector = inspector;
        _renderer = renderer;
    }

    /// <summary>
    /// 分析待同步的迁移文件，检测哪些已在数据库中存在
    /// </summary>
    /// <returns>可同步和已跳过的迁移列表</returns>
    public async Task<SyncAnalysisResult> AnalyzeAsync(List<MigrationFile> pendingMigrations, CancellationToken ct = default)
    {
        var synced = new List<SyncedMigration>();
        var skipped = new List<SkippedMigration>();

        foreach (var migration in pendingMigrations)
        {
            var sql = migration.GetContent();
            var objects = SqlObjectDetector.ExtractObjects(sql);

            if (objects.Count == 0)
            {
                skipped.Add(new SkippedMigration(migration, ["无可检测的数据库对象"]));
                continue;
            }

            var existing = new List<DatabaseObject>();
            var missing = new List<DatabaseObject>();

            foreach (var obj in objects)
            {
                if (await _inspector.ObjectExistsAsync(obj, ct))
                    existing.Add(obj);
                else
                    missing.Add(obj);
            }

            if (missing.Count == 0)
                synced.Add(new SyncedMigration(migration, existing));
            else
                skipped.Add(new SkippedMigration(migration, missing.Select(o => o.ToString()).ToList()));
        }

        return new SyncAnalysisResult(synced, skipped);
    }

    /// <summary>
    /// 将已验证的迁移记录到 schema_migrations 表
    /// </summary>
    public async Task ApplyAsync(List<SyncedMigration> syncedMigrations, CancellationToken ct = default)
    {
        foreach (var item in syncedMigrations)
        {
            // Reason: 同步的迁移使用 0ms 执行时间，表示是补录而非实际执行
            await _tracker.RecordAsync(item.Migration, elapsedMs: 0, ct);
            _renderer.Success($"已同步: {item.Migration}");
        }
    }
}

public record SyncedMigration(MigrationFile Migration, List<DatabaseObject> Objects);
public record SkippedMigration(MigrationFile Migration, List<string> Missing);
public record SyncAnalysisResult(List<SyncedMigration> Synced, List<SkippedMigration> Skipped);
