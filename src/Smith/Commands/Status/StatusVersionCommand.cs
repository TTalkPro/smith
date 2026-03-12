namespace Smith.Commands.Status;

/// <summary>
/// 输出当前数据库迁移版本号（纯数字，适合脚本使用）
/// </summary>
public static class StatusVersionCommand
{
    public static Task<int> ExecuteAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose) =>
        CommandContext.RunAsync(database, host, port, user, password, databasePath, verbose,
            async ctx =>
            {
                var connection = await ctx.GetConnectionAsync();
                var tracker = ctx.CreateTracker(connection);
                var version = await tracker.GetCurrentVersionAsync();
                Console.WriteLine(version);
                return 0;
            });
}
