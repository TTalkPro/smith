using Smith.Migration;

namespace Smith.Commands.Status;

/// <summary>
/// 同步迁移状态：检测已在数据库中手动执行但未记录的迁移，
/// 通过 Schema 对象检查验证后补录到 schema_migrations 表
/// </summary>
public static class StatusSyncCommand
{
    public static Task<int> ExecuteAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose, bool dryRun) =>
        CommandContext.RunAsync(database, host, port, user, password, databasePath, verbose,
            async ctx =>
            {
                ctx.Renderer.Title("Smith - 同步迁移状态");

                var connection = await ctx.GetConnectionAsync();
                var tracker = ctx.CreateTracker(connection);
                var inspector = ctx.CreateSchemaInspector(connection);
                var syncService = new MigrationSyncService(tracker, inspector, ctx.Renderer);

                await tracker.EnsureTableExistsAsync();
                var pending = await FindPendingMigrations(tracker, ctx.Config.GetMigrationsPath());

                if (pending.Count == 0)
                {
                    ctx.Renderer.Info("没有需要同步的迁移");
                    return 0;
                }

                ctx.Renderer.Info($"发现 {pending.Count} 个未记录的迁移，正在分析...");
                var result = await syncService.AnalyzeAsync(pending);

                RenderSyncResult(ctx.Renderer, result);

                if (dryRun || result.Synced.Count == 0)
                {
                    if (dryRun) ctx.Renderer.Warning("预览模式，不会实际执行");
                    return 0;
                }

                ctx.Renderer.NewLine();
                await syncService.ApplyAsync(result.Synced);
                ctx.Renderer.Success($"已同步 {result.Synced.Count} 个迁移");
                return 0;
            });

    /// <summary>
    /// 查找未记录的迁移文件
    /// </summary>
    private static async Task<List<MigrationFile>> FindPendingMigrations(
        IMigrationTracker tracker, string migrationsPath)
    {
        var appliedVersions = await tracker.GetAppliedVersionsAsync();
        var allMigrations = MigrationFile.LoadFromDirectory(migrationsPath);
        return allMigrations.Where(m => !appliedVersions.Contains(m.Version)).ToList();
    }

    /// <summary>
    /// 渲染同步分析结果
    /// </summary>
    private static void RenderSyncResult(Rendering.IConsoleRenderer renderer, SyncAnalysisResult result)
    {
        if (result.Synced.Count > 0)
        {
            renderer.NewLine();
            renderer.Info("可同步的迁移:");
            foreach (var item in result.Synced)
                renderer.Success($"  {item.Migration} ({item.Objects.Count} 个对象已存在)");
        }

        if (result.Skipped.Count > 0)
        {
            renderer.NewLine();
            renderer.Warning("无法同步的迁移:");
            RenderSkippedMigrations(renderer, result.Skipped);
        }
    }

    /// <summary>
    /// 渲染无法同步的迁移详情
    /// </summary>
    private static void RenderSkippedMigrations(
        Rendering.IConsoleRenderer renderer, List<SkippedMigration> skipped)
    {
        foreach (var item in skipped)
        {
            renderer.Warning($"  {item.Migration}");
            foreach (var missing in item.Missing)
                renderer.Warning($"    缺失: {missing}");
        }
    }
}
