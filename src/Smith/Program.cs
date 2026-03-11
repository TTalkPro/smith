using System.CommandLine;
using Smith.Commands;
using Smith.Commands.Database;
using Smith.Commands.Migrate;
using Smith.Commands.Seed;
using Smith.Commands.Status;
using Smith.Commands.UpgradeSchema;

var databaseOption = new Option<string>("--database", "-d")
{
    Description = "数据库名称"
};
var hostOption = new Option<string>("--host", "-H")
{
    Description = "数据库主机"
};
var portOption = new Option<int>("--port", "-P")
{
    Description = "数据库端口"
};
var userOption = new Option<string>("--user", "-u")
{
    Description = "数据库用户"
};
var passwordOption = new Option<string>("--password", "-p")
{
    Description = "数据库密码"
};
var databasePathOption = new Option<string>("--database-path", "-D")
{
    Description = "数据库脚本目录"
};
var verboseOption = new Option<bool>("--verbose", "-v")
{
    Description = "详细输出"
};

var rootCommand = new RootCommand("Smith - PostgreSQL 数据库迁移管理工具");

var migrateCommand = new Command("migrate", "迁移管理");

var migrateInitCommand = new Command("init", "初始化迁移系统");
migrateInitCommand.Options.Add(databaseOption);
migrateInitCommand.Options.Add(hostOption);
migrateInitCommand.Options.Add(portOption);
migrateInitCommand.Options.Add(userOption);
migrateInitCommand.Options.Add(passwordOption);
migrateInitCommand.Options.Add(databasePathOption);
migrateInitCommand.Options.Add(verboseOption);
migrateInitCommand.SetAction(async parseResult =>
{
    return await MigrateInitCommand.ExecuteAsync(
        parseResult.GetValue(databaseOption),
        parseResult.GetValue(hostOption),
        parseResult.GetValue(portOption),
        parseResult.GetValue(userOption),
        parseResult.GetValue(passwordOption),
        parseResult.GetValue(databasePathOption),
        parseResult.GetValue(verboseOption));
});

var targetOption = new Option<int>("--target", "-t")
{
    Description = "目标版本号"
};
var dryRunOption = new Option<bool>("--dry-run")
{
    Description = "预览模式，不实际执行"
};
var migrationsOnlyOption = new Option<bool>("--migrations-only")
{
    Description = "仅执行迁移"
};
var seedsOnlyOption = new Option<bool>("--seeds-only")
{
    Description = "仅执行种子数据"
};

var migrateUpCommand = new Command("up", "执行待处理的迁移");
migrateUpCommand.Options.Add(databaseOption);
migrateUpCommand.Options.Add(hostOption);
migrateUpCommand.Options.Add(portOption);
migrateUpCommand.Options.Add(userOption);
migrateUpCommand.Options.Add(passwordOption);
migrateUpCommand.Options.Add(databasePathOption);
migrateUpCommand.Options.Add(verboseOption);
migrateUpCommand.Options.Add(targetOption);
migrateUpCommand.Options.Add(dryRunOption);
migrateUpCommand.Options.Add(migrationsOnlyOption);
migrateUpCommand.Options.Add(seedsOnlyOption);
migrateUpCommand.SetAction(async parseResult =>
{
    return await MigrateUpCommand.ExecuteAsync(
        parseResult.GetValue(databaseOption),
        parseResult.GetValue(hostOption),
        parseResult.GetValue(portOption),
        parseResult.GetValue(userOption),
        parseResult.GetValue(passwordOption),
        parseResult.GetValue(databasePathOption),
        parseResult.GetValue(verboseOption),
        parseResult.GetValue(targetOption),
        parseResult.GetValue(dryRunOption),
        parseResult.GetValue(migrationsOnlyOption),
        parseResult.GetValue(seedsOnlyOption));
});

migrateCommand.Subcommands.Add(migrateInitCommand);
migrateCommand.Subcommands.Add(migrateUpCommand);

var statusCommand = new Command("status", "迁移状态");

var statusShowCommand = new Command("show", "显示所有迁移状态");
statusShowCommand.Options.Add(databaseOption);
statusShowCommand.Options.Add(hostOption);
statusShowCommand.Options.Add(portOption);
statusShowCommand.Options.Add(userOption);
statusShowCommand.Options.Add(passwordOption);
statusShowCommand.Options.Add(databasePathOption);
statusShowCommand.Options.Add(verboseOption);
statusShowCommand.SetAction(async parseResult =>
{
    return await StatusShowCommand.ExecuteAsync(
        parseResult.GetValue(databaseOption),
        parseResult.GetValue(hostOption),
        parseResult.GetValue(portOption),
        parseResult.GetValue(userOption),
        parseResult.GetValue(passwordOption),
        parseResult.GetValue(databasePathOption),
        parseResult.GetValue(verboseOption));
});

