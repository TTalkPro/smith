using Spectre.Console.Cli;
using Smith.Commands.Settings;
using Smith.Database;
using Smith.Rendering;

namespace Smith.Commands.Seed;

public class SeedRequiredCommand : AsyncCommand<ConnectionSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, ConnectionSettings settings) =>
        SeedHelper.RunSeedsAsync(settings, "required", "必需种子数据");
}

public class SeedExamplesCommand : AsyncCommand<ConnectionSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, ConnectionSettings settings) =>
        SeedHelper.RunSeedsAsync(settings, "examples", "示例数据");
}

public class SeedAllCommand : AsyncCommand<ConnectionSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ConnectionSettings settings)
    {
        var result = await SeedHelper.RunSeedsAsync(settings, "required", "必需种子数据");
        if (result != 0) return result;
        return await SeedHelper.RunSeedsAsync(settings, "examples", "示例数据");
    }
}

internal static class SeedHelper
{
    public static async Task<int> RunSeedsAsync(ConnectionSettings settings, string category, string label)
    {
        var config = settings.BuildConfig();
        if (string.IsNullOrEmpty(config.Database))
        {
            Console.Error.WriteLine("错误: 请通过 -d 参数或 SMITH_DATABASE 环境变量指定数据库名称");
            return 1;
        }

        var renderer = new SpectreRenderer();
        renderer.Title($"Smith - {label}");

        try
        {
            var seedsPath = config.GetSeedsPath(category);
            if (!Directory.Exists(seedsPath))
            {
                renderer.Warning($"目录不存在: {seedsPath}");
                return 0;
            }

            var files = Directory.GetFiles(seedsPath, "*.sql").OrderBy(f => f).ToList();
            if (files.Count == 0)
            {
                renderer.Info($"没有{label}文件");
                return 0;
            }

            var factory = new NpgsqlConnectionFactory(config);
            await using var connection = await factory.CreateConnectionAsync();

            renderer.Info($"执行 {files.Count} 个{label}文件...");
            foreach (var file in files)
            {
                var sql = await File.ReadAllTextAsync(file);
                await using var cmd = new Npgsql.NpgsqlCommand(sql, connection);
                cmd.CommandTimeout = 120;
                await cmd.ExecuteNonQueryAsync();
                renderer.Success($"  {Path.GetFileName(file)}");
            }

            renderer.Success($"{label}执行完成");
            return 0;
        }
        catch (Exception ex)
        {
            renderer.Error(ex.Message);
            return 1;
        }
    }
}
