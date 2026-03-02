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
- `core.guild.officer.assigned` → `Game.Core/Contracts/Guild/GuildOfficerAssigned.cs`
- `core.guild.officer.revoked` → `Game.Core/Contracts/Guild/GuildOfficerRevoked.cs`

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

### Supplemental Contracts (DTO / Store / Utility)

- `Game.Core/Contracts/Achievements/AchievementStateSnapshot.cs`
- `Game.Core/Contracts/Achievements/AchievementStateSnapshotMigration.cs`
- `Game.Core/Contracts/Achievements/IAchievementStateStore.cs`
- `Game.Core/Contracts/Content/NpcGuildArchetypesLoaded.cs`
- `Game.Core/Contracts/EventTypes.cs`
- `Game.Core/Contracts/Events/EventTypeRules.cs`
- `Game.Core/Contracts/Persistence/AutoSaveCompleted.cs`
- `Game.Core/Contracts/Persistence/AutoSaveDisabled.cs`
- `Game.Core/Contracts/Persistence/AutoSaveEnabled.cs`
- `Game.Core/Contracts/Persistence/SaveDeleted.cs`
- `Game.Core/Contracts/Progression/ExperienceSnapshotPayload.cs`
- `Game.Core/Contracts/Security/SecurityAiLogPopupGateDecision.cs`
- `Game.Core/Contracts/Security/SecuritySnapshotGateDecision.cs`
- `Game.Core/Contracts/State/GameStateUpdated.cs`
- `Game.Core/Contracts/UI/UiMenuEventTypes.cs`

<!-- GENERATED_CONTRACTS_START -->

## 领域事件明细（自动生成，避免漂移）

> 说明：本段由脚本从 `Game.Core/Contracts/**` 中提取 `EventType` 常量与 record 参数生成。

### AI

- **core.ai.cycle.completed**
  - Trigger: Emitted when an AI simulation cycle finishes.
  - Fields: SaveId, Week, IntentsIssued, CompletedAt
  - Contract: `Game.Core/Contracts/AI/AiCycleCompleted.cs`

- **core.ai.cycle.started**
  - Trigger: Emitted when an AI simulation cycle begins.
  - Fields: SaveId, Week, StartedAt
  - Contract: `Game.Core/Contracts/AI/AiCycleStarted.cs`

- **core.ai.ecosystem.step.completed**
  - Trigger: Emitted when an AI ecosystem step completes for the current week.
  - Fields: SaveId, Week, Summary, CompletedAt
  - Contract: `Game.Core/Contracts/AI/AiEcosystemStepCompleted.cs`

- **core.ai.intent.issued**
  - Trigger: Emitted when the AI issues an intent for downstream consumers.
  - Fields: SaveId, Week, IntentId, IntentType, ActorId, TargetId, IssuedAt
  - Contract: `Game.Core/Contracts/AI/AiIntentIssued.cs`

### Combat

- **core.player.damaged**
  - Trigger: Emitted when a player takes damage in combat.
  - Fields: PlayerId, Amount, DamageType, IsCritical, Timestamp
  - Contract: `Game.Core/Contracts/Combat/PlayerDamaged.cs`

### Content

- **core.content.manifest.loaded**
  - Trigger: Emitted when the content manifest is successfully loaded and validated.
  - Fields: ManifestId, SchemaVersion, EntryCount, LoadedAt
  - Contract: `Game.Core/Contracts/Content/ContentManifestLoaded.cs`

### Contracts

- **core.security.file_access.denied**
  - Trigger: Emitted when file path validation denies access.
  - Fields: Type, Source, Data, Timestamp, Id, "1.0", Target, Reason, OccurredAt, Caller
  - Contract: `Game.Core/Contracts/DomainEvent.cs`

### Engine

- **core.game.ended**
  - Trigger: Published when the game session ends
  - Fields: Score
  - Contract: `Game.Core/Contracts/Engine/GameEnded.cs`

- **core.game.started**
  - Trigger: Published when a new game session begins
  - Fields: StateId
  - Contract: `Game.Core/Contracts/Engine/GameStarted.cs`

