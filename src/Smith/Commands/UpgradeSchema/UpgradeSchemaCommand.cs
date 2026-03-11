using Smith.Configuration;
using Smith.Database;
using Smith.Migration;
using Smith.Rendering;

namespace Smith.Commands.UpgradeSchema;

public static class UpgradeSchemaStatusCommand
{
    public static async Task<int> ExecuteAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose)
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
        renderer.Title("Smith - Schema 版本状态");

        try
        {
            var factory = new NpgsqlConnectionFactory(config);
            await using var connection = await factory.CreateConnectionAsync();

            var detector = new SchemaDetector(connection);
            var currentVersion = await detector.DetectCurrentVersionAsync();
            var needsUpgrade = await detector.NeedsUpgradeAsync();

            renderer.Info($"当前 Schema 版本: {currentVersion}");
            renderer.Info($"最新 Schema 版本: {SchemaDefinitions.LatestVersion}");

            if (!needsUpgrade)
            {
                renderer.Success("Schema 已是最新版本");
                return 0;
            }

            var diff = await detector.GetDiffAsync(SchemaDefinitions.LatestVersion);
            renderer.NewLine();
            renderer.Warning("需要升级:");
            foreach (var change in diff.Changes)
            {
                renderer.Info($"  - {change}");
            }

            renderer.NewLine();
            renderer.Info("运行以下命令进行升级:");
            renderer.Info("  smith upgrade-schema run -d <database>");

            return 0;
        }
        catch (Exception ex)
        {
            renderer.Error(ex.Message);
            return 1;
        }
    }
}

public static class UpgradeSchemaRunCommand
{
    public static async Task<int> ExecuteAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose, bool dryRun, bool force)
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
        renderer.Title("Smith - 升级 Schema");

        try
        {
            var factory = new NpgsqlConnectionFactory(config);
            await using var connection = await factory.CreateConnectionAsync();

            var detector = new SchemaDetector(connection);
            var currentVersion = await detector.DetectCurrentVersionAsync();

            if (currentVersion == SchemaDefinitions.LatestVersion)
            {
                renderer.Success("Schema 已是最新版本，无需升级");
                return 0;
            }

            if (!force && !dryRun)
            {
                var diff = await detector.GetDiffAsync(SchemaDefinitions.LatestVersion);
                renderer.Warning("即将执行以下变更:");
                foreach (var change in diff.Changes)
                {
                    renderer.Info($"  - {change}");
                }

                Console.Write("确认升级? (y/N): ");
                var confirm = Console.ReadLine();
                if (confirm?.ToLower() != "y")
                {
                    renderer.Warning("操作已取消");
                    return 0;
                }
            }

            var upgrader = new SchemaUpgrader(connection, renderer);
            var result = await upgrader.UpgradeAsync(
                currentVersion,
                SchemaDefinitions.LatestVersion,
                dryRun);

            return result.Success ? 0 : 1;
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
