using System.Diagnostics;
using Spectre.Console.Cli;
using Smith.Commands.Settings;
using Smith.Rendering;

namespace Smith.Commands;

public class ConsoleCommand : AsyncCommand<ConnectionSettings>
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
