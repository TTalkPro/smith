using Smith.Migration;

namespace Smith.Commands.Seed;

/// <summary>
/// 种子数据命令：加载并执行指定类别的种子数据文件
/// </summary>
internal static class SeedHelper
{
    /// <summary>
    /// 执行指定类别的种子数据
    /// </summary>
    public static Task<int> RunSeedsAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose, string category, string label) =>
        CommandContext.RunAsync(database, host, port, user, password, databasePath, verbose,
            async ctx =>
            {
                ctx.Renderer.Title($"Smith - {label}");

                var connection = await ctx.GetConnectionAsync();
                var result = await SeedRunner.ExecuteAsync(
                    connection, ctx.Config.GetSeedsPath(category), label, ctx.Renderer);

                if (result < 0) return 1;
                if (result > 0) ctx.Renderer.Success($"{label}执行完成");
                return 0;
            });
}

/// <summary>执行必需种子数据（seeds/required/）</summary>
public static class SeedRequiredCommand
{
    public static Task<int> ExecuteAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose) =>
        SeedHelper.RunSeedsAsync(database, host, port, user, password,
            databasePath, verbose, "required", "必需种子数据");
}

/// <summary>执行示例数据（seeds/examples/）</summary>
public static class SeedExamplesCommand
{
    public static Task<int> ExecuteAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose) =>
        SeedHelper.RunSeedsAsync(database, host, port, user, password,
            databasePath, verbose, "examples", "示例数据");
}

/// <summary>执行所有种子数据（先必需，后示例）</summary>
public static class SeedAllCommand
{
    public static async Task<int> ExecuteAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose)
    {
        var result = await SeedHelper.RunSeedsAsync(database, host, port, user, password,
            databasePath, verbose, "required", "必需种子数据");
        if (result != 0) return result;
        return await SeedHelper.RunSeedsAsync(database, host, port, user, password,
            databasePath, verbose, "examples", "示例数据");
    }
}
