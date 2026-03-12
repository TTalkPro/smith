using Smith.Migration;

namespace Smith.Commands.Status;

/// <summary>
/// 显示所有迁移文件的当前状态（已应用/待执行）
/// </summary>
public static class StatusShowCommand
{
    public static Task<int> ExecuteAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose) =>
        CommandContext.RunAsync(database, host, port, user, password, databasePath, verbose,
            async ctx =>
            {
                ctx.Renderer.Title("Smith - 迁移状态");

                var connection = await ctx.GetConnectionAsync();
                var tracker = ctx.CreateTracker(connection);
                var appliedVersions = await tracker.GetAppliedVersionsAsync();
                var allMigrations = MigrationFile.LoadFromDirectory(ctx.Config.GetMigrationsPath());

                var rows = allMigrations.Select(m => new[]
                {
                    m.Version.ToString("D3"),
                    m.Description,
                    appliedVersions.Contains(m.Version) ? "✓ Applied" : "○ Pending",
                    m.FileName
                }).ToList();

                ctx.Renderer.Table(["版本", "描述", "状态", "文件"], rows);
                ctx.Renderer.NewLine();
                ctx.Renderer.Info($"共 {allMigrations.Count} 个迁移，{appliedVersions.Count} 已应用，{allMigrations.Count - appliedVersions.Count} 待执行");
                return 0;
            });
}
