using Smith.Configuration;
using Smith.Database;
using Smith.Migration;

namespace Smith.Commands.Database;

/// <summary>
/// 重建数据库：删除现有数据库 → 创建新数据库 → 执行迁移 → 可选执行种子数据
/// </summary>
public static class RebuildCommand
{
    public static Task<int> ExecuteAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose, bool seed, bool examples, bool force) =>
        CommandContext.RunAsync(database, host, port, user, password, databasePath, verbose,
            async ctx =>
            {
                ctx.Renderer.Title("Smith - 重建数据库");

                if (!force && !ctx.Renderer.Confirm($"即将删除并重建数据库 {ctx.Config.Database}，确认继续?"))
                {
                    ctx.Renderer.Warning("操作已取消");
                    return 0;
                }

                await DropAndRecreateDatabase(ctx);
                ctx.Renderer.Success("数据库已重建");

                var connection = await ctx.GetConnectionAsync();
                var runner = ctx.CreateMigrationRunner(connection);
                await runner.RunAsync(ctx.Config.GetMigrationsPath());

                await RunOptionalSeeds(ctx, connection, seed, examples);

                ctx.Renderer.NewLine();
                ctx.Renderer.Success("数据库重建完成");
                return 0;
            });

    /// <summary>
    /// 删除并重建数据库（PostgreSQL 和 SQLite 使用不同策略）
    /// </summary>
    private static async Task DropAndRecreateDatabase(CommandContext ctx)
    {
        if (ctx.Config.Driver == DatabaseDriver.Sqlite)
        {
            DropSqliteDatabase(ctx);
            return;
        }

        await DropPostgresDatabase(ctx);
    }

    /// <summary>
    /// SQLite 重建：直接删除数据库文件
    /// </summary>
    private static void DropSqliteDatabase(CommandContext ctx)
    {
        if (!File.Exists(ctx.Config.Database!))
            return;

        File.Delete(ctx.Config.Database!);
        ctx.Renderer.Info($"已删除数据库文件: {ctx.Config.Database}");
    }

    /// <summary>
    /// PostgreSQL 重建：断开连接 → 删除数据库 → 创建数据库
    /// </summary>
    private static async Task DropPostgresDatabase(CommandContext ctx)
    {
        SmithConfig.ValidateDatabaseName(ctx.Config.Database!);
        var dbName = ctx.Config.Database!;

        await using var adminConn = await ctx.CreateAdminConnectionAsync();
        var pgConn = (Npgsql.NpgsqlConnection)adminConn;

        ctx.Renderer.Info($"断开 {dbName} 的所有连接...");
        await ExecutePgAdmin(pgConn,
            "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = $1 AND pid <> pg_backend_pid()",
            dbName);

        ctx.Renderer.Info($"删除数据库 {dbName}...");
        await ExecutePgDdl(pgConn, $"DROP DATABASE IF EXISTS \"{dbName}\"");

        ctx.Renderer.Info($"创建数据库 {dbName}...");
        await ExecutePgDdl(pgConn, $"CREATE DATABASE \"{dbName}\" ENCODING 'UTF8'");
    }

    /// <summary>
    /// 执行带参数的 PostgreSQL 管理命令
    /// </summary>
    private static async Task ExecutePgAdmin(Npgsql.NpgsqlConnection conn, string sql, string param)
    {
        await using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(param);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 执行 PostgreSQL DDL 语句（CREATE/DROP DATABASE 不支持参数化）
    /// </summary>
    private static async Task ExecutePgDdl(Npgsql.NpgsqlConnection conn, string sql)
    {
        await using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 按需执行种子数据
    /// </summary>
    private static async Task RunOptionalSeeds(CommandContext ctx, System.Data.Common.DbConnection connection, bool seed, bool examples)
    {
        if (seed)
            await SeedRunner.ExecuteAsync(connection, ctx.Config.GetSeedsPath("required"), "必需种子数据", ctx.Renderer);
        if (examples)
            await SeedRunner.ExecuteAsync(connection, ctx.Config.GetSeedsPath("examples"), "示例数据", ctx.Renderer);
    }
}
