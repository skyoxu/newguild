---
PRD-ID: PRD-Guild-Manager
Title: 08 Migrations - Guild Recruitment (Schema Versioning)
Updated: true
Arch-Refs:
  - CH05
  - CH06
ADR-Refs:
  - ADR-0005
  - ADR-0006
  - ADR-0007
---

本页定义“公会招募（Recruitment）”相关 SQLite 表的版本策略与最小迁移口径，用于避免后续演进时出现口径漂移、不可回放的升级路径，以及“版本号已提升但数据未迁移”的不可逆状态。

## SSoT 与版本边界

- `schema_version` 为单行元数据表，固定 `id = 1`
- Guild DB 的 `schema_version` 是**同一个数据库文件的全局版本号**，由 `Game.Core/Persistence/Migrations/GuildDbSchema.cs` 作为单一事实源统一维护
- 迁移执行器：`Game.Core/Persistence/Migrations/SchemaMigrationRunner.cs`

## 当前版本

- `GuildDbSchema.LatestVersion = 2`

## v2（当前）新增表结构

由 `Game.Core/Repositories/SQLiteRecruitmentOfferRepository.cs` 使用 `SchemaMigrationRunner` 按版本顺序创建并维护：

- `RecruitmentOffers`：仅存储**待处理（pending）**的招募申请/offer
  - 主键：`OfferId`
  - 关键字段：`GuildId`、`CandidateId`、`Role`、`PresentedAt`
  - 约束：`UNIQUE (GuildId, CandidateId)` 防止同一候选人对同一公会重复挂起申请
  - 外键：`GuildId → Guilds(GuildId)`（`ON DELETE CASCADE`）

说明：
- 本表用于“可回放/可恢复”的运行中状态；已解决（accepted/rejected/withdrawn）的 offer 在流程完成后可被删除，不作为历史归档。

## 版本策略（可执行）

当且仅当出现以下变化时，必须提升 `GuildDbSchema.LatestVersion` 并补齐迁移步骤：

- 表结构变化：新增/删除列、字段语义变更、主键/外键/约束变更、索引策略变更
- 数据语义变化：同一列的含义改变（例如 `Role` 编码规则变化）
- 约束语义变化：例如从“允许重复申请”改为“禁止重复申请”（影响 `UNIQUE`/冲突处理）

迁移实现规则：
1. 每个版本 `migrations[version]` 必须**幂等**且**可重放**（允许重复执行不破坏数据）
2. `SchemaMigrationRunner` 必须 fail-fast：缺失任何版本的迁移步骤直接失败（避免不可逆损坏）
3. 必须配套 xUnit 覆盖升级路径与一致性验证（ADR-0005）
   - 例如：从 v1 → v2，验证 `schema_version.version` 正确提升，且 `RecruitmentOffers` 可读写

