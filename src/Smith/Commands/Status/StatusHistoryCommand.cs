using Spectre.Console.Cli;
using Smith.Commands.Settings;
using Smith.Database;
using Smith.Migration;
using Smith.Rendering;

namespace Smith.Commands.Status;

public class StatusHistoryCommand : AsyncCommand<StatusHistoryCommand.Settings>
{
    public class Settings : ConnectionSettings
    {
        [CommandOption("-n|--limit")]
        public int Limit { get; set; } = 20;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var config = settings.BuildConfig();
        if (string.IsNullOrEmpty(config.Database))
        {
            Console.Error.WriteLine("错误: 请通过 -d 参数或 SMITH_DATABASE 环境变量指定数据库名称");
            return 1;
        }

        var renderer = new SpectreRenderer();
        renderer.Title("Smith - 迁移历史");

        try
        {
            var factory = new NpgsqlConnectionFactory(config);
            await using var connection = await factory.CreateConnectionAsync();
            var tracker = new PostgresMigrationTracker(connection);
            var history = await tracker.GetHistoryAsync(settings.Limit);

            if (history.Count == 0)
            {
                renderer.Info("暂无迁移记录");
                return 0;
            }

            var rows = history.Select(r => new[]
            {
                r.Version.ToString("D3"),
                r.Description,
                r.InstalledOn.ToString("yyyy-MM-dd HH:mm:ss"),
                $"{r.ExecutionTimeMs}ms",
                r.Success ? "✓" : "✗"
            }).ToList();

            renderer.Table(["版本", "描述", "执行时间", "耗时", "状态"], rows);
            return 0;
        }
        catch (Exception ex)
        {
            renderer.Error(ex.Message);
            return 1;
        }
    }
}
