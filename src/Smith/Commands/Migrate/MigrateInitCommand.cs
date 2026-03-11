using Spectre.Console.Cli;
using Smith.Commands.Settings;
using Smith.Database;
using Smith.Migration;
using Smith.Rendering;

namespace Smith.Commands.Migrate;

public class MigrateInitCommand : AsyncCommand<ConnectionSettings>
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

        try
        {
            var factory = new NpgsqlConnectionFactory(config);
            await using var connection = await factory.CreateConnectionAsync();
            var tracker = new PostgresMigrationTracker(connection);
            await tracker.EnsureTableExistsAsync();
            renderer.Success("schema_migrations 表已就绪");
            return 0;
        }
        catch (Exception ex)
        {
            renderer.Error(ex.Message);
            return 1;
        }
    }
}
