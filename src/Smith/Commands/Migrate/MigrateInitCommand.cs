namespace Smith.Commands.Migrate;

/// <summary>
/// 初始化迁移系统，在数据库中创建 schema_migrations 跟踪表
/// </summary>
public static class MigrateInitCommand
{
    public static Task<int> ExecuteAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose) =>
        CommandContext.RunAsync(database, host, port, user, password, databasePath, verbose,
            async ctx =>
            {
                var connection = await ctx.GetConnectionAsync();
                var tracker = ctx.CreateTracker(connection);
                await tracker.EnsureTableExistsAsync();
                ctx.Renderer.Success("schema_migrations 表已就绪");
                return 0;
            });
}
