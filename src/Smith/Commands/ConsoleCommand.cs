using System.Diagnostics;
using Smith.Database;

namespace Smith.Commands;

/// <summary>
/// 打开数据库交互式终端（PostgreSQL: psql，SQLite: sqlite3）
/// </summary>
public static class ConsoleCommand
{
    public static Task<int> ExecuteAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose) =>
        CommandContext.RunAsync(database, host, port, user, password, databasePath, verbose,
            async ctx =>
            {
                var startInfo = BuildProcessStartInfo(ctx);
                var toolName = ctx.Config.Driver == DatabaseDriver.Sqlite ? "sqlite3" : "psql";

                var process = Process.Start(startInfo);
                if (process is null)
                {
                    ctx.Renderer.Error($"无法启动 {toolName}，请确认已安装");
                    return 1;
                }

                await process.WaitForExitAsync();
                return process.ExitCode;
            });

    /// <summary>
    /// 根据数据库类型构建对应的交互终端启动参数
    /// </summary>
    private static ProcessStartInfo BuildProcessStartInfo(CommandContext ctx)
    {
        if (ctx.Config.Driver == DatabaseDriver.Sqlite)
        {
            ctx.Renderer.Info($"连接到 SQLite: {ctx.Config.Database}...");
            return new ProcessStartInfo
            {
                FileName = "sqlite3",
                Arguments = ctx.Config.Database!,
                UseShellExecute = false,
            };
        }

        ctx.Renderer.Info($"连接到 {ctx.Config.Host}:{ctx.Config.Port}/{ctx.Config.Database}...");
        var startInfo = new ProcessStartInfo
        {
            FileName = "psql",
            Arguments = $"-h {ctx.Config.Host} -p {ctx.Config.Port} -U {ctx.Config.User} -d {ctx.Config.Database}",
            UseShellExecute = false,
        };

        if (!string.IsNullOrEmpty(ctx.Config.Password))
            startInfo.Environment["PGPASSWORD"] = ctx.Config.Password;

        return startInfo;
    }
}
