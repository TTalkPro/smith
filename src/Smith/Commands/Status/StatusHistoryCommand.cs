using Smith.Configuration;
using Smith.Database;
using Smith.Migration;
using Smith.Rendering;

namespace Smith.Commands.Status;

public static class StatusHistoryCommand
{
    public static async Task<int> ExecuteAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose, int limit = 20)
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
        renderer.Title("Smith - 迁移历史");

        try
        {
            var factory = new NpgsqlConnectionFactory(config);
            await using var connection = await factory.CreateConnectionAsync();
            var tracker = new PostgresMigrationTracker(connection);
            var history = await tracker.GetHistoryAsync(limit);

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
