# Smith

C# TUI PostgreSQL 数据库管理工具，专注于迁移管理。

## 技术栈

- C# / .NET 10.0
- **System.CommandLine** (CLI 框架) - Microsoft 官方
- **Npgsql 10.0.1**（PostgreSQL 驱动）
- **Native AOT** 编译，生成原生二进制
- xUnit + FluentAssertions + Moq（测试）

## 安装

### 从源码构建

**前置要求：**
- .NET 10.0 SDK 或更高版本
- PostgreSQL 数据库

**构建步骤：**
```bash
# 克隆仓库
git clone <repository-url>
cd smith

# 构建
dotnet build

# 运行
dotnet run --project src/Smith -- --help
```

### 构建独立二进制（Linux）

Smith 支持两种构建模式，均无需目标机器安装 .NET Runtime。

**前置要求：**
- .NET 10.0 SDK
- Linux x64 系统

#### Native AOT 构建（推荐）

编译为原生机器码，体积小、启动快。

```bash
./build-linux-aot.sh

# 生成的二进制文件位于
publish/linux-x64/smith    # ~11 MB

# 安装到系统（可选）
sudo cp publish/linux-x64/smith /usr/local/bin/
```

#### Self-Contained 构建

打包完整 .NET 运行时，兼容性更好，但体积较大。

```bash
./build-linux-contained.sh

# 生成的二进制文件位于
publish/linux-x64/smith    # ~74 MB
```

**两种模式对比：**

| | Native AOT | Self-Contained |
|---|---|---|
| 二进制大小 | ~11 MB | ~74 MB |
| 启动速度 | 极快（无 JIT） | 较慢（JIT 预热） |
| 运行时依赖 | 无 | 无 |
| 构建脚本 | `build-linux-aot.sh` | `build-linux-contained.sh` |

## 快速开始

```bash
# 查看帮助
dotnet run --project src/Smith -- --help

# 执行迁移
dotnet run --project src/Smith -- migrate up -d owl_dev -D ../../database

# 查看迁移状态
dotnet run --project src/Smith -- status show -d owl_dev -D ../../database
```

## 命令

```
smith
├── migrate
│   └── up        [-d db] [-t version] [--dry-run] [-v]
├── status
│   ├── show      [-d db]             # 迁移状态表格
│   ├── history   [-d db] [-n limit]  # 执行历史
│   ├── version   [-d db]             # 当前版本号（脚本友好）
│   └── sync      [-d db] [--dry-run] # 同步未记录的迁移
├── database
│   ├── rebuild   [-d db] [-s] [-e] [-f]  # 重建数据库
│   └── init      [-d db]                  # 初始化（迁移+种子）
├── seed
│   ├── required  [-d db]
│   ├── examples  [-d db]
│   └── all       [-d db]
├── console       [-d db]             # 打开 psql
└── --version
```

### 连接参数

所有命令共享以下参数：

| 参数 | 说明 | 默认值 |
|------|------|--------|
| `-d, --database` | 数据库名称 | - |
| `-H, --host` | 主机 | localhost |
| `-P, --port` | 端口 | 5432 |
| `-u, --user` | 用户 | postgres |
| `-p, --password` | 密码 | - |
| `-D, --database-path` | 数据库脚本目录 | 当前目录 |
| `-v, --verbose` | 详细输出 | false |

### 环境变量

| 变量 | 说明 |
|------|------|
| `SMITH_HOST` | 数据库主机 |
| `SMITH_PORT` | 数据库端口 |
| `SMITH_USER` | 数据库用户 |
| `SMITH_PASSWORD` | 数据库密码 |
| `SMITH_DATABASE` | 数据库名称 |
| `SMITH_DATABASE_PATH` | 数据库脚本目录 |

**配置优先级**: CLI 参数 > 环境变量 > smith.json > 默认值

### 用法示例

```bash
# 执行所有待处理迁移
smith migrate up -d owl_dev -D /path/to/database

# 预览将要执行的迁移（不实际执行）
smith migrate up -d owl_dev -D /path/to/database --dry-run

# 执行到指定版本
smith migrate up -d owl_dev -D /path/to/database -t 25

# 查看迁移状态
smith status show -d owl_dev -D /path/to/database

# 查看执行历史（最近 10 条）
smith status history -d owl_dev -n 10

# 获取当前版本号（适合脚本使用）
version=$(smith status version -d owl_dev)

# 同步未记录的迁移（预览）
smith status sync -d owl_dev -D /path/to/database --dry-run

# 重建数据库（删除 → 创建 → 迁移 → 种子数据）
smith database rebuild -d owl_test -D /path/to/database -s -f

# 初始化数据库（迁移 + 必需种子数据）
smith database init -d owl_dev -D /path/to/database

# 打开 psql 终端
smith console -d owl_dev
```

## 目录结构

数据库脚本目录（`-D` 参数指定）应包含：

```
database/
├── migrations/          # 迁移文件（001_xxx.sql, 002_xxx.sql, ...）
└── seeds/
    ├── required/        # 必需种子数据
    └── examples/        # 示例数据
```

迁移文件命名格式：`{版本号}_{描述}.sql`，例如 `001_create_users_table.sql`。

## 迁移同步

当迁移已在数据库中手动执行但未记录到 `schema_migrations` 表时，使用 `status sync` 命令：

1. 读取 SQL 文件，解析 CREATE 语句（TABLE/FUNCTION/INDEX/EXTENSION/TRIGGER/VIEW）
2. 检查数据库中对应对象是否存在
3. 全部对象存在则标记为可同步，补录到 `schema_migrations`

## 兼容性

- 使用与 owl-db（Ruby 版）相同的 `schema_migrations` 表结构
- 使用相同的迁移文件命名格式
- 两工具可在过渡期并存

## 测试

```bash
cd tools/smith
dotnet test
```

需要本地 PostgreSQL 服务运行（测试会自动创建和删除临时数据库）。

## 项目结构

```
smith/
├── src/Smith/
│   ├── Program.cs                  # 入口，命令注册
│   ├── Configuration/              # 配置模型和加载器
│   ├── Database/                   # 连接工厂、Schema 检查
│   ├── Migration/                  # 迁移文件、追踪、执行、SQL 检测、同步
│   ├── Commands/                   # CLI 命令实现
│   └── Rendering/                  # 控制台输出抽象
└── tests/Smith.Tests/
    ├── Configuration/              # 配置测试
    ├── Migration/                  # 迁移相关单元测试
    ├── Integration/                # PostgreSQL 集成测试
    └── Fixtures/                   # 测试用 SQL 文件
```
