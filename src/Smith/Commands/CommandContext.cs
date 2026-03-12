using System.Data.Common;
using Smith.Configuration;
using Smith.Database;
using Smith.Migration;
using Smith.Rendering;

namespace Smith.Commands;

/// <summary>
/// 命令执行上下文，封装配置加载、数据库连接生命周期和服务创建。
/// 提供统一的参数验证、错误处理和资源释放。
/// </summary>
public sealed class CommandContext : IAsyncDisposable
{
    /// <summary>已加载的配置</summary>
    public SmithConfig Config { get; }

    /// <summary>控制台渲染器</summary>
    public IConsoleRenderer Renderer { get; }

    private DbConnection? _connection;
    private IConnectionFactory? _factory;

    private CommandContext(SmithConfig config, IConsoleRenderer renderer)
    {
        Config = config;
        Renderer = renderer;
    }

    /// <summary>
    /// 获取数据库连接（懒加载，首次调用时创建并缓存）
    /// </summary>
    public async Task<DbConnection> GetConnectionAsync(CancellationToken ct = default)
    {
        if (_connection != null)
            return _connection;

        _factory ??= DbServices.CreateConnectionFactory(Config);
        _connection = await _factory.CreateConnectionAsync(ct);
        return _connection;
    }

    /// <summary>
    /// 创建管理连接（PostgreSQL 连接到 postgres 库，SQLite 返回同一连接）。
    /// 注意：返回的连接由调用方负责释放。
    /// </summary>
    public async Task<DbConnection> CreateAdminConnectionAsync(CancellationToken ct = default)
    {
        _factory ??= DbServices.CreateConnectionFactory(Config);
        return await _factory.CreateAdminConnectionAsync(ct);
    }

    /// <summary>创建迁移追踪器</summary>
    public IMigrationTracker CreateTracker(DbConnection connection) =>
        DbServices.CreateTracker(connection, Config);

    /// <summary>创建 Schema 版本检测器</summary>
    public ISchemaDetector CreateSchemaDetector(DbConnection connection) =>
        DbServices.CreateSchemaDetector(connection, Config);

    /// <summary>创建 Schema 对象检查器</summary>
    public ISchemaInspector CreateSchemaInspector(DbConnection connection) =>
        DbServices.CreateInspector(connection, Config);

    /// <summary>创建迁移执行器（包含追踪器和 Schema 检测器）</summary>
    public MigrationRunner CreateMigrationRunner(DbConnection connection) =>
        new(connection, CreateTracker(connection), CreateSchemaDetector(connection), Renderer);

    /// <summary>
    /// 命令统一执行入口：加载配置 → 验证参数 → 执行逻辑 → 处理异常 → 释放资源
    /// </summary>
    public static async Task<int> RunAsync(
        string? database, string? host, int? port, string? user, string? password,
        string? databasePath, bool verbose,
        Func<CommandContext, Task<int>> action)
    {
        var config = ConfigLoader.Load(
            cliHost: host, cliPort: port, cliUser: user,
            cliPassword: password, cliDatabase: database,
            cliDatabasePath: databasePath, cliVerbose: verbose);

        if (string.IsNullOrEmpty(config.Database))
        {
            Console.Error.WriteLine("错误: 请通过 -d 参数或 SMITH_DATABASE 环境变量指定数据库名称");
            return 1;
        }

        await using var ctx = new CommandContext(config, new TerminalGuiRenderer());
        try
        {
            return await action(ctx);
        }
        catch (Exception ex)
        {
            ctx.Renderer.Error(ex.Message);
            if (config.Verbose)
                ctx.Renderer.Error(ex.StackTrace ?? "");
            return 1;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
            await _connection.DisposeAsync();
    }
}
