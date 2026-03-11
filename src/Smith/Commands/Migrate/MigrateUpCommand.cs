using Smith.Configuration;
using Smith.Database;
using Smith.Migration;
using Smith.Rendering;

namespace Smith.Commands.Migrate;

public static class MigrateUpCommand
{
    public static async Task<int> ExecuteAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose, int? target, bool dryRun, 
        bool migrationsOnly, bool seedsOnly)
    {
        if (migrationsOnly && seedsOnly)
        {
            Console.Error.WriteLine("错误: --migrations-only 和 --seeds-only 不能同时使用");
            return 1;
        }

        var config = ConfigLoader.Load(cliHost: host, cliPort: port, cliUser: user,
            cliPassword: password, cliDatabase: database, cliDatabasePath: databasePath,
            cliVerbose: verbose);
        
        if (string.IsNullOrEmpty(config.Database))
        {
            Console.Error.WriteLine("错误: 请通过 -d 参数或 SMITH_DATABASE 环境变量指定数据库名称");
            return 1;
        }

        var renderer = new TerminalGuiRenderer();
        
        ScriptType? scriptType = null;
        if (migrationsOnly)
        {
            scriptType = ScriptType.Migration;
            renderer.Title("Smith - 执行迁移 (仅 Migration)");
        }
        else if (seedsOnly)
        {
            scriptType = ScriptType.SeedRequired;
            renderer.Title("Smith - 执行种子数据 (仅 Seeds)");
        }
        else
        {
            renderer.Title("Smith - 执行迁移和种子数据");
        }

        try
        {
            var factory = new NpgsqlConnectionFactory(config);
            await using var connection = await factory.CreateConnectionAsync();
            var tracker = new PostgresMigrationTracker(connection);
            var runner = new MigrationRunner(connection, tracker, renderer);

            var migrationsPath = config.GetMigrationsPath();
            var count = await runner.RunAsync(migrationsPath, target, dryRun, scriptType);
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
