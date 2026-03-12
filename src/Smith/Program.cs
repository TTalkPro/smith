using System.CommandLine;
using System.CommandLine.Parsing;
using Smith.Commands;
using Smith.Commands.Database;
using Smith.Commands.Migrate;
using Smith.Commands.Seed;
using Smith.Commands.Status;
using Smith.Commands.UpgradeSchema;

// ── 通用数据库连接选项 ──
var databaseOption = new Option<string>("--database", "-d") { Description = "数据库名称" };
var hostOption = new Option<string>("--host", "-H") { Description = "数据库主机" };
var portOption = new Option<int?>("--port", "-P") { Description = "数据库端口" };
var userOption = new Option<string>("--user", "-u") { Description = "数据库用户" };
var passwordOption = new Option<string>("--password", "-p") { Description = "数据库密码" };
var databasePathOption = new Option<string>("--database-path", "-D") { Description = "数据库脚本目录" };
var verboseOption = new Option<bool>("--verbose", "-v") { Description = "详细输出" };

// ── 辅助方法：为命令批量添加通用选项 ──
void AddCommonOptions(Command command)
{
    command.Options.Add(databaseOption);
    command.Options.Add(hostOption);
    command.Options.Add(portOption);
    command.Options.Add(userOption);
    command.Options.Add(passwordOption);
    command.Options.Add(databasePathOption);
    command.Options.Add(verboseOption);
}

// ── 辅助方法：从解析结果中提取通用选项值 ──
(string? database, string? host, int? port, string? user, string? password, string? databasePath, bool verbose)
    CommonValues(ParseResult r) =>
    (r.GetValue(databaseOption), r.GetValue(hostOption), r.GetValue(portOption),
     r.GetValue(userOption), r.GetValue(passwordOption), r.GetValue(databasePathOption),
     r.GetValue(verboseOption));

// ── 辅助方法：构建仅含通用选项的命令 ──
Command BuildCommand(string name, string description,
    Func<string?, string?, int?, string?, string?, string?, bool, Task<int>> handler)
{
    var command = new Command(name, description);
    AddCommonOptions(command);
    command.SetAction(async parseResult =>
    {
        var (db, host, port, user, pwd, path, verbose) = CommonValues(parseResult);
        return await handler(db, host, port, user, pwd, path, verbose);
    });
    return command;
}

// ── 根命令 ──
var rootCommand = new RootCommand("Smith - 数据库迁移管理工具 (PostgreSQL / SQLite)");
rootCommand.SetAction(_ =>
{
    Console.WriteLine("Smith 0.1.0 - 数据库迁移管理工具 (PostgreSQL / SQLite)");
    Console.WriteLine("使用 --help 查看可用命令");
    return Task.FromResult(0);
});

// ── migrate 子命令组 ──
var migrateCommand = new Command("migrate", "迁移管理");
migrateCommand.Subcommands.Add(BuildCommand("init", "初始化迁移系统", MigrateInitCommand.ExecuteAsync));

var targetOption = new Option<int?>("--target", "-t") { Description = "目标版本号" };
var dryRunOption = new Option<bool>("--dry-run") { Description = "预览模式，不实际执行" };
var migrationsOnlyOption = new Option<bool>("--migrations-only") { Description = "仅执行迁移" };
var seedsOnlyOption = new Option<bool>("--seeds-only") { Description = "仅执行种子数据" };

var migrateUpCommand = new Command("up", "执行待处理的迁移");
AddCommonOptions(migrateUpCommand);
migrateUpCommand.Options.Add(targetOption);
migrateUpCommand.Options.Add(dryRunOption);
migrateUpCommand.Options.Add(migrationsOnlyOption);
migrateUpCommand.Options.Add(seedsOnlyOption);
migrateUpCommand.SetAction(async parseResult =>
{
    var (db, host, port, user, pwd, path, verbose) = CommonValues(parseResult);
    return await MigrateUpCommand.ExecuteAsync(db, host, port, user, pwd, path, verbose,
        parseResult.GetValue(targetOption), parseResult.GetValue(dryRunOption),
        parseResult.GetValue(migrationsOnlyOption), parseResult.GetValue(seedsOnlyOption));
});
migrateCommand.Subcommands.Add(migrateUpCommand);

// ── status 子命令组 ──
var statusCommand = new Command("status", "迁移状态");
statusCommand.Subcommands.Add(BuildCommand("show", "显示所有迁移状态", StatusShowCommand.ExecuteAsync));
statusCommand.Subcommands.Add(BuildCommand("version", "获取当前版本号", StatusVersionCommand.ExecuteAsync));