var limitOption = new Option<int>("--limit", "-n")
{
    Description = "返回记录数",
    DefaultValueFactory = _ => 20
};

var statusHistoryCommand = new Command("history", "执行历史");
statusHistoryCommand.Options.Add(databaseOption);
statusHistoryCommand.Options.Add(hostOption);
statusHistoryCommand.Options.Add(portOption);
statusHistoryCommand.Options.Add(userOption);
statusHistoryCommand.Options.Add(passwordOption);
statusHistoryCommand.Options.Add(databasePathOption);
statusHistoryCommand.Options.Add(verboseOption);
statusHistoryCommand.Options.Add(limitOption);
statusHistoryCommand.SetAction(async parseResult =>
{
    return await StatusHistoryCommand.ExecuteAsync(
        parseResult.GetValue(databaseOption),
        parseResult.GetValue(hostOption),
        parseResult.GetValue(portOption),
        parseResult.GetValue(userOption),
        parseResult.GetValue(passwordOption),
        parseResult.GetValue(databasePathOption),
        parseResult.GetValue(verboseOption),
        parseResult.GetValue(limitOption));
});

var statusVersionCommand = new Command("version", "获取当前版本号");
statusVersionCommand.Options.Add(databaseOption);
statusVersionCommand.Options.Add(hostOption);
statusVersionCommand.Options.Add(portOption);
statusVersionCommand.Options.Add(userOption);
statusVersionCommand.Options.Add(passwordOption);
statusVersionCommand.Options.Add(databasePathOption);
statusVersionCommand.Options.Add(verboseOption);
statusVersionCommand.SetAction(async parseResult =>
{
    return await StatusVersionCommand.ExecuteAsync(
        parseResult.GetValue(databaseOption),
        parseResult.GetValue(hostOption),
        parseResult.GetValue(portOption),
        parseResult.GetValue(userOption),
        parseResult.GetValue(passwordOption),
        parseResult.GetValue(databasePathOption),
        parseResult.GetValue(verboseOption));
});

var statusSyncDryRunOption = new Option<bool>("--dry-run")
{
    Description = "预览模式"
};

var statusSyncCommand = new Command("sync", "同步未记录的迁移");
statusSyncCommand.Options.Add(databaseOption);
statusSyncCommand.Options.Add(hostOption);
statusSyncCommand.Options.Add(portOption);
statusSyncCommand.Options.Add(userOption);
statusSyncCommand.Options.Add(passwordOption);
statusSyncCommand.Options.Add(databasePathOption);
statusSyncCommand.Options.Add(verboseOption);
statusSyncCommand.Options.Add(statusSyncDryRunOption);
statusSyncCommand.SetAction(async parseResult =>
{
    return await StatusSyncCommand.ExecuteAsync(
        parseResult.GetValue(databaseOption),
        parseResult.GetValue(hostOption),
        parseResult.GetValue(portOption),
        parseResult.GetValue(userOption),
        parseResult.GetValue(passwordOption),
        parseResult.GetValue(databasePathOption),
        parseResult.GetValue(verboseOption),
        parseResult.GetValue(statusSyncDryRunOption));
});

statusCommand.Subcommands.Add(statusShowCommand);
statusCommand.Subcommands.Add(statusHistoryCommand);
statusCommand.Subcommands.Add(statusVersionCommand);
statusCommand.Subcommands.Add(statusSyncCommand);

var databaseCommand = new Command("database", "数据库管理");

var rebuildSeedOption = new Option<bool>("--seed", "-s")
{
    Description = "执行种子数据"
};
var rebuildExamplesOption = new Option<bool>("--examples", "-e")
{
    Description = "执行示例数据"
};
var rebuildForceOption = new Option<bool>("--force", "-f")
{
    Description = "强制执行，不确认"
};

