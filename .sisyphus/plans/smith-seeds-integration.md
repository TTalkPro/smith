# Smith Seeds 集成方案：实施方案 A+C 混合策略

## TL;DR

> **核心目标**: 将 seeds 纳入 schema 版本管理体系，解决当前 seeds 独立带来的无追踪、无校验、难同步问题
> 
> **推荐方案**: A+C 混合 - 参考数据纳入 migration + 独立 seed 追踪
> 
> **实施范围**: 
> - 修改 Smith 工具源码 (C#)
> - 定义新的 seed 文件规范
> - 兼容现有 seeds 目录结构
> 
> **预估工作量**: 3-5 个核心任务 + 2 个测试任务

---

## 一、为什么选择 A+C 混合方案？

### 1.1 方案对比回顾

| 评估维度 | 方案 A (统一追踪表) | 方案 B (独立追踪表) | 方案 C (并入 Migration) | A+C 混合 |
|---------|---------------------|---------------------|----------------------|---------|
| **实现复杂度** | 中等 | 高 | 低 | 中等 |
| **数据一致性** | 高 | 高 | 最高 | 高 |
| **语义清晰度** | 中 | 高 | 低 | 高 |
| **向后兼容** | 好 | 好 | 差 | 好 |
| **维护成本** | 低 | 中 | 低 | 低 |

### 1.2 核心决策依据

#### 决策点 1: 为什么不能纯方案 C（全部并入 Migration）？

```
❌ 问题 1: 语义污染
   Migration 的核心职责是描述 schema 变更（DDL）
   突然混入大量 DML (INSERT/UPDATE) 导致:
   - 文件体积膨胀（一个包含 1000 行数据的 migration 文件）
   - 代码审查困难（DDL 变更淹没在数据海洋中）
   - Git history 噪音增加

❌ 问题 2: 示例数据困境
   演示数据、测试数据不适合放入 migrations:
   - 不是"必需"数据
   - 可能需要频繁变更
   - 不同环境可能有不同需求

❌ 问题 3: 大批量数据性能
   如果有 10 万行初始数据:
   - 放在 migration 文件中会非常臃肿
   - 无法利用数据库批量导入工具
   - 执行超时风险增加
```

#### 决策点 2: 为什么不能纯方案 B（独立追踪表）？

```
❌ 问题 1: 重复建设
   schema_seeds 和 schema_migrations 高度相似:
   - version, description, checksum, execution_time, success...
   两套几乎相同的逻辑增加维护成本

❌ 问题 2: 查询复杂性
   查看数据库完整状态需要查询两个表:
   SELECT * FROM schema_migrations;
   SELECT * FROM schema_seeds;
   UI 显示也需要整合

❌ 问题 3: 交互复杂性
   用户需要理解两个命令:
   smith migrate up
   smith seed up
   学习成本增加
```

#### 决策点 3: 为什么 A+C 混合是最优解？

```
✅ 方案 A 核心优势（扩展追踪表）:
   - 复用现有 schema_migrations 表结构
   - 一套 API 查询所有脚本状态
   - 用户感知最小（只是一个新字段）

✅ 方案 C 核心优势（参考数据入 Migration）:
   - 原子性保证：表结构和必需数据一起创建
   - 无需追踪：CREATE TABLE + INSERT = 原子操作
   - 版本锁定：数据与 schema 版本强绑定

✅ 混合策略优势:
   ┌────────────────────────────────────────────────────────────┐
   │  数据类型          │  处理方式        │  理由              │
   ├────────────────────┼─────────────────┼───────────────────┤
   │  参考数据          │  纳入 Migration  │  与 schema 强耦合  │
   │  (roles, perms)   │  (INSERT IN FILE)│  需要原子创建   性 │
   ├────────────────────┼─────────────────┼───────────────────┤
   │  系统配置          │  纳入 Migration  │  首次必须存在      │
   │  (settings)       │  (INSERT IN FILE)│  缺失会导致报错    │
   ├────────────────────┼─────────────────┼───────────────────┤
   │  演示数据          │  独立 Seed 追踪  │  非必需、可选      │
   │  (demo users)     │  (S001_xxx.sql)  │  可独立刷新        │
   ├────────────────────┼─────────────────┼───────────────────┤
   │  大批量初始数据    │  独立 Seed 追踪  │  避免 migration   │
   │  (10k+ rows)      │  (S002_xxx.sql)  │  臃肿             │
   └────────────────────────────────────────────────────────────┘
```

### 1.3 技术合理性分析

#### 现有基础设施可复用

```
MigrationFile.cs:
├── ✅ Parse() 方法 → 可扩展支持 S 前缀
├── ✅ GetChecksum() → 直接复用
├── ✅ 版本号解析 → 只需调整正则

MigrationRunner.cs:
├── ✅ 事务包装 → 完全适用 seeds
├── ✅ 错误处理 → 完全适用 seeds
└── ✅ 回调机制 → 可复用

PostgresMigrationTracker.cs:
├── ✅ RecordAsync() → 只需添加 script_type
├── ✅ GetHistoryAsync() → 只需添加过滤
└── ✅ 幂等设计 → 完美支持 seed 重复执行
```

#### 最小侵入性

```
不修改:
- schema_migrations 表结构 (添加列是向后兼容的)
- migrations/ 目录结构
- 已有的 migration 文件

只添加:
- script_type 字段 (非空默认值 'migration')
- seeds/required/ 下的 S 前缀支持
- 新增命令选项 (--migrations-only, --seeds-only)
```

---

## 二、实施计划

### 阶段 1: 核心模型扩展 ✅ 已完成

**任务 1.1**: 扩展 MigrationFile类 ✅
- [x] 添加 `ScriptType` 枚举 (Migration, SeedRequired, SeedExample)
- [x] 扩展 `Parse()` 方法识别 `S{version}_` 前缀
- [x] 识别 `{version}_` 前缀为标准 migration

**任务 1.2**: 扩展 PostgresMigrationTracker ✅
- [x] 添加 `script_type` 字段支持
- [x] 扩展 `RecordAsync()` 方法接受 ScriptType
- [x] 扩展查询方法支持类型过滤 (`GetCurrentVersionAsync(ScriptType?)`)

### 阶段 2: Runner 增强 ✅ 已完成

**任务 2.1**: MigrationRunner 支持类型过滤 ✅
- [x] 添加 `ScriptType?` 过滤参数
- [x] 修改 `LoadAllScripts()` 返回完整列表
- [x] `RunAsync()` 支持按类型过滤

**任务 2.2**: 命令行扩展 ✅
- [x] `--migrations-only`: 仅执行 migrations
- [x] `--seeds-only`: 仅执行 seeds
- [x] 默认行为: 执行所有

### 阶段 3: 兼容与测试 🔄 部分完成

**任务 3.1**: 向后兼容处理 ✅
- [x] 检测旧表结构（无 script_type 列）
- [x] 自动添加带默认值的列
- [x] 现有命令行为保持不变

**任务 3.2**: 集成测试 ⏳ 待完成
- [ ] 测试 migration + seed 混合执行
- [ ] 测试类型过滤功能
- [ ] 测试幂等性

---

## 三、文件变更清单

```
src/Smith/
├── Migration/
│   ├── MigrationFile.cs          # 修改: 添加 ScriptType 支持
│   ├── IMigrationTracker.cs     # 修改: 添加类型参数
│   ├── PostgresMigrationTracker.cs  # 修改: 字段扩展
│   └── MigrationRunner.cs       # 修改: 类型过滤
├── Commands/
│   ├── Migrate/
│   │   └── MigrateUpCommand.cs  # 修改: 添加 --type 选项
│   └── Seed/                    # 保留: 兼容现有命令
└── Configuration/
    └── SmithConfig.cs          # 无需修改
```

---

## 四、验收标准

### 功能验收

- [x] `migrate up` 默认执行所有 (migration + seed required) - MigrationRunner 支持类型过滤
- [x] `migrate up --migrations-only` 仅执行 migrations
- [x] `migrate up --seeds-only` 仅执行 required seeds
- [ ] `status show` 正确显示所有脚本类型 (需验证)
- [x] 幂等性: 重复执行不报错，数据不重复 (ON CONFLICT DO UPDATE)

### 兼容性验收

- [x] 现有无 script_type 列的数据库自动升级 (EnsureTableExistsAsync 处理)
- [x] 现有 `seed required` 命令继续工作
- [x] 现有 migration 文件无需修改

### 代码质量

- [x] 遵循现有代码风格
- [ ] 新增单元测试覆盖核心逻辑 (待添加 ScriptType 相关测试)
- [x] 集成测试验证端到端流程 (EndToEndCommandTests)

---

## 五、风险与缓解

### 风险 1: 版本号冲突

```
问题: Migration 001_ 和 Seed S001_ 可能混淆
缓解: 使用不同的前缀字母区分，UI 显示时明确标注类型
```

### 风险 2: 事务边界

```
问题: Migration + Seed 在同一事务还是独立事务？
缓解: 默认独立事务（保持当前行为），未来可考虑添加 --atomic 选项
```

### 风险 3: 大量 Seed 数据

```
问题: S 前缀的 seed 文件如何处理大批量数据？
缓解: 建议使用 COPY 命令或批量 INSERT，Runner 无需特殊处理
```
