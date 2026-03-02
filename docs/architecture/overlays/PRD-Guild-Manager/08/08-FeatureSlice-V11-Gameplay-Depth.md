---
PRD-ID: PRD-Guild-Manager
Title: V1.1 Gameplay Depth Slice
Status: Active
Arch-Refs:
  - CH01
  - CH04
  - CH05
  - CH06
  - CH07
  - CH09
ADR-Refs:
  - ADR-0004
  - ADR-0005
  - ADR-0006
  - ADR-0015
  - ADR-0018
Test-Refs:
  - Game.Core.Tests/Services/GuildRecruitmentServiceTests.cs
  - Game.Core.Tests/Services/RaidEncounterDomainEventsTests.cs
  - Tests.Godot/tests/Playability/Phase2/test_phase2_play_route.gd
  - Game.Core.Tests/Domain/GuildRosterServiceTests.cs
  - Game.Core.Tests/Services/RaidEncounterStateMachineTests.cs
  - Game.Core.Tests/Persistence/SaveLoad/SaveLoadRoundTripTests.cs
  - Game.Core.Tests/Domain/ReputationTests.cs
  - Tests.Godot/tests/Playability/Phase2/test_phase2_guild_vertical_slice.gd
  - Game.Core.Tests/CI/ActivityFeedArtifactsTests.cs
  - Tests.Godot/tests/UI/test_guild_panel_recruitment_apply_approve.gd
  - Tests.Godot/tests/Playability/Phase2/test_phase2_raid_media_rep.gd
  - Tests.Godot/tests/Integration/test_guild_vertical_slice.gd
  - Game.Core.Tests/Domain/GuildMemberTests.cs
  - Tests.Godot/tests/Playability/Phase2/test_task51_raid_feedback_feed.gd
  - Tests.Godot/tests/Playability/Phase2/test_phase2_save_load.gd
  - Tests.Godot/tests/UI/test_hud_reputation_display.gd
---

# V1.1 Gameplay Depth Slice

## Scope

- Task range: `T53-T102` (gameplay subset only)
- View routing rule: gameplay-functional tasks stay in `tasks_gameplay.json`
- Serial execution policy: one task at a time in strict order

## Execution Order

T58, T59, T60, T67, T68, T69, T70, T71, T72, T73, T74, T75, T76, T77, T78, T81, T82, T83, T84, T85, T86, T87, T88, T89, T90, T91, T92, T93, T94, T95, T96, T97, T98, T99, T100, T101, T102

## Grouped Topics

| 主题 | 阶段任务 | 关键 Overlay |
|---|---|---|
| V1.1 玩法：招募到入会闭环调参与反馈 | T58, T76 | docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Recruitment.md |
| V1.1 玩法：Raid 结果驱动成长与排名联动 | T59, T77 | docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-PVE-Raid.md |
| V1.1 玩法：两周循环与存档回放一致性 | T60, T78, T94 | docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Core-Game-Loop.md |
| V1.1 玩法深度：招募链路边界与异常分支 | T67, T81, T95 | docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Recruitment.md |
| V1.1 玩法深度：成员管理状态机与角色变更 | T68, T82, T96 | docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Member-Management.md |
| V1.1 玩法深度：Raid 进阶规则与结果可解释性 | T69, T83, T97 | docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-PVE-Raid.md |
| V1.1 玩法深度：经济与后勤联动闭环 | T70, T84, T98 | docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Tactical-Rewards.md |
| V1.1 玩法深度：媒体事件与声望反馈闭环 | T71, T85, T99 | docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Media-Reputation.md |
| V1.1 玩法扩展：WorldBoss 从入口到可玩闭环 | T72, T86, T87 | docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Guild-Manager.md |
| V1.1 玩法扩展：PVP 从入口到匹配结算 | T73, T88, T89 | docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Social.md |
| V1.1 平衡治理：玩法遥测与数值调参闭环 | T74, T90, T91 | docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Core-Observability.md |
| V1.1 收口：玩法全链路回归与发布前验收 | T75, T92, T93, T100, T101, T102 | docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md |

## Acceptance Focus

- End-to-end gameplay loops remain deterministic in headless replay
- Save/load continuity and event consistency across phased tasks
- Route-level artifacts under `logs/e2e/**` are replayable and traceable

## Notes

- This page is a V1.1 gameplay index page and should be referenced by `_index.md` and `ACCEPTANCE_CHECKLIST.md`.

## 契约定义（V1.1 Gameplay Depth 新增）

### 事件
- **ActivityFeedAppended** (`core.activity.feed.appended`)
  - 触发时机：关键玩法事件提交后写入活动流。
  - 字段：`FeedEntryId`, `GuildId`, `SourceEventType`, `Message`, `AppendedAt`
  - 契约位置：`Game.Core/Contracts/Activity/ActivityFeedAppended.cs`
- **WorldBossEntered** (`core.worldboss.entered`)
  - 触发时机：公会进入 WorldBoss 周常遭遇战。
  - 字段：`EncounterId`, `GuildId`, `Week`, `EnteredAt`
  - 契约位置：`Game.Core/Contracts/WorldBoss/WorldBossEntered.cs`
- **WorldBossResolved** (`core.worldboss.resolved`)
  - 触发时机：WorldBoss 战斗结算完成并产出奖励。
  - 字段：`EncounterId`, `GuildId`, `Week`, `Result`, `RewardPoints`, `ResolvedAt`
  - 契约位置：`Game.Core/Contracts/WorldBoss/WorldBossResolved.cs`
- **PvpMatchStarted** (`core.pvp.match.started`)
  - 触发时机：PVP 匹配确认后进入对局。
  - 字段：`MatchId`, `GuildId`, `OpponentGuildId`, `Week`, `StartedAt`
  - 契约位置：`Game.Core/Contracts/Pvp/PvpMatchStarted.cs`
- **PvpMatchResolved** (`core.pvp.match.resolved`)
  - 触发时机：PVP 对局终态结算并计算积分变化。
  - 字段：`MatchId`, `GuildId`, `OpponentGuildId`, `Result`, `RatingDelta`, `ResolvedAt`
  - 契约位置：`Game.Core/Contracts/Pvp/PvpMatchResolved.cs`
