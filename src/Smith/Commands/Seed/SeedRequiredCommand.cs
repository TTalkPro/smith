using Smith.Configuration;
using Smith.Database;
using Smith.Migration;
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

            // 检查不符合命名规范的文件
            var allSqlFiles = Directory.GetFiles(seedsPath, "*.sql");
            var seedFiles = MigrationFile.LoadFromDirectory(seedsPath)
                .Where(f => f.Type == ScriptType.SeedRequired)
                .ToList();

            var invalidFiles = allSqlFiles
                .Where(f => seedFiles.All(s => s.FilePath != f))
                .Select(Path.GetFileName)
                .ToList();

            if (invalidFiles.Count > 0)
            {
                renderer.Error("以下文件不符合命名规范 (应为 Sxxx_name.sql，如 S001_roles.sql):");
                foreach (var file in invalidFiles)
                    renderer.Warning($"  {file}");
                return 1;
            }

            if (seedFiles.Count == 0)
            {
                renderer.Info($"没有{label}文件");
                return 0;
            }

            var factory = new NpgsqlConnectionFactory(config);
            await using var connection = await factory.CreateConnectionAsync();

            renderer.Info($"执行 {seedFiles.Count} 个{label}文件...");
            foreach (var seed in seedFiles)
            {
                var sql = seed.GetContent();
                await using var cmd = new Npgsql.NpgsqlCommand(sql, connection);
                cmd.CommandTimeout = 120;
                await cmd.ExecuteNonQueryAsync();
                renderer.Success($"  {seed.FileName}");
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
