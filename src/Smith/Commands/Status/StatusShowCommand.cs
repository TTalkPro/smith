using Spectre.Console.Cli;
using Smith.Commands.Settings;
using Smith.Database;
using Smith.Migration;
using Smith.Rendering;

namespace Smith.Commands.Status;

public class StatusShowCommand : AsyncCommand<ConnectionSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ConnectionSettings settings)
    {
        var config = settings.BuildConfig();
        if (string.IsNullOrEmpty(config.Database))
        {
            Console.Error.WriteLine("错误: 请通过 -d 参数或 SMITH_DATABASE 环境变量指定数据库名称");
            return 1;
        }

        var renderer = new SpectreRenderer();
        renderer.Title("Smith - 迁移状态");

        try
        {
            var factory = new NpgsqlConnectionFactory(config);
            await using var connection = await factory.CreateConnectionAsync();
            var tracker = new PostgresMigrationTracker(connection);

            var appliedVersions = await tracker.GetAppliedVersionsAsync();
            var allMigrations = MigrationFile.LoadFromDirectory(config.GetMigrationsPath());

            var rows = allMigrations.Select(m => new[]
            {
                m.Version.ToString("D3"),
                m.Description,
                appliedVersions.Contains(m.Version) ? "✓ Applied" : "○ Pending",
                m.FileName
            }).ToList();

            renderer.Table(["版本", "描述", "状态", "文件"], rows);
            renderer.NewLine();
            renderer.Info($"共 {allMigrations.Count} 个迁移，{appliedVersions.Count} 已应用，{allMigrations.Count - appliedVersions.Count} 待执行");
            return 0;
        }
        catch (Exception ex)
        {
            renderer.Error(ex.Message);
            return 1;
        }
    }
}