var databaseRebuildCommand = new Command("rebuild", "重建数据库");
databaseRebuildCommand.Options.Add(databaseOption);
databaseRebuildCommand.Options.Add(hostOption);
databaseRebuildCommand.Options.Add(portOption);
databaseRebuildCommand.Options.Add(userOption);
databaseRebuildCommand.Options.Add(passwordOption);
databaseRebuildCommand.Options.Add(databasePathOption);
databaseRebuildCommand.Options.Add(verboseOption);
databaseRebuildCommand.Options.Add(rebuildSeedOption);
databaseRebuildCommand.Options.Add(rebuildExamplesOption);
databaseRebuildCommand.Options.Add(rebuildForceOption);
databaseRebuildCommand.SetAction(async parseResult =>
{
    return await RebuildCommand.ExecuteAsync(
        parseResult.GetValue(databaseOption),
        parseResult.GetValue(hostOption),
        parseResult.GetValue(portOption),
        parseResult.GetValue(userOption),
        parseResult.GetValue(passwordOption),
        parseResult.GetValue(databasePathOption),
        parseResult.GetValue(verboseOption),
        parseResult.GetValue(rebuildSeedOption),
        parseResult.GetValue(rebuildExamplesOption),
        parseResult.GetValue(rebuildForceOption));
});

var databaseInitCommand = new Command("init", "初始化数据库（迁移+种子）");
databaseInitCommand.Options.Add(databaseOption);
databaseInitCommand.Options.Add(hostOption);
databaseInitCommand.Options.Add(portOption);
databaseInitCommand.Options.Add(userOption);
databaseInitCommand.Options.Add(passwordOption);
databaseInitCommand.Options.Add(databasePathOption);
databaseInitCommand.Options.Add(verboseOption);
databaseInitCommand.SetAction(async parseResult =>
{
    return await InitCommand.ExecuteAsync(
        parseResult.GetValue(databaseOption),
        parseResult.GetValue(hostOption),
        parseResult.GetValue(portOption),
        parseResult.GetValue(userOption),
        parseResult.GetValue(passwordOption),
        parseResult.GetValue(databasePathOption),
        parseResult.GetValue(verboseOption));
});

databaseCommand.Subcommands.Add(databaseRebuildCommand);
databaseCommand.Subcommands.Add(databaseInitCommand);

var seedCommand = new Command("seed", "种子数据管理");

var seedRequiredCommand = new Command("required", "执行必需种子数据");
seedRequiredCommand.Options.Add(databaseOption);
seedRequiredCommand.Options.Add(hostOption);
seedRequiredCommand.Options.Add(portOption);
seedRequiredCommand.Options.Add(userOption);
seedRequiredCommand.Options.Add(passwordOption);
seedRequiredCommand.Options.Add(databasePathOption);
seedRequiredCommand.Options.Add(verboseOption);
seedRequiredCommand.SetAction(async parseResult =>
{
    return await SeedRequiredCommand.ExecuteAsync(
        parseResult.GetValue(databaseOption),
        parseResult.GetValue(hostOption),
        parseResult.GetValue(portOption),
        parseResult.GetValue(userOption),
        parseResult.GetValue(passwordOption),
        parseResult.GetValue(databasePathOption),
        parseResult.GetValue(verboseOption));
});

var seedExamplesCommand = new Command("examples", "执行示例数据");
seedExamplesCommand.Options.Add(databaseOption);
seedExamplesCommand.Options.Add(hostOption);
seedExamplesCommand.Options.Add(portOption);
seedExamplesCommand.Options.Add(userOption);
seedExamplesCommand.Options.Add(passwordOption);
seedExamplesCommand.Options.Add(databasePathOption);
seedExamplesCommand.Options.Add(verboseOption);
seedExamplesCommand.SetAction(async parseResult =>
{
    return await SeedExamplesCommand.ExecuteAsync(
        parseResult.GetValue(databaseOption),
        parseResult.GetValue(hostOption),
        parseResult.GetValue(portOption),
        parseResult.GetValue(userOption),
        parseResult.GetValue(passwordOption),
        parseResult.GetValue(databasePathOption),
        parseResult.GetValue(verboseOption));
});

var seedAllCommand = new Command("all", "执行所有种子数据");
seedAllCommand.Options.Add(databaseOption);
seedAllCommand.Options.Add(hostOption);
seedAllCommand.Options.Add(portOption);
seedAllCommand.Options.Add(userOption);
seedAllCommand.Options.Add(passwordOption);
seedAllCommand.Options.Add(databasePathOption);
seedAllCommand.Options.Add(verboseOption);
seedAllCommand.SetAction(async parseResult =>
{
    return await SeedAllCommand.ExecuteAsync(
        parseResult.GetValue(databaseOption),
        parseResult.GetValue(hostOption),
        parseResult.GetValue(portOption),
        parseResult.GetValue(userOption),
        parseResult.GetValue(passwordOption),
        parseResult.GetValue(databasePathOption),
        parseResult.GetValue(verboseOption));
});

