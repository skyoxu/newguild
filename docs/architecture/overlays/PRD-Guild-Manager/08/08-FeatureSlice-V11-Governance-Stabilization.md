---
PRD-ID: PRD-Guild-Manager
Title: V1.1 Governance & Stabilization Slice
Status: Active
Arch-Refs:
  - CH01
  - CH03
  - CH07
  - CH09
  - CH10
  - CH11
ADR-Refs:
  - ADR-0003
  - ADR-0004
  - ADR-0005
  - ADR-0006
  - ADR-0015
Test-Refs:
  - Game.Core.Tests/Tasks/Task52GovernanceTests.cs
  - Game.Core.Tests/CI/ActivityFeedArtifactsTests.cs
  - Tests.Godot/tests/Playability/Phase2/test_phase2_play_route.gd
  - Tests.Godot/tests/UI/test_activity_feed_scene.gd
  - Tests.Godot/tests/Integration/test_backup_restore_savegame.gd
  - Tests.Godot/tests/Playability/Phase2/test_phase2_raid_media_rep.gd
  - Game.Core.Tests/Persistence/Migrations/GuildDbSchemaTests.cs
  - Game.Core.Tests/Contracts/Achievements/AchievementStateSnapshotMigrationTests.cs
  - Game.Core.Tests/Persistence/SaveLoad/SaveLoadRoundTripTests.cs
  - Game.Core.Tests/Persistence/SaveLoad/WorldGenSaveLoadReplayTests.cs
  - Tests.Godot/tests/Adapters/Db/test_savegame_persistence_cross_restart.gd
  - Tests.Godot/tests/Integration/test_guild_vertical_slice.gd
---

# V1.1 Governance & Stabilization Slice

## Scope

- Task range: `T53-T102` (governance/security/data subset only)
- View routing rule: governance/security tasks stay in `tasks_back.json`
- Security profile baseline: `SECURITY_PROFILE=host-safe`

## Execution Order

T53, T54, T55, T56, T57, T61, T62, T63, T64, T65, T66, T79, T80

## Grouped Topics

| 主题 | 阶段任务 | 关键 Overlay |
|---|---|---|
| V1.1 治理：稳定性门禁基线统一 | T53 | docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md |
| V1.1 治理：Headless Flaky 采集与根因分类 | T54 | docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md |
| V1.1 治理：测试编排与证据消费规范 | T55 | docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md |
| V1.1 治理：性能预算硬门与场景基准 | T56 | docs/architecture/overlays/PRD-Guild-Manager/08/_index.md |
| V1.1 安全治理：Release Health 门禁固化 | T57 | docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md |
| V1.1 数据治理：schemaVersion 与迁移矩阵 | T61 | docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md |
| V1.1 数据治理：跨版本存档回放与损坏恢复 | T62, T79 | docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md |
| V1.1 治理：观测与审计日志字段统一 | T63 | docs/architecture/overlays/PRD-Guild-Manager/08/_index.md |
| V1.1 治理：CI 诊断摘要与止损模板 | T64 | docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md |
| V1.1 治理：Task/ADR/Overlay/Test-Refs 自动体检 | T65 | docs/architecture/overlays/PRD-Guild-Manager/08/_index.md |
| V1.1 收口：RC 准入评审包 | T66, T80 | docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md |

## Acceptance Focus

- Deterministic gate evidence under `logs/ci/**` and `logs/perf/**`
- Stable task refs chain (ADR/CH/Overlay/Test-Refs) for each phase task
- Release health and schema evolution checks remain hard-gated

## Notes

- This page is a V1.1 governance index page and should be referenced by `_index.md` and `ACCEPTANCE_CHECKLIST.md`.
