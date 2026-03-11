using System.Diagnostics;
using Smith.Configuration;
using Smith.Rendering;

namespace Smith.Commands;

public static class ConsoleCommand
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
        renderer.Info($"连接到 {config.Host}:{config.Port}/{config.Database}...");

        var startInfo = new ProcessStartInfo
        {
            FileName = "psql",
            Arguments = $"-h {config.Host} -p {config.Port} -U {config.User} -d {config.Database}",
            UseShellExecute = false,
        };

        if (!string.IsNullOrEmpty(config.Password))
            startInfo.Environment["PGPASSWORD"] = config.Password;

        var process = Process.Start(startInfo);
        if (process is null)
        {
            renderer.Error("无法启动 psql，请确认已安装 PostgreSQL 客户端");
            return 1;
        }

        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
