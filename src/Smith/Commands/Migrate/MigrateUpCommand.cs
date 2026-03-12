using Smith.Migration;

namespace Smith.Commands.Migrate;

/// <summary>
/// 执行待处理的迁移和/或种子数据脚本
/// </summary>
public static class MigrateUpCommand
{
    public static Task<int> ExecuteAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose, int? target, bool dryRun,
        bool migrationsOnly, bool seedsOnly)
    {
        if (migrationsOnly && seedsOnly)
        {
            Console.Error.WriteLine("错误: --migrations-only 和 --seeds-only 不能同时使用");
            return Task.FromResult(1);
        }

        return CommandContext.RunAsync(database, host, port, user, password, databasePath, verbose,
            async ctx =>
            {
                var scriptType = DetermineScriptType(migrationsOnly, seedsOnly);
                ctx.Renderer.Title($"Smith - {GetTitle(scriptType)}");

                var connection = await ctx.GetConnectionAsync();
                var runner = ctx.CreateMigrationRunner(connection);
                var count = await runner.RunAsync(ctx.Config.GetMigrationsPath(), target, dryRun, scriptType);
                return count >= 0 ? 0 : 1;
            });
    }

    /// <summary>
    /// 根据命令行标志确定要执行的脚本类型
    /// </summary>
    private static ScriptType? DetermineScriptType(bool migrationsOnly, bool seedsOnly) =>
        migrationsOnly ? ScriptType.Migration :
        seedsOnly ? ScriptType.SeedRequired :
        null;

    /// <summary>
    /// 根据脚本类型生成命令标题
    /// </summary>
    private static string GetTitle(ScriptType? scriptType) => scriptType switch
    {
        ScriptType.Migration => "执行迁移 (仅 Migration)",
        ScriptType.SeedRequired => "执行种子数据 (仅 Seeds)",
        _ => "执行迁移和种子数据"
    };
}
