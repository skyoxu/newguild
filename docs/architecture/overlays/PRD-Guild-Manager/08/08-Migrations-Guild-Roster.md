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

- `LatestGuildSchemaVersion = 1`
- `schema_version` 为单行元数据表，固定 `id = 1`

## v1（当前）表结构

由 `Game.Core/Repositories/SQLiteGuildRepository.cs` 负责创建并维护：

- `Guilds`：公会基本信息
- `GuildMembers`：成员列表（`Role` 为整数枚举值）

## 版本策略（可执行）

当且仅当出现以下变化时，必须提升 `LatestGuildSchemaVersion`：

- 表结构变更：新增/删除列、字段语义变化、主键/外键/约束变化
- 数据语义变更：同一列存储含义变化（例如 Role 编码规则变化）

迁移实现规则：

1. 先执行迁移步骤（必须可重放、幂等，允许重复执行而不破坏数据）。
2. 再调用 `SchemaMigrationRunner.EnsureLatestAsync(db, LatestGuildSchemaVersion)` 更新 `schema_version.version`。
3. 必须配套 xUnit 覆盖：旧版本→新版本升级路径、升级后 `schema_version` 与关键数据一致性校验（ADR-0005）。

备注：当前版本为 v1，尚不存在历史版本升级路径；本页用于固化后续演进规则。

