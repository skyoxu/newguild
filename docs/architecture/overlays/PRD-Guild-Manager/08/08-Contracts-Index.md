---
PRD-ID: PRD-Guild-Manager
PRD-Refs:
  - docs/prd.txt
Title: 契约索引（Contracts Index）
Status: Proposed
Arch-Refs:
  - CH01
  - CH03
ADR-Refs:
  - ADR-0004
  - ADR-0020
---

本页提供 PRD-Guild-Manager 的“契约单一入口索引”，用于避免在多个纵切页重复拷贝契约口径而导致漂移。

## 止损规则

- **领域事件**：凡会写入视图任务 `contractRefs`、写入 Overlay 08、且会被跨模块/跨层消费的事件，视为领域事件；必须：
  - `EventType` 命名遵循 ADR-0004（当前域前缀为 `core.`）；
  - 在 `Game.Core/Contracts/**` 中落盘 **强类型 C# 契约**（不依赖 Godot）；
  - 由 Core 发布、由 UI/适配层订阅；失败时不发布事件（仅记录审计/调试信息）。
- **内部调试事件**：仅用于本地调试/开发诊断、不会跨模块消费的信号/事件，不应写入 `contractRefs`，也不应进入 Overlay 08 作为领域事件口径。

## 现有契约页（Overlay）

- 公会管理器领域事件（Guild Manager Events）：`08-Contracts-Guild-Manager-Events.md`
- CloudEvent 契约：`08-Contracts-CloudEvent.md`
- CloudEvents Core 契约：`08-Contracts-CloudEvents-Core.md`
- 安全契约：`08-Contracts-Security.md`
- 质量指标（Quality Metrics）：`08-Contracts-Quality-Metrics.md`
- 外链白名单（ALLOWED_EXTERNAL_HOSTS）：`08-Contracts-Allowed-External-Hosts.md`

## 领域事件（已落盘到 Game.Core/Contracts）

> 说明：以下为“领域事件（Domain Event）”清单，事件 `EventType` 作为 CloudEvents 1.0 的 `type` 字段口径；
> 合同文件均为纯 C#（不依赖 Godot），可在 xUnit 环境直接编译与验证。

### Guild

- `core.guild.created` → `Game.Core/Contracts/Guild/GuildCreated.cs`
- `core.guild.disbanded` → `Game.Core/Contracts/Guild/GuildDisbanded.cs`
- `core.guild.member.joined` → `Game.Core/Contracts/Guild/GuildMemberJoined.cs`
- `core.guild.member.left` → `Game.Core/Contracts/Guild/GuildMemberLeft.cs`
- `core.guild.member.role_changed` → `Game.Core/Contracts/Guild/GuildMemberRoleChanged.cs`

### Game Loop / Turn

- `core.game_turn.started` → `Game.Core/Contracts/GameLoop/GameTurnStarted.cs`
- `core.game_turn.phase_changed` → `Game.Core/Contracts/GameLoop/GameTurnPhaseChanged.cs`
- `core.game_turn.week_advanced` → `Game.Core/Contracts/GameLoop/GameWeekAdvanced.cs`

### Persistence (Save/Load)

- `core.save.requested` → `Game.Core/Contracts/Persistence/SaveRequested.cs`
- `core.save.completed` → `Game.Core/Contracts/Persistence/SaveCompleted.cs`
- `core.save.failed` → `Game.Core/Contracts/Persistence/SaveFailed.cs`
- `core.load.requested` → `Game.Core/Contracts/Persistence/LoadRequested.cs`
- `core.load.completed` → `Game.Core/Contracts/Persistence/LoadCompleted.cs`
- `core.load.failed` → `Game.Core/Contracts/Persistence/LoadFailed.cs`
- `core.save.format.migration.applied` → `Game.Core/Contracts/Persistence/SaveFormatMigrationApplied.cs`

### AI

- `core.ai.cycle.started` → `Game.Core/Contracts/AI/AiCycleStarted.cs`
- `core.ai.intent.issued` → `Game.Core/Contracts/AI/AiIntentIssued.cs`
- `core.ai.cycle.completed` → `Game.Core/Contracts/AI/AiCycleCompleted.cs`
- `core.ai.ecosystem.step.completed` → `Game.Core/Contracts/AI/AiEcosystemStepCompleted.cs`

### Recruitment

- `core.recruitment.offer.presented` → `Game.Core/Contracts/Recruitment/RecruitmentOfferPresented.cs`
- `core.recruitment.offer.resolved` → `Game.Core/Contracts/Recruitment/RecruitmentOfferResolved.cs`

### Raid

- `core.raid.scheduled` → `Game.Core/Contracts/Raid/RaidScheduled.cs`
- `core.raid.resolved` → `Game.Core/Contracts/Raid/RaidResolved.cs`

### Social

- `core.social.interaction.triggered` → `Game.Core/Contracts/Social/SocialInteractionTriggered.cs`
- `core.social.relationship.changed` → `Game.Core/Contracts/Social/SocialRelationshipChanged.cs`

### Media & Reputation

- `core.media.beat.triggered` → `Game.Core/Contracts/Media/MediaBeatTriggered.cs`
- `core.reputation.changed` → `Game.Core/Contracts/Media/ReputationChanged.cs`
