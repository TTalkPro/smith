using Smith.Migration;

namespace Smith.Commands.UpgradeSchema;

/// <summary>
/// 查看 schema_migrations 表的 Schema 版本状态
/// </summary>
public static class UpgradeSchemaStatusCommand
{
    public static Task<int> ExecuteAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose) =>
        CommandContext.RunAsync(database, host, port, user, password, databasePath, verbose,
            async ctx =>
            {
                ctx.Renderer.Title("Smith - Schema 版本状态");

                var connection = await ctx.GetConnectionAsync();
                var detector = ctx.CreateSchemaDetector(connection);
                var currentVersion = await detector.DetectCurrentVersionAsync();
                var needsUpgrade = await detector.NeedsUpgradeAsync();

                ctx.Renderer.Info($"当前 Schema 版本: {currentVersion}");
                ctx.Renderer.Info($"最新 Schema 版本: {SchemaDefinitions.LatestVersion}");

                if (!needsUpgrade)
                {
                    ctx.Renderer.Success("Schema 已是最新版本");
                    return 0;
                }

                var diff = await detector.GetDiffAsync(SchemaDefinitions.LatestVersion);
                ctx.Renderer.NewLine();
                ctx.Renderer.Warning("需要升级:");
                foreach (var change in diff.Changes)
                    ctx.Renderer.Info($"  - {change}");

                ctx.Renderer.NewLine();
                ctx.Renderer.Info("运行以下命令进行升级:");
                ctx.Renderer.Info("  smith upgrade-schema run -d <database>");
                return 0;
            });
}

/// <summary>
/// 执行 schema_migrations 表的 Schema 升级
/// </summary>
public static class UpgradeSchemaRunCommand
{
    public static Task<int> ExecuteAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose, bool dryRun, bool force) =>
        CommandContext.RunAsync(database, host, port, user, password, databasePath, verbose,
            async ctx =>
            {
                ctx.Renderer.Title("Smith - 升级 Schema");

                var connection = await ctx.GetConnectionAsync();
                var detector = ctx.CreateSchemaDetector(connection);
                var currentVersion = await detector.DetectCurrentVersionAsync();

                if (currentVersion == SchemaDefinitions.LatestVersion)
                {
                    ctx.Renderer.Success("Schema 已是最新版本，无需升级");
                    return 0;
                }

                if (!force && !dryRun && !ConfirmUpgrade(ctx, detector, SchemaDefinitions.LatestVersion).Result)
                    return 0;

                var upgrader = new SchemaUpgrader(connection, ctx.Renderer);
                var result = await upgrader.UpgradeAsync(currentVersion, SchemaDefinitions.LatestVersion, dryRun);
                return result.Success ? 0 : 1;
            });

    /// <summary>
    /// 显示变更详情并请求用户确认
    /// </summary>
    private static async Task<bool> ConfirmUpgrade(
        CommandContext ctx, ISchemaDetector detector, SchemaVersion targetVersion)
    {
        var diff = await detector.GetDiffAsync(targetVersion);
        ctx.Renderer.Warning("即将执行以下变更:");
        foreach (var change in diff.Changes)
            ctx.Renderer.Info($"  - {change}");

        if (!ctx.Renderer.Confirm("确认升级?"))
        {
            ctx.Renderer.Warning("操作已取消");
            return false;
        }
        return true;
    }
}