seedCommand.Subcommands.Add(seedRequiredCommand);
seedCommand.Subcommands.Add(seedExamplesCommand);
seedCommand.Subcommands.Add(seedAllCommand);

var upgradeSchemaCommand = new Command("upgrade-schema", "Schema 升级管理");

var upgradeSchemaStatusCommand = new Command("status", "查看 Schema 版本状态");
upgradeSchemaStatusCommand.Options.Add(databaseOption);
upgradeSchemaStatusCommand.Options.Add(hostOption);
upgradeSchemaStatusCommand.Options.Add(portOption);
upgradeSchemaStatusCommand.Options.Add(userOption);
upgradeSchemaStatusCommand.Options.Add(passwordOption);
upgradeSchemaStatusCommand.Options.Add(databasePathOption);
upgradeSchemaStatusCommand.Options.Add(verboseOption);
upgradeSchemaStatusCommand.SetAction(async parseResult =>
{
    return await UpgradeSchemaStatusCommand.ExecuteAsync(
        parseResult.GetValue(databaseOption),
        parseResult.GetValue(hostOption),
        parseResult.GetValue(portOption),
        parseResult.GetValue(userOption),
        parseResult.GetValue(passwordOption),
        parseResult.GetValue(databasePathOption),
        parseResult.GetValue(verboseOption));
});

var upgradeSchemaDryRunOption = new Option<bool>("--dry-run")
{
    Description = "预览模式"
};
var upgradeSchemaForceOption = new Option<bool>("--force", "-f")
{
    Description = "强制执行"
};

var upgradeSchemaRunCommand = new Command("run", "执行 Schema 升级");
upgradeSchemaRunCommand.Options.Add(databaseOption);
upgradeSchemaRunCommand.Options.Add(hostOption);
upgradeSchemaRunCommand.Options.Add(portOption);
upgradeSchemaRunCommand.Options.Add(userOption);
upgradeSchemaRunCommand.Options.Add(passwordOption);
upgradeSchemaRunCommand.Options.Add(databasePathOption);
upgradeSchemaRunCommand.Options.Add(verboseOption);
upgradeSchemaRunCommand.Options.Add(upgradeSchemaDryRunOption);
upgradeSchemaRunCommand.Options.Add(upgradeSchemaForceOption);
upgradeSchemaRunCommand.SetAction(async parseResult =>
{
    return await UpgradeSchemaRunCommand.ExecuteAsync(
        parseResult.GetValue(databaseOption),
        parseResult.GetValue(hostOption),
        parseResult.GetValue(portOption),
        parseResult.GetValue(userOption),
        parseResult.GetValue(passwordOption),
        parseResult.GetValue(databasePathOption),
        parseResult.GetValue(verboseOption),
        parseResult.GetValue(upgradeSchemaDryRunOption),
        parseResult.GetValue(upgradeSchemaForceOption));
});

upgradeSchemaCommand.Subcommands.Add(upgradeSchemaStatusCommand);
upgradeSchemaCommand.Subcommands.Add(upgradeSchemaRunCommand);

var consoleCommand = new Command("console", "打开 psql 交互式终端");
consoleCommand.Options.Add(databaseOption);
consoleCommand.Options.Add(hostOption);
consoleCommand.Options.Add(portOption);
consoleCommand.Options.Add(userOption);
consoleCommand.Options.Add(passwordOption);
consoleCommand.Options.Add(databasePathOption);
consoleCommand.Options.Add(verboseOption);
consoleCommand.SetAction(async parseResult =>
{
    return await ConsoleCommand.ExecuteAsync(
        parseResult.GetValue(databaseOption),
        parseResult.GetValue(hostOption),
        parseResult.GetValue(portOption),
        parseResult.GetValue(userOption),
        parseResult.GetValue(passwordOption),
        parseResult.GetValue(databasePathOption),
        parseResult.GetValue(verboseOption));
});

rootCommand.Subcommands.Add(migrateCommand);
rootCommand.Subcommands.Add(statusCommand);
rootCommand.Subcommands.Add(databaseCommand);
rootCommand.Subcommands.Add(seedCommand);
rootCommand.Subcommands.Add(upgradeSchemaCommand);
rootCommand.Subcommands.Add(consoleCommand);

rootCommand.SetAction(parseResult =>
{
    Console.WriteLine("Smith 0.1.0 - PostgreSQL 数据库迁移管理工具");
    Console.WriteLine("使用 --help 查看可用命令");
    return Task.FromResult(0);
});

return await rootCommand.Parse(args).InvokeAsync();