var limitOption = new Option<int>("--limit", "-n") { Description = "返回记录数", DefaultValueFactory = _ => 20 };
var statusHistoryCommand = new Command("history", "执行历史");
AddCommonOptions(statusHistoryCommand);
statusHistoryCommand.Options.Add(limitOption);
statusHistoryCommand.SetAction(async parseResult =>
{
    var (db, host, port, user, pwd, path, verbose) = CommonValues(parseResult);
    return await StatusHistoryCommand.ExecuteAsync(db, host, port, user, pwd, path, verbose,
        parseResult.GetValue(limitOption));
});
statusCommand.Subcommands.Add(statusHistoryCommand);

var syncDryRunOption = new Option<bool>("--dry-run") { Description = "预览模式" };
var statusSyncCommand = new Command("sync", "同步未记录的迁移");
AddCommonOptions(statusSyncCommand);
statusSyncCommand.Options.Add(syncDryRunOption);
statusSyncCommand.SetAction(async parseResult =>
{
    var (db, host, port, user, pwd, path, verbose) = CommonValues(parseResult);
    return await StatusSyncCommand.ExecuteAsync(db, host, port, user, pwd, path, verbose,
        parseResult.GetValue(syncDryRunOption));
});
statusCommand.Subcommands.Add(statusSyncCommand);

// ── database 子命令组 ──
var databaseCommand = new Command("database", "数据库管理");
databaseCommand.Subcommands.Add(BuildCommand("init", "初始化数据库（迁移+种子）", InitCommand.ExecuteAsync));

var rebuildSeedOption = new Option<bool>("--seed", "-s") { Description = "执行种子数据" };
var rebuildExamplesOption = new Option<bool>("--examples", "-e") { Description = "执行示例数据" };
var rebuildForceOption = new Option<bool>("--force", "-f") { Description = "强制执行，不确认" };
var databaseRebuildCommand = new Command("rebuild", "重建数据库");
AddCommonOptions(databaseRebuildCommand);
databaseRebuildCommand.Options.Add(rebuildSeedOption);
databaseRebuildCommand.Options.Add(rebuildExamplesOption);
databaseRebuildCommand.Options.Add(rebuildForceOption);
databaseRebuildCommand.SetAction(async parseResult =>
{
    var (db, host, port, user, pwd, path, verbose) = CommonValues(parseResult);
    return await RebuildCommand.ExecuteAsync(db, host, port, user, pwd, path, verbose,
        parseResult.GetValue(rebuildSeedOption), parseResult.GetValue(rebuildExamplesOption),
        parseResult.GetValue(rebuildForceOption));
});
databaseCommand.Subcommands.Add(databaseRebuildCommand);

// ── seed 子命令组 ──
var seedCommand = new Command("seed", "种子数据管理");
seedCommand.Subcommands.Add(BuildCommand("required", "执行必需种子数据", SeedRequiredCommand.ExecuteAsync));
seedCommand.Subcommands.Add(BuildCommand("examples", "执行示例数据", SeedExamplesCommand.ExecuteAsync));
seedCommand.Subcommands.Add(BuildCommand("all", "执行所有种子数据", SeedAllCommand.ExecuteAsync));

// ── upgrade-schema 子命令组 ──
var upgradeSchemaCommand = new Command("upgrade-schema", "Schema 升级管理");
upgradeSchemaCommand.Subcommands.Add(BuildCommand("status", "查看 Schema 版本状态", UpgradeSchemaStatusCommand.ExecuteAsync));

var upgradeSchemaDryRunOption = new Option<bool>("--dry-run") { Description = "预览模式" };
var upgradeSchemaForceOption = new Option<bool>("--force", "-f") { Description = "强制执行" };
var upgradeSchemaRunCommand = new Command("run", "执行 Schema 升级");
AddCommonOptions(upgradeSchemaRunCommand);
upgradeSchemaRunCommand.Options.Add(upgradeSchemaDryRunOption);
upgradeSchemaRunCommand.Options.Add(upgradeSchemaForceOption);
upgradeSchemaRunCommand.SetAction(async parseResult =>
{
    var (db, host, port, user, pwd, path, verbose) = CommonValues(parseResult);
    return await UpgradeSchemaRunCommand.ExecuteAsync(db, host, port, user, pwd, path, verbose,
        parseResult.GetValue(upgradeSchemaDryRunOption), parseResult.GetValue(upgradeSchemaForceOption));
});
upgradeSchemaCommand.Subcommands.Add(upgradeSchemaRunCommand);

// ── console 命令 ──
rootCommand.Subcommands.Add(migrateCommand);
rootCommand.Subcommands.Add(statusCommand);
rootCommand.Subcommands.Add(databaseCommand);
rootCommand.Subcommands.Add(seedCommand);
rootCommand.Subcommands.Add(upgradeSchemaCommand);
rootCommand.Subcommands.Add(BuildCommand("console", "打开数据库交互式终端 (psql/sqlite3)", ConsoleCommand.ExecuteAsync));

return await rootCommand.Parse(args).InvokeAsync();
