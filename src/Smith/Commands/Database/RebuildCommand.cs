using Smith.Configuration;
using Smith.Database;
using Smith.Migration;
using Smith.Rendering;

namespace Smith.Commands.Database;

public static class RebuildCommand
{
    public static async Task<int> ExecuteAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose, bool seed, bool examples, bool force)
    {
        var config = ConfigLoader.Load(cliHost: host, cliPort: port, cliUser: user,
            cliPassword: password, cliDatabase: database, cliDatabasePath: databasePath,
            cliVerbose: verbose);
        
        if (string.IsNullOrEmpty(config.Database))
        {
            Console.Error.WriteLine("错误: 请通过 -d 参数或 SMITH_DATABASE 环境变量指定数据库名称");
            return 1;
        }

        SmithConfig.ValidateDatabaseName(config.Database);
        var renderer = new TerminalGuiRenderer();
        renderer.Title("Smith - 重建数据库");

        if (!force)
        {
            Console.Write($"即将删除并重建数据库 {config.Database}，确认继续? (y/N): ");
            var confirm = Console.ReadLine();
            if (confirm?.ToLower() != "y")
            {
                renderer.Warning("操作已取消");
                return 0;
            }
        }

        try
        {
            var factory = new NpgsqlConnectionFactory(config);

            renderer.Info($"断开 {config.Database} 的所有连接...");
            await using (var adminConn = await factory.CreateAdminConnectionAsync())
            {
                await TerminateConnectionsAsync(adminConn, config.Database);
                renderer.Info($"删除数据库 {config.Database}...");
                await DropDatabaseAsync(adminConn, config.Database);
                renderer.Info($"创建数据库 {config.Database}...");
                await CreateDatabaseAsync(adminConn, config.Database);
            }

            renderer.Success("数据库已重建");

            await using var connection = await factory.CreateConnectionAsync();
            var tracker = new PostgresMigrationTracker(connection);
            var runner = new MigrationRunner(connection, tracker, renderer);
            await runner.RunAsync(config.GetMigrationsPath());

            if (seed || examples)
            {
                if (seed)
                    await RunSeedFilesAsync(connection, config.GetSeedsPath("required"), renderer);
                if (examples)
                    await RunSeedFilesAsync(connection, config.GetSeedsPath("examples"), renderer);
            }

            renderer.NewLine();
            renderer.Success("数据库重建完成");
            return 0;
        }
        catch (Exception ex)
        {
            renderer.Error(ex.Message);
            if (config.Verbose)
                renderer.Error(ex.StackTrace ?? "");
            return 1;
        }
    }

    private static async Task TerminateConnectionsAsync(Npgsql.NpgsqlConnection adminConn, string database)
    {
        const string sql = "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = $1 AND pid <> pg_backend_pid()";
        await using var cmd = new Npgsql.NpgsqlCommand(sql, adminConn);
        cmd.Parameters.AddWithValue(database);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(Npgsql.NpgsqlConnection adminConn, string database)
    {
        var sql = $"DROP DATABASE IF EXISTS \"{database}\"";
        await using var cmd = new Npgsql.NpgsqlCommand(sql, adminConn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task CreateDatabaseAsync(Npgsql.NpgsqlConnection adminConn, string database)
    {
        var sql = $"CREATE DATABASE \"{database}\" ENCODING 'UTF8'";
        await using var cmd = new Npgsql.NpgsqlCommand(sql, adminConn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task RunSeedFilesAsync(Npgsql.NpgsqlConnection connection, string seedsPath, IConsoleRenderer renderer)
    {
        if (!Directory.Exists(seedsPath))
        {
            renderer.Warning($"种子数据目录不存在: {seedsPath}");
            return;
        }

        var files = Directory.GetFiles(seedsPath, "*.sql").OrderBy(f => f).ToList();
        if (files.Count == 0)
        {
            renderer.Info("没有种子数据文件");
            return;
        }

        renderer.Info($"执行 {files.Count} 个种子数据文件...");
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
