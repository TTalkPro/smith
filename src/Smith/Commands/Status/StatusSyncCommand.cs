using Smith.Configuration;
using Smith.Database;
using Smith.Migration;
using Smith.Rendering;

namespace Smith.Commands.Status;

public static class StatusSyncCommand
{
    public static async Task<int> ExecuteAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose, bool dryRun)
    {
        var config = ConfigLoader.Load(cliHost: host, cliPort: port, cliUser: user,
            cliPassword: password, cliDatabase: database, cliDatabasePath: databasePath,
            cliVerbose: verbose);
        
        if (string.IsNullOrEmpty(config.Database))
        {
            Console.Error.WriteLine("错误: 请通过 -d 参数或 SMITH_DATABASE 环境变量指定数据库名称");
            return 1;
        }

        var renderer = new TerminalGuiRenderer();
        renderer.Title("Smith - 同步迁移状态");

        try
        {
            var factory = new NpgsqlConnectionFactory(config);
            await using var connection = await factory.CreateConnectionAsync();
            var tracker = new PostgresMigrationTracker(connection);
            var inspector = new PostgresSchemaInspector(connection);
            var syncService = new MigrationSyncService(tracker, inspector, renderer);

            await tracker.EnsureTableExistsAsync();
            var appliedVersions = await tracker.GetAppliedVersionsAsync();
            var allMigrations = MigrationFile.LoadFromDirectory(config.GetMigrationsPath());
            var pending = allMigrations.Where(m => !appliedVersions.Contains(m.Version)).ToList();

            if (pending.Count == 0)
            {
                renderer.Info("没有需要同步的迁移");
                return 0;
            }

            renderer.Info($"发现 {pending.Count} 个未记录的迁移，正在分析...");
            var result = await syncService.AnalyzeAsync(pending);

            if (result.Synced.Count > 0)
            {
                renderer.NewLine();
                renderer.Info("可同步的迁移:");
                foreach (var item in result.Synced)
                {
                    renderer.Success($"  {item.Migration} ({item.Objects.Count} 个对象已存在)");
                }
            }

            if (result.Skipped.Count > 0)
            {
                renderer.NewLine();
                renderer.Warning("无法同步的迁移:");
                foreach (var item in result.Skipped)
                {
                    renderer.Warning($"  {item.Migration}");
                    foreach (var missing in item.Missing)
                        renderer.Warning($"    缺失: {missing}");
                }
            }

            if (dryRun || result.Synced.Count == 0)
            {
                if (dryRun)
                    renderer.Warning("预览模式，不会实际执行");
                return 0;
            }

            renderer.NewLine();
            await syncService.ApplyAsync(result.Synced);
            renderer.Success($"已同步 {result.Synced.Count} 个迁移");
            return 0;
        }
        catch (Exception ex)
        {
            renderer.Error(ex.Message);
            return 1;
        }
    }
}
