# Smith Schema 升级机制改进计划

## 问题背景

当前 `EnsureTableExistsAsync` 实现存在以下问题：

1. **只检测列存在性，不检测类型/约束**
   - `ADD COLUMN IF NOT EXISTS` 不会修改已存在列的类型
   - 主键变更无法处理

2. **主键变更问题**
   - 旧表: `PRIMARY KEY (version)`
   - 新表: `PRIMARY KEY (version, script_type)`
   - PostgreSQL 不支持直接修改主键

3. **静默忽略错误**
   - 升级失败被 catch 忽略
   - 用户无法感知问题

---

## 设计目标

1. **安全性**: 升级操作必须显式确认，不能隐式执行
2. **可预测性**: 用户明确知道将要发生什么
3. **可回滚**: 升级失败时数据库状态不变
4. **向后兼容**: 支持从旧版本平滑升级

---

## 解决方案：显式 `upgrade-schema` 命令

### 命令设计

```bash
# 检查当前 schema 状态
smith upgrade-schema status -d mydb

# 预览升级操作（不执行）
smith upgrade-schema check -d mydb --dry-run

# 执行升级
smith upgrade-schema run -d mydb

# 强制升级（跳过确认）
smith upgrade-schema run -d mydb --force
```

### 升级策略

```
┌─────────────────────────────────────────────────────────────────┐
│                     Schema 升级流程                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  1. 检测阶段                                                    │
│     ├── 查询 information_schema 获取当前表结构                  │
│     ├── 对比目标结构                                            │
│     └── 生成差异报告                                            │
│                                                                 │
│  2. 验证阶段                                                    │
│     ├── 检查数据完整性（无重复 version）                        │
│     ├── 检查外键依赖                                            │
│     └── 估算升级风险                                            │
│                                                                 │
│  3. 执行阶段                                                    │
│     ├── BEGIN TRANSACTION                                       │
│     ├── 执行 ALTER 语句                                         │
│     ├── 验证结果                                                │
│     └── COMMIT / ROLLBACK                                       │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 实施任务

### 任务 1: Schema 版本定义 ✅ 已完成

**目标**: 定义 schema 版本枚举和目标结构

**实现**:
- [x] 创建 `SchemaVersion` 枚举
- [x] 定义每个版本的完整表结构
- [x] 实现版本差异计算

**文件**: `src/Smith/Migration/SchemaVersion.cs`

```csharp
public enum SchemaVersion
{
    V1,  // 原始版本: PRIMARY KEY (version)
    V2   // 当前版本: PRIMARY KEY (version, script_type)
}

public static class SchemaDefinitions
{
    public static string GetV1Schema() => """
        CREATE TABLE schema_migrations (
            version INTEGER PRIMARY KEY,
            description VARCHAR(255),
            ...
        )
        """;
    
    public static string GetV2Schema() => """
        CREATE TABLE schema_migrations (
            version INTEGER NOT NULL,
            script_type VARCHAR(20) DEFAULT 'Migration',
            ...
            PRIMARY KEY (version, script_type)
        )
        """;
}
```

---

### 任务 2: Schema 检测器 ✅ 已完成

**目标**: 检测当前数据库的 schema 版本

**实现**:
- [x] 查询 `information_schema.columns` 获取列信息
- [x] 查询 `information_schema.table_constraints` 获取主键信息
- [x] 对比确定当前版本

**文件**: `src/Smith/Migration/SchemaDetector.cs`

```csharp
public class SchemaDetector
{
    public async Task<SchemaVersion> DetectCurrentVersionAsync(NpgsqlConnection conn)
    {
        // 检查 script_type 列是否存在
        // 检查主键是单列还是复合
        // 返回对应的 SchemaVersion
    }
    
