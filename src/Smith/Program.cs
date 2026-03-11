using Spectre.Console.Cli;
using Smith.Commands;
using Smith.Commands.Database;
using Smith.Commands.Migrate;
using Smith.Commands.Seed;
using Smith.Commands.Status;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("smith");
    config.SetApplicationVersion("0.1.0");

    config.AddBranch("migrate", migrate =>
    {
        migrate.AddCommand<MigrateInitCommand>("init")
            .WithDescription("创建 schema_migrations 表");
        
        migrate.AddCommand<MigrateUpCommand>("up")
            .WithDescription("执行待处理的迁移");
    });

    config.AddBranch("status", status =>
    {
        status.AddCommand<StatusShowCommand>("show")
            .WithDescription("显示所有迁移状态");
        
        status.AddCommand<StatusHistoryCommand>("history")
            .WithDescription("执行历史");
        
        status.AddCommand<StatusVersionCommand>("version")
            .WithDescription("当前版本号");
        
        status.AddCommand<StatusSyncCommand>("sync")
            .WithDescription("同步未记录的迁移");
    });

    config.AddBranch("database", database =>
    {
        database.AddCommand<RebuildCommand>("rebuild")
            .WithDescription("重建数据库");
        
        database.AddCommand<InitCommand>("init")
            .WithDescription("初始化数据库（迁移 + 必需种子数据）");
    });

    config.AddBranch("seed", seed =>
    {
        seed.AddCommand<SeedRequiredCommand>("required")
            .WithDescription("执行必需种子数据");
        
        seed.AddCommand<SeedExamplesCommand>("examples")
            .WithDescription("执行示例数据");
        
        seed.AddCommand<SeedAllCommand>("all")
            .WithDescription("执行所有种子数据");
    });

    config.AddCommand<ConsoleCommand>("console")
        .WithDescription("打开 psql 交互式终端");
});

return await app.RunAsync(args);
