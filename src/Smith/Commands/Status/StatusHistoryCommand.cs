namespace Smith.Commands.Status;

/// <summary>
/// 显示迁移执行历史记录
/// </summary>
public static class StatusHistoryCommand
{
    public static Task<int> ExecuteAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose, int limit = 20) =>
        CommandContext.RunAsync(database, host, port, user, password, databasePath, verbose,
            async ctx =>
            {
                ctx.Renderer.Title("Smith - 迁移历史");

                var connection = await ctx.GetConnectionAsync();
                var tracker = ctx.CreateTracker(connection);
                var history = await tracker.GetHistoryAsync(limit);

                if (history.Count == 0)
                {
                    ctx.Renderer.Info("暂无迁移记录");
                    return 0;
                }

                var rows = history.Select(r => new[]
                {
                    r.Version.ToString("D3"),
                    r.Description,
                    r.InstalledOn.ToString("yyyy-MM-dd HH:mm:ss"),
                    $"{r.ExecutionTimeMs}ms",
                    r.Success ? "✓" : "✗"
                }).ToList();

                ctx.Renderer.Table(["版本", "描述", "执行时间", "耗时", "状态"], rows);
                return 0;
            });
}
