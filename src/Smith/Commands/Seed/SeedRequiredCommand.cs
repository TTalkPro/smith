using Smith.Configuration;
using Smith.Database;
using Smith.Rendering;

namespace Smith.Commands.Seed;

internal static class SeedHelper
{
    public static async Task<int> RunSeedsAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose, string category, string label)
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

public static class SeedRequiredCommand
{
    public static async Task<int> ExecuteAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose)
    {
        return await SeedHelper.RunSeedsAsync(database, host, port, user, password, databasePath, verbose, "required", "必需种子数据");
    }
}

public static class SeedExamplesCommand
{
    public static async Task<int> ExecuteAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose)
    {
        return await SeedHelper.RunSeedsAsync(database, host, port, user, password, databasePath, verbose, "examples", "示例数据");
    }
}

public static class SeedAllCommand
{
    public static async Task<int> ExecuteAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose)
    {
        var result = await SeedHelper.RunSeedsAsync(database, host, port, user, password, databasePath, verbose, "required", "必需种子数据");
        if (result != 0) return result;
        return await SeedHelper.RunSeedsAsync(database, host, port, user, password, databasePath, verbose, "examples", "示例数据");
    }
}
