using Spectre.Console.Cli;
using Smith.Commands.Settings;
using Smith.Database;
using Smith.Migration;
using Smith.Rendering;

namespace Smith.Commands.Migrate;

public class MigrateUpCommand : AsyncCommand<MigrateUpCommand.Settings>
{
    public class Settings : ConnectionSettings
    {
        [CommandOption("-t|--target")]
        public int? Target { get; set; }

        [CommandOption("--dry-run")]
        public bool DryRun { get; set; }
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
        renderer.Title("Smith - 执行迁移");

        try
        {
            var factory = new NpgsqlConnectionFactory(config);
            await using var connection = await factory.CreateConnectionAsync();
            var tracker = new PostgresMigrationTracker(connection);
            var runner = new MigrationRunner(connection, tracker, renderer);

            var migrationsPath = config.GetMigrationsPath();
            var count = await runner.RunAsync(migrationsPath, settings.Target, settings.DryRun);
            return count >= 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            renderer.Error(ex.Message);
            if (config.Verbose)
                renderer.Error(ex.StackTrace ?? "");
            return 1;
        }
    }
}