    public async Task<SchemaDiff> GetDiffAsync(NpgsqlConnection conn, SchemaVersion targetVersion)
    {
        // 返回当前结构 vs 目标结构的差异
    }
}
```

---

### 任务 3: 升级执行器 ✅ 已完成

**目标**: 执行 schema 升级操作

**实现**:
- [x] 生成安全的 ALTER 语句
- [x] 在事务中执行
- [x] 处理升级失败回滚

**文件**: `src/Smith/Migration/SchemaUpgrader.cs`

```csharp
public class SchemaUpgrader
{
    public async Task<UpgradeResult> UpgradeAsync(
        NpgsqlConnection conn, 
        SchemaVersion fromVersion,
        SchemaVersion toVersion,
        bool dryRun = false)
    {
        await using var transaction = await conn.BeginTransactionAsync();
        try
        {
            var sql = GenerateUpgradeSql(fromVersion, toVersion);
            
            if (dryRun)
            {
                return new UpgradeResult { Sql = sql, DryRun = true };
            }
            
            await ExecuteSqlAsync(conn, sql, transaction);
            await transaction.CommitAsync();
            
            return new UpgradeResult { Success = true };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
    
    private string GenerateUpgradeSql(SchemaVersion from, SchemaVersion to)
    {
        // V1 -> V2 升级 SQL
        if (from == SchemaVersion.V1 && to == SchemaVersion.V2)
        {
            return """
                -- 1. 添加 script_type 列
                ALTER TABLE schema_migrations 
                ADD COLUMN IF NOT EXISTS script_type VARCHAR(20) DEFAULT 'Migration';
                
                -- 2. 删除旧主键
                ALTER TABLE schema_migrations 
                DROP CONSTRAINT IF EXISTS schema_migrations_pkey;
                
                -- 3. 添加新主键
                ALTER TABLE schema_migrations 
                ADD PRIMARY KEY (version, script_type);
                """;
        }
        
        throw new NotSupportedException($"Cannot upgrade from {from} to {to}");
    }
}
```

---

### 任务 4: CLI 命令 🔄 部分完成

**目标**: 添加 `upgrade-schema` 命令

**实现**:
- [x] 添加 `UpgradeSchemaStatusCommand` (status 子命令)
- [x] 添加 `UpgradeSchemaRunCommand` (run 子命令)
- [x] 添加 `--dry-run` 和 `--force` 选项
- [ ] 添加 `check` 子命令 (预览升级 SQL)

**文件**: `src/Smith/Commands/UpgradeSchema/UpgradeSchemaCommand.cs`

```csharp
public class UpgradeSchemaStatusCommand : AsyncCommand<ConnectionSettings>
{
    public override async Task<int> ExecuteAsync(...)
    {
        var detector = new SchemaDetector();
        var currentVersion = await detector.DetectCurrentVersionAsync(connection);
        var targetVersion = SchemaVersion.V2;
        
        renderer.Info($"当前 Schema 版本: {currentVersion}");
        renderer.Info($"目标 Schema 版本: {targetVersion}");
        
        if (currentVersion == targetVersion)
        {
            renderer.Success("Schema 已是最新版本");
            return 0;
        }
        
        var diff = await detector.GetDiffAsync(connection, targetVersion);
        renderer.Warning("需要升级:");
        foreach (var change in diff.Changes)
        {
            renderer.Info($"  - {change}");
        }
        
        return 0;
    }
}
```

---

### 任务 5: 启动时检测 ✅ 已完成

**目标**: 在 migrate 命令启动时检测 schema 版本

**实现**:
- [x] 修改 `MigrationRunner.RunAsync`
- [x] 如果检测到旧版本，提示用户运行 `upgrade-schema`

**文件**: `src/Smith/Migration/MigrationRunner.cs`

```csharp
public async Task<int> RunAsync(...)
{
    var detector = new SchemaDetector();
    var currentVersion = await detector.DetectCurrentVersionAsync(_connection);
    
    if (currentVersion < SchemaVersion.V2)
    {
        _renderer.Error("Schema 版本过旧，请先运行: smith upgrade-schema run -d <database>");
        _renderer.Info($"当前版本: {currentVersion}, 需要版本: {SchemaVersion.V2}");
        return -1;
    }
    
    // 原有逻辑...
}
```

---

### 任务 6: 测试 ✅ 已完成

**目标**: 验证升级功能

**测试用例**:
- [x] V1 → V2 升级成功
- [x] V2 数据库不执行升级
- [x] 升级后数据完整性
- [x] 升级失败回滚
- [x] dry-run 不修改数据

**文件**: 
- [x] `tests/Smith.Tests/Integration/SchemaDetectorTests.cs`
- [x] `tests/Smith.Tests/Integration/SchemaUpgraderTests.cs`

---

## 文件变更清单

```
src/Smith/
├── Migration/
│   ├── SchemaVersion.cs          # 新增: Schema 版本定义
│   ├── SchemaDetector.cs         # 新增: Schema 检测器
│   ├── SchemaUpgrader.cs         # 新增: 升级执行器
│   └── MigrationRunner.cs        # 修改: 启动时检测
├── Commands/
│   └── UpgradeSchema/
│       ├── UpgradeSchemaCommand.cs    # 新增: 主命令
│       ├── StatusCommand.cs           # 新增: status 子命令
│       ├── CheckCommand.cs            # 新增: check 子命令
│       └── RunCommand.cs              # 新增: run 子命令
└── Program.cs                    # 修改: 注册命令

tests/Smith.Tests/
└── Migration/
    ├── SchemaDetectorTests.cs    # 新增: 检测器测试
    └── SchemaUpgraderTests.cs    # 新增: 升级器测试
```

---

## 验收标准

### 功能验收

- [x] `upgrade-schema status` 正确显示当前版本
- [x] `upgrade-schema run --dry-run` 预览升级 SQL
- [ ] `upgrade-schema check --dry-run` 预览升级 SQL (check 子命令未实现)
- [x] `upgrade-schema run` 成功升级 V1 → V2
- [x] 升级后 `script_type` 列存在且有默认值
- [x] 升级后主键为 (version, script_type)
- [x] 升级失败时数据不变 (事务回滚)

### 安全验收

- [x] 旧版本数据库执行 migrate 时提示升级
- [x] 升级需要用户确认（除非 --force）
- [x] 升级在事务中执行

### 兼容性验收

- [x] 新安装直接创建 V2 表结构
- [x] V1 → V2 升级不丢失数据
- [x] 现有 migration 记录保持不变

### 测试验收

- [ ] SchemaDetector 单元测试
- [ ] SchemaUpgrader 单元测试
- [ ] V1 → V2 升级集成测试

---

## 风险与缓解

### 风险 1: 升级过程中断

**问题**: 升级执行到一半时连接断开
**缓解**: 所有操作在单个事务中，失败自动回滚

### 风险 2: 数据冲突

**问题**: V1 表中存在重复 version
**缓解**: 升级前检测，存在重复时报错并提示手动处理

### 风险 3: 并发执行

**问题**: 多个进程同时执行升级
**缓解**: 使用 `LOCK TABLE` 防止并发

---

## 时间估算

| 任务 | 估算时间 |
|------|----------|
| 任务 1: Schema 版本定义 | 0.5h |
| 任务 2: Schema 检测器 | 1h |
| 任务 3: 升级执行器 | 1h |
| 任务 4: CLI 命令 | 1h |
| 任务 5: 启动时检测 | 0.5h |
| 任务 6: 测试 | 1h |
| **总计** | **5h** |