- **core.player.health.changed**
  - Trigger: Published when player health changes due to damage or healing
  - Fields: Health, Delta
  - Contract: `Game.Core/Contracts/Engine/PlayerHealthChanged.cs`

- **core.player.moved**
  - Trigger: Published when the player changes position
  - Fields: X, Y
  - Contract: `Game.Core/Contracts/Engine/PlayerMoved.cs`

- **core.score.changed**
  - Trigger: Published when player score changes
  - Fields: Score, Added
  - Contract: `Game.Core/Contracts/Engine/ScoreChanged.cs`

### Events

- **core.event_catalog.loaded**
  - Trigger: Emitted when the event catalog definitions (events/chains) are loaded and validated.
  - Fields: CatalogId, SchemaVersion, EventDefinitionCount, EventChainCount, LoadedAt
  - Contract: `Game.Core/Contracts/Events/EventCatalogLoaded.cs`

### GameLoop

- **core.game_turn.phase_changed**
  - Trigger: Indicates that the game turn phase changed within the same week.
  - Fields: SaveId, Week, PreviousPhase, CurrentPhase, ChangedAt
  - Contract: `Game.Core/Contracts/GameLoop/GameTurnPhaseChanged.cs`

- **core.game_turn.started**
  - Trigger: Represents the start of a game turn for a given save and week.
  - Fields: SaveId, Week, Phase, StartedAt
  - Contract: `Game.Core/Contracts/GameLoop/GameTurnStarted.cs`

- **core.game_turn.week_advanced**
  - Trigger: Signals that the game loop advanced from one week to the next.
  - Fields: SaveId, PreviousWeek, CurrentWeek, AdvancedAt
  - Contract: `Game.Core/Contracts/GameLoop/GameWeekAdvanced.cs`

### Guild

- **core.guild.created**
  - Trigger: Emitted when a new guild is created.
  - Fields: GuildId, CreatorId, GuildName, CreatedAt
  - Contract: `Game.Core/Contracts/Guild/GuildCreated.cs`

- **core.guild.disbanded**
  - Trigger: Emitted when a guild is disbanded.
  - Fields: GuildId, DisbandedByUserId, DisbandedAt, Reason
  - Contract: `Game.Core/Contracts/Guild/GuildDisbanded.cs`

- **core.guild.member.joined**
  - Trigger: Emitted when a user joins a guild.
  - Fields: UserId, GuildId, JoinedAt, admin
  - Contract: `Game.Core/Contracts/Guild/GuildMemberJoined.cs`

- **core.guild.member.left**
  - Trigger: Emitted when a user leaves or is removed from a guild.
  - Fields: UserId, GuildId, LeftAt, Reason
  - Contract: `Game.Core/Contracts/Guild/GuildMemberLeft.cs`

- **core.guild.member.role_changed**
  - Trigger: Emitted when a guild member role is changed.
  - Fields: UserId, GuildId, OldRole, NewRole, ChangedAt, ChangedByUserId
  - Contract: `Game.Core/Contracts/Guild/GuildMemberRoleChanged.cs`

### Media

- **core.media.beat.triggered**
  - Trigger: Emitted when a media beat is triggered by upstream gameplay.
  - Fields: BeatId, GuildId, SourceEventType, Headline, TriggeredAt
  - Contract: `Game.Core/Contracts/Media/MediaBeatTriggered.cs`

- **core.reputation.changed**
  - Trigger: Emitted when a guild reputation value changes.
  - Fields: GuildId, OldValue, NewValue, Reason, ChangedAt
  - Contract: `Game.Core/Contracts/Media/ReputationChanged.cs`

### Persistence

- **core.load.completed**
  - Trigger: Emitted when a load operation completes successfully.
  - Fields: SaveId, CompletedAt
  - Contract: `Game.Core/Contracts/Persistence/LoadCompleted.cs`

- **core.load.failed**
  - Trigger: Emitted when a load operation fails.
  - Fields: SaveId, FailedAt, Reason
  - Contract: `Game.Core/Contracts/Persistence/LoadFailed.cs`

- **core.load.requested**
  - Trigger: Emitted when a load operation is requested.
  - Fields: SaveId, RequestedAt
  - Contract: `Game.Core/Contracts/Persistence/LoadRequested.cs`

