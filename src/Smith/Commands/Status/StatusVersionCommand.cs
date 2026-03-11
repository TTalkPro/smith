using Spectre.Console.Cli;
using Smith.Commands.Settings;
using Smith.Database;
using Smith.Migration;

namespace Smith.Commands.Status;

public class StatusVersionCommand : AsyncCommand<ConnectionSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ConnectionSettings settings)
    {
        var config = settings.BuildConfig();
        if (string.IsNullOrEmpty(config.Database))
        {
            Console.Error.WriteLine("错误: 请通过 -d 参数或 SMITH_DATABASE 环境变量指定数据库名称");
            return 1;
        }

        try
        {
            var factory = new NpgsqlConnectionFactory(config);
            await using var connection = await factory.CreateConnectionAsync();
            var tracker = new PostgresMigrationTracker(connection);
            var version = await tracker.GetCurrentVersionAsync();
            Console.WriteLine(version);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"错误: {ex.Message}");
            return 1;
        }
    }
}
