---
PRD-ID: PRD-Guild-Manager
PRD-Refs:
  - docs/prd.txt
Story-ID: PRD-GUILD-MANAGER
Title: Feature Slice - Guild Manager（功能纵切总览）
Status: Active
ADR-Refs:
  - ADR-0018
  - ADR-0019
  - ADR-0004
  - ADR-0005
  - ADR-0023
  - ADR-0011
Test-Refs:
  # Core 领域逻辑（xUnit）
  - Game.Core.Tests/Domain/GuildCoreTests.cs
  - Game.Core.Tests/Domain/GuildMemberTests.cs
  - Game.Core.Tests/Domain/GameTurnSystemTests.cs
  - Game.Core.Tests/Domain/EventEngineTests.cs
  - Game.Core.Tests/Domain/GameLoopTests.cs
  # 场景与公会 UI（GdUnit4）
  - Tests.Godot/tests/Scenes/test_main_scene_smoke.gd
  - Tests.Godot/tests/Scenes/Guild/T2PlayableSceneTests.gd
  - Tests.Godot/tests/Integration/test_guild_vertical_slice.gd
Artifact-Refs:
  # 产物/门禁锚点：允许占位；不参与“测试文件必须存在”的硬规则
  - logs/ci/<YYYY-MM-DD>/ci-pipeline-summary.json
  - logs/e2e/<YYYY-MM-DD>/smoke/selfcheck-summary.json
---

本页为 PRD-Guild-Manager 的“功能纵切总览”审计锚点，用于约束：

- 纵切页面如何落盘到 `docs/architecture/overlays/PRD-Guild-Manager/08/`
- 契约（领域事件/DTO/接口）如何落盘到 `Game.Core/Contracts/**`（per ADR-0020）
- 视图任务（`tasks_back.json`/`tasks_gameplay.json`）如何引用 Overlay（不把 `.taskmaster/tasks/tasks_newguild.json` 当门禁输入）

## 跨切面口径（只引用，不复制）

- 安全：Base/CH02 + ADR-0019
- 事件与契约：Base/CH01/CH03 + ADR-0004
- 质量门禁：ADR-0005
- 性能预算：ADR-0015（如涉及）

## 纵切拆分建议（与视图任务 story_id 对齐）

为避免所有任务都指向同一个页面导致审计失真，建议按 story_id 拆页（本目录已预置索引页）：

- Core
  - `08-FeatureSlice-Core-Event-Engine.md`（PRD-GUILD-MANAGER-CORE-EVENT-ENGINE）
  - `08-FeatureSlice-Core-Game-Loop.md`（PRD-GUILD-MANAGER-CORE-GAME-LOOP）
  - `08-FeatureSlice-Core-AI-Coordinator.md`（PRD-GUILD-MANAGER-CORE-AI-COORDINATOR）
- T3
  - `08-FeatureSlice-T3-Member-Management.md`（PRD-GUILD-MANAGER-T3-MEMBER-MANAGEMENT）
  - `08-FeatureSlice-T3-Recruitment.md`（PRD-GUILD-MANAGER-T3-RECRUITMENT）
  - `08-FeatureSlice-T3-AI-Ecosystem.md`（PRD-GUILD-MANAGER-T3-AI-ECOSYSTEM）
  - `08-FeatureSlice-T3-PVE-Raid.md`（PRD-GUILD-MANAGER-T3-PVE-RAID）
  - `08-FeatureSlice-T3-Social.md`（PRD-GUILD-MANAGER-T3-SOCIAL）
  - `08-FeatureSlice-T3-Media-Reputation.md`（PRD-GUILD-MANAGER-T3-MEDIA）
  - `08-FeatureSlice-T3-SaveLoad-UI.md`（PRD-GUILD-MANAGER-T3-SAVELOAD-UI）

## 契约引用示例（已存在）

- `Game.Core/Contracts/Guild/GuildMemberJoined.cs`

## 变更规则（止损）

如本模块引入新的领域事件/DTO/接口：

1. 先落盘到 `Game.Core/Contracts/<Module>/**`（强类型，禁止 Godot 依赖）。
2. 再在 Overlay/08 对应纵切页登记“影响范围 + 验收/Test-Refs 挂钩”，并通过 `validate_contracts.py` 校验回链。
