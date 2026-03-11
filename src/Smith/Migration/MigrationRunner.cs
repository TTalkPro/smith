using System.Diagnostics;
using Npgsql;
using Smith.Rendering;

namespace Smith.Migration;

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

    public async Task<int> RunAsync(string migrationsPath, int? targetVersion = null, bool dryRun = false, ScriptType? scriptType = null, CancellationToken ct = default)
    {
        var detector = new SchemaDetector(_connection);
        if (await detector.NeedsUpgradeAsync(ct))
        {
            var currentSchemaVersion = await detector.DetectCurrentVersionAsync(ct);
            _renderer.Error($"Schema 版本过旧: {currentSchemaVersion}");
            _renderer.Error("请先运行: smith upgrade-schema run -d <database>");
            return -1;
        }

        await _tracker.EnsureTableExistsAsync(ct);

        var allMigrations = MigrationFile.LoadFromDirectory(migrationsPath);
        
        if (scriptType.HasValue)
        {
            allMigrations = allMigrations.Where(m => m.Type == scriptType.Value).ToList();
        }

        var currentVersion = await _tracker.GetCurrentVersionAsync(scriptType, ct);

        var pending = allMigrations
            .Where(m => m.Version > currentVersion)
            .Where(m => targetVersion is null || m.Version <= targetVersion)
            .ToList();

        if (pending.Count == 0)
        {
            var typeLabel = scriptType switch
            {
                ScriptType.Migration => "迁移",
                ScriptType.SeedRequired => "种子数据",
                _ => "脚本"
            };
            _renderer.Info($"数据库{typeLabel}已是最新版本");
            return 0;
        }

        var typeDesc = scriptType switch
        {
            ScriptType.Migration => "迁移",
            ScriptType.SeedRequired => "种子数据",
            _ => "脚本"
        };
        _renderer.Info($"当前版本: {currentVersion}，待执行: {pending.Count} 个{typeDesc}");

        if (dryRun)
        {
            _renderer.Warning("预览模式 (dry-run)，不会实际执行");
            foreach (var m in pending)
                _renderer.Info($"  [{m.Type}] {m}");
            return pending.Count;
        }

        var successCount = 0;
        foreach (var migration in pending)
        {
            await RunSingleAsync(migration, ct);
            successCount++;
        }

        _renderer.Success($"已成功执行 {successCount} 个{typeDesc}");
        return successCount;
    }

    private async Task RunSingleAsync(MigrationFile migration, CancellationToken ct)
    {
        _renderer.Info($"执行: [{migration.Type}] {migration}");
        var sw = Stopwatch.StartNew();

        await using var transaction = await _connection.BeginTransactionAsync(ct);
        try
        {
            var sql = migration.GetContent();
            await using var cmd = new NpgsqlCommand(sql, _connection, transaction);
            cmd.CommandTimeout = 300;
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

            await _tracker.RecordFailureAsync(migration, ex.Message, ct);
            _renderer.Error($"  失败: {ex.Message}");
            throw;
        }
    }
}
