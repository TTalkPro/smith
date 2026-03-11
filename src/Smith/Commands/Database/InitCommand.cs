using Spectre.Console.Cli;
using Smith.Commands.Settings;
using Smith.Database;
using Smith.Migration;
using Smith.Rendering;

namespace Smith.Commands.Database;

public class InitCommand : AsyncCommand<ConnectionSettings>
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
        renderer.Title("Smith - 初始化数据库");

        try
        {
            var factory = new NpgsqlConnectionFactory(config);
            await using var connection = await factory.CreateConnectionAsync();

            var tracker = new PostgresMigrationTracker(connection);
            var runner = new MigrationRunner(connection, tracker, renderer);
            await runner.RunAsync(config.GetMigrationsPath());

            var seedsPath = config.GetSeedsPath("required");
            if (Directory.Exists(seedsPath))
            {
                var files = Directory.GetFiles(seedsPath, "*.sql").OrderBy(f => f).ToList();
                if (files.Count > 0)
                {
                    renderer.NewLine();
                    renderer.Info($"执行 {files.Count} 个必需种子数据...");
                    foreach (var file in files)
                    {
                        var sql = await File.ReadAllTextAsync(file);
                        await using var cmd = new Npgsql.NpgsqlCommand(sql, connection);
                        cmd.CommandTimeout = 120;
                        await cmd.ExecuteNonQueryAsync();
                        renderer.Success($"  {Path.GetFileName(file)}");
                    }
                }
            }

            renderer.NewLine();
            renderer.Success("数据库初始化完成");
            return 0;
        }
        catch (Exception ex)
        {
            renderer.Error(ex.Message);
            return 1;
        }
    }
}
