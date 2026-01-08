# 08-DataSchema-SaveLoad（T3 前置：数据字典与 Schema 规范）

## 目标与范围

本文件定义 **T3 Save/Load + Schema Migration** 的最小可执行口径，提供：

- 数据字典（Data Dictionary）：核心实体、字段含义、约束与版本策略
- 数据库存储规范（SQLite Schema Norms）：表/索引/约束/版本表/迁移规则
- 失败路径审计与脱敏规则（与 ADR-0019 对齐）

本文件是 Overlay 的“验收锚点”，用于约束实现与测试；实现细节与阈值请引用 ADR/Base，不在此处复制。

## ADR / Base 引用（口径来源）

- ADR-0006：Data Storage（SQLite / schema version / migration runner）
- ADR-0019：Security Audit JSONL（统一五字段审计 + 脱敏要求）
- ADR-0004：Event Bus and Contracts（事件命名规范；CloudEvents type 字段口径）
- CH05 / CH06 / CH07：数据模型与端口、运行时/错误路径、质量门禁

## 1. 稳定 ID 与版本（面向 Save/Load）

### 1.1 SchemaVersion

- `schemaVersion`：整数（`>= 1`），仅递增
- 迁移必须是 **幂等** 且 **可回放**（重复执行不会破坏数据）
- 迁移 runner 必须记录：
  - 当前版本
  - 目标版本
  - 已应用迁移列表（可选）

### 1.2 Save Snapshot（逻辑快照）

Save/Load 的“逻辑快照”应当与 UI/脚本无关，能够在纯 .NET 测试中构造并 round-trip。

最小快照包含：

- `SaveId`：稳定 ID（见 `SaveIdValue` 相关口径；不允许空/不允许路径片段）
- `CreatedAt`：`DateTimeOffset`
- `SchemaVersion`：与数据库 schemaVersion 一致
- `PayloadVersion`：逻辑快照格式版本（可选；与 DB schemaVersion 区分）

## 2. 数据字典（Data Dictionary）

> 说明：字段命名在数据库层采用 snake_case；在 C# DTO 采用 PascalCase。两者必须有明确映射。

### 2.1 表：schema_version

- `id`：主键，固定为 1（单行表）
- `version`：当前 schema version（int）
- `updated_at_utc`：最后更新时间（ISO8601 文本或 INTEGER epoch；保持一致）

约束：

- 必须存在且仅有一行（`id=1`）

### 2.2 表：save_slots（最小存档槽位索引）

- `save_id`：TEXT 主键（稳定 ID）
- `created_at_utc`：TEXT（ISO8601）
- `updated_at_utc`：TEXT（ISO8601）
- `schema_version`：INTEGER（当前 DB schemaVersion）

约束：

- `save_id` 非空
- `schema_version >= 1`

备注：

- 本表只保存最小索引与版本信息，避免把大 payload 全塞进索引表。

### 2.3 表：save_kv（最小 Key-Value Payload，用于早期阶段）

- `save_id`：TEXT（FK -> save_slots.save_id）
- `k`：TEXT
- `v`：TEXT（JSON string 或 plain string；必须在实现中明确一致）

约束：

- `(save_id, k)` 组合唯一

用途：

- T3 早期允许用 KV 方式落地最小可玩性（成员列表、招募候选等），后续可演进为更结构化表。

## 3. SQLite Schema 规范（Norms）

### 3.1 基本原则

- 只允许 `user://` 的数据库路径（详见 ADR-0019/安全基线）
- 默认启用外键（`PRAGMA foreign_keys=ON`）
- 对写操作使用事务（transaction），保证 Save 操作原子性

### 3.2 索引建议（最小集）

- `save_slots(updated_at_utc)`：按最近存档排序（可选）
- `save_kv(save_id)`：按 save_id 查询 payload

## 4. 迁移规则（Migration Rules）

### 4.1 迁移 runner 责任边界

- runner 只负责 DB schemaVersion 的推进
- 逻辑快照/内容版本的兼容（PayloadVersion）应由 Core 层的映射器处理

### 4.2 迁移失败的审计与脱敏

迁移失败必须写入 `security-audit.jsonl`，格式严格遵循 ADR-0019 五字段：

- `ts`
- `action`：例如 `db.migration.failed`
- `reason`：失败原因（Release/secure 模式不得包含绝对路径/SQL 原文）
- `target`：例如 `user://saves/game.db`
- `caller`：例如 `SqliteDataStore` 或 `MigrationRunner`

## 5. 事件与可观测性（仅列名，不复制阈值）

Save/Load 相关事件 type（CloudEvents `type` 字段）应遵循 ADR-0004，最小集：

- `core.save.requested`
- `core.save.completed`
- `core.save.failed`
- `core.load.requested`
- `core.load.completed`
- `core.load.failed`
- `core.storage.migration.applied`

本文件只作为“事件名清单”，字段定义与契约落盘位置以 Contracts SSoT 为准。

