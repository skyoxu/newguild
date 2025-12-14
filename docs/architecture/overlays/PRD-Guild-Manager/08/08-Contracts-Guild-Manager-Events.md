---
PRD-ID: PRD-Guild-Manager
Title: 公会管理事件（Guild Manager Events）契约更新
ADR-Refs:
  - ADR-0004
  - ADR-0005
Test-Refs:
  - Game.Core.Tests/Domain/GuildContractsTests.cs
Contracts-Refs:
  - Game.Core/Contracts/Guild/GuildCreated.cs
  - Game.Core/Contracts/Guild/GuildMemberJoined.cs
  - Game.Core/Contracts/Guild/GuildMemberLeft.cs
  - Game.Core/Contracts/Guild/GuildDisbanded.cs
  - Game.Core/Contracts/Guild/GuildMemberRoleChanged.cs
Status: Proposed
---

本页为功能纵切（08 章）对应“公会管理事件”契约更新的记录与验收口径。

变更意图（引用，不复制口径）

- 事件命名遵循统一规范 `${DOMAIN_PREFIX}.<entity>.<action>`（ADR‑0004）；
- 质量门禁与变更追踪引用 ADR‑0005；

影响范围

- 契约文件：`Game.Core/Contracts/Guild/`（5 个 C# 事件记录类型，per ADR-0020）
- 受影响模块：公会管理场景的事件发布/订阅、UI 状态同步

验收要点（就地）

- 单测覆盖事件构造与必需字段；E2E 占位用例存在（见 Test-Refs）

## 核心公会事件契约（Godot + C#）

- **GuildCreated** (`core.guild.created`)
  - 触发时机：公会创建成功后
  - 字段示例：GuildId, CreatorId, GuildName, CreatedAt
  - 契约位置（规划）：Game.Core/Contracts/Guild/GuildCreated.cs
- **GuildMemberJoined** (`core.guild.member.joined`)
  - 触发时机：成员成功加入公会后
  - 字段示例：UserId, GuildId, JoinedAt, Role
  - 契约位置：`Game.Core/Contracts/Guild/GuildMemberJoined.cs`
  - 说明：当用户成功加入某个公会时触发，用于驱动 Guild Manager 场景内成员列表与日志更新。
- **GuildMemberLeft** (`core.guild.member.left`)
  - 触发时机：成员离开或被移出公会后
  - 字段示例：UserId, GuildId, LeftAt, Reason
  - 契约位置（规划）：Game.Core/Contracts/Guild/GuildMemberLeft.cs

## 扩展公会事件契约（Godot + C#）

- **GuildDisbanded** (`core.guild.disbanded`)
  - 触发时机：公会被解散时（主动解散或管理策略触发）
  - 字段示例：GuildId, DisbandedByUserId, DisbandedAt, Reason
  - 契约位置：Game.Core/Contracts/Guild/GuildDisbanded.cs
- **GuildMemberRoleChanged** (`core.guild.member.role_changed`)
  - 触发时机：公会成员角色发生变更时（如 member → admin）
  - 字段示例：UserId, GuildId, OldRole, NewRole, ChangedAt, ChangedByUserId
  - 契约位置：Game.Core/Contracts/Guild/GuildMemberRoleChanged.cs

> 约定：任何新增或调整 Guild 相关 C# 契约（Game.Core/Contracts/Guild/，per ADR-0020）时，必须同步更新：
> - Game.Core.Tests/Domain/GuildContractsTests.cs（新增/调整对应测试用例）
> - scripts/python/check_guild_contracts.py（扩展 EXPECTED 列表）
> - 如涉及 Overlay 内容变化，仍需通过 scripts/python/validate_contracts.py 校验 Overlay ↔ Contracts 回链。
## SaveId 值对象（GameLoop 关联）

- **SaveIdValue**
  - 用途：作为长生命周期存档槽的逻辑标识，避免直接使用原始字符串参与路径或 SQL 片段拼接。
  - 规则：仅允许 `[a-zA-Z0-9_-]`，长度 1–64，违反规则时抛出异常。
  - 契约位置：`Game.Core/Domain/Turn/SaveIdValue.cs`
  - 关联事件：`core.game_turn.started` / `core.game_turn.phase_changed` / `core.game_turn.week_advanced` 中的 `SaveId` 字段应基于该值对象生成，避免未经验证的输入进入事件与持久化层。