- **core.save.completed**
  - Trigger: Emitted when a save operation completes successfully.
  - Fields: SaveId, CompletedAt
  - Contract: `Game.Core/Contracts/Persistence/SaveCompleted.cs`

- **core.save.failed**
  - Trigger: Emitted when a save operation fails.
  - Fields: SaveId, FailedAt, Reason
  - Contract: `Game.Core/Contracts/Persistence/SaveFailed.cs`

- **core.save.format.migration.applied**
  - Trigger: Emitted when a save-file format migration is applied successfully.
  - Fields: SaveId, FromVersion, ToVersion, AppliedAt
  - Contract: `Game.Core/Contracts/Persistence/SaveFormatMigrationApplied.cs`

- **core.save.requested**
  - Trigger: Emitted when a save operation is requested.
  - Fields: SaveId, RequestedAt
  - Contract: `Game.Core/Contracts/Persistence/SaveRequested.cs`

### Raid

- **core.raid.resolved**
  - Trigger: Emitted when a raid encounter is resolved (success/failure).
  - Fields: RaidId, GuildId, Week, Result, RewardPoints, ResolvedAt
  - Contract: `Game.Core/Contracts/Raid/RaidResolved.cs`

- **core.raid.scheduled**
  - Trigger: Emitted when a raid encounter is scheduled for a given week.
  - Fields: RaidId, GuildId, Week, EncounterId, ScheduledAt
  - Contract: `Game.Core/Contracts/Raid/RaidScheduled.cs`

### Recruitment

- **core.recruitment.offer.presented**
  - Trigger: Emitted when a recruitment offer is presented to the player guild.
  - Fields: OfferId, GuildId, CandidateId, Role, PresentedAt
  - Contract: `Game.Core/Contracts/Recruitment/RecruitmentOfferPresented.cs`

- **core.recruitment.offer.resolved**
  - Trigger: Emitted when a recruitment offer is accepted/rejected/expired.
  - Fields: OfferId, GuildId, CandidateId, Decision, Reason, ResolvedAt
  - Contract: `Game.Core/Contracts/Recruitment/RecruitmentOfferResolved.cs`

### Security

- **security.raid_encounter_demo.decision**
  - Trigger: Emitted when the Raid Encounter demo gate evaluates allow/deny/error.
  - Fields: Target, Decision, Reason, OccurredAt, Caller
  - Contract: `Game.Core/Contracts/Security/SecurityDemoGateDecision.cs`

### Social

- **core.social.interaction.triggered**
  - Trigger: Emitted when a social interaction is triggered between two actors.
  - Fields: InteractionId, GuildId, ActorId, TargetId, InteractionType, TriggeredAt
  - Contract: `Game.Core/Contracts/Social/SocialInteractionTriggered.cs`

- **core.social.relationship.changed**
  - Trigger: Emitted when a relationship value changes.
  - Fields: GuildId, SubjectId, OtherId, OldValue, NewValue, ChangedAt
  - Contract: `Game.Core/Contracts/Social/SocialRelationshipChanged.cs`

## DTO / Schema（自动生成，避免漂移）

> 说明：本段列出 Phase2 新增的内容/事件 Schema（不包含 EventType 常量）。

- `Game.Core/Contracts/Content/ContentManifest.cs`
- `Game.Core/Contracts/Content/ContentManifestEntry.cs`
- `Game.Core/Contracts/Events/EventCatalogDefinition.cs`
- `Game.Core/Contracts/Events/EventChainDefinition.cs`
- `Game.Core/Contracts/Events/EventDefinition.cs`

<!-- GENERATED_CONTRACTS_END -->

### Activity

- `core.activity.feed.appended` → `Game.Core/Contracts/Activity/ActivityFeedAppended.cs`

### WorldBoss

- `core.worldboss.entered` → `Game.Core/Contracts/WorldBoss/WorldBossEntered.cs`
- `core.worldboss.resolved` → `Game.Core/Contracts/WorldBoss/WorldBossResolved.cs`

### Pvp

- `core.pvp.match.started` → `Game.Core/Contracts/Pvp/PvpMatchStarted.cs`
- `core.pvp.match.resolved` → `Game.Core/Contracts/Pvp/PvpMatchResolved.cs`
