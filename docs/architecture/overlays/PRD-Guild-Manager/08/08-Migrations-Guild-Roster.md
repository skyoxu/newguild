---
PRD-ID: PRD-Guild-Manager
Title: 08 Migrations - Guild Roster (Schema Versioning)
Updated: true
Arch-Refs:
  - CH05
  - CH06
ADR-Refs:
  - ADR-0005
  - ADR-0007
---

本页定义“公会 roster（成员列表）”相关 SQLite 表的版本策略与最小迁移口径，用于避免后续演进时出现口径漂移与不可回放升级路径。

## 当前版本

- `schema_version` 为单行元数据表，固定 `id = 1`
- Guild DB 的 `schema_version` 是**同一个数据库文件的全局版本号**，由 `Game.Core/Persistence/Migrations/GuildDbSchema.cs` 作为单一事实源统一维护
- `GuildDbSchema.LatestVersion = 2`（其中 v1 覆盖 roster 表结构，v2 新增 recruitment 相关表）

## v1（当前）表结构

由 `Game.Core/Repositories/SQLiteGuildRepository.cs` 负责创建并维护：

- `Guilds`：公会基本信息
- `GuildMembers`：成员列表（`Role` 为整数枚举值）

## 版本策略（可执行）

当且仅当出现以下变化时，必须提升 `GuildDbSchema.LatestVersion` 并补齐迁移步骤：

- 表结构变更：新增/删除列、字段语义变化、主键/外键/约束变化
- 数据语义变更：同一列存储含义变化（例如 Role 编码规则变化）

迁移实现规则：

1. 定义迁移步骤映射 `migrations`：键为**目标版本号**（`>= 1`），值为执行迁移的函数；每一步必须可重放/幂等（允许重复执行而不破坏数据）。
2. 调用 `SchemaMigrationRunner.EnsureLatestAsync(db, GuildDbSchema.LatestVersion, GuildDbSchema.CreateMigrations())`；Runner 负责创建 `schema_version`、按版本顺序执行缺失迁移并更新 `schema_version.version`。
   - 缺失任何版本的迁移步骤必须 **fail-fast**（避免“版本号已提升但数据未迁移”的不可回退状态）。
3. 必须配套 xUnit 覆盖：旧版本→新版本升级路径、升级后 `schema_version` 与关键数据一致性校验（ADR-0005）。

备注：当前版本为 v1，尚不存在历史版本升级路径；本页用于固化后续演进规则。

