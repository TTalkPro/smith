using Smith.Migration;

namespace Smith.Commands.Database;

/// <summary>
/// 初始化数据库：执行全部迁移，然后执行必需种子数据
/// </summary>
public static class InitCommand
{
    public static Task<int> ExecuteAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose) =>
        CommandContext.RunAsync(database, host, port, user, password, databasePath, verbose,
            async ctx =>
            {
                ctx.Renderer.Title("Smith - 初始化数据库");

                var connection = await ctx.GetConnectionAsync();
                var runner = ctx.CreateMigrationRunner(connection);
                await runner.RunAsync(ctx.Config.GetMigrationsPath());

                ctx.Renderer.NewLine();
                await SeedRunner.ExecuteAsync(
                    connection, ctx.Config.GetSeedsPath("required"),
                    "必需种子数据", ctx.Renderer);

                ctx.Renderer.NewLine();
                ctx.Renderer.Success("数据库初始化完成");
                return 0;
            });
}
