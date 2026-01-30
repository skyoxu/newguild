---
PRD-ID: PRD-Guild-Manager
Title: 08 Phase 2：战术中心入口与奖励闭环（Tactical + Rewards + XP + Achievements）
Status: Draft
ADR-Refs:
  - ADR-0004
  - ADR-0005
  - ADR-0006
  - ADR-0007
  - ADR-0018
Arch-Refs:
  - CH01
  - CH04
  - CH05
  - CH06
  - CH07
---

## 范围与非目标（止损）

- 范围：仅覆盖 Phase 2 的“内容驱动 + UI 入口”相关纵切；不替代 PRD/Tasks。
- 非目标：不复制 Base/ADR 阈值，不在文档复制 Contracts 字段定义。

## 关联任务（SSoT）

- `T34`（见 `.taskmaster/tasks/tasks.json`）
- `T35`（见 `.taskmaster/tasks/tasks.json`）
- `T37`（见 `.taskmaster/tasks/tasks.json`）
- `T36`（见 `.taskmaster/tasks/tasks.json`）

## 事件与契约（ADR-0004）

- 事件类型与触发时机以 `Game.Core/Contracts/**` 为准；本页仅提供索引与口径说明。


## 契约定义（索引）

### 事件
- **ExperienceChanged** (`core.experience.changed`)
  - 触发时机：经验值变更（例如 raid/media/recruitment 结果结算后）。
  - 字段：`GuildId`, `TotalExperience`, `Delta`, `Level`, `SourceEventType`, `ChangedAt`
  - 契约位置：`Game.Core/Contracts/Progression/ExperienceChanged.cs`
- **LevelChanged** (`core.level.changed`)
  - 触发时机：等级提升或回退时触发。
  - 字段：`GuildId`, `OldLevel`, `NewLevel`, `TotalExperience`, `SourceEventType`, `ChangedAt`
  - 契约位置：`Game.Core/Contracts/Progression/LevelChanged.cs`

### DTO
- **AchievementCountChanged**
  - 用途：成就计数变更通知（Core → UI）
  - 字段：`UnlockedCount`, `TriggerEventType`
  - 契约位置：`Game.Core/Contracts/Achievements/AchievementCountChanged.cs`


## 验收与证据链（Draft）

- 本页为 Draft：当对应任务进入实现阶段时，将通过 view 任务 `acceptance[]` 的 `Refs:` 与测试文件内 `ACC:T<id>.<n>` anchors 建立确定性证据链。

## 备注

- 奖励发放统一：若没有其他任务负责战斗奖励发放，本阶段在 Reward Ledger 完成。
- 战术中心只做统一入口与最小编成/校验，驱动现有 PVE/Raid demo 与事件输出。

## Test-Refs

- `Game.Core.Tests/Progression/RewardLedgerServiceTests.cs`
- `Game.Core.Tests/Progression/RewardLedgerTests.cs`
- `Game.Core.Tests/CI/ArtifactsLoggingTests.cs`
- `Game.Core.Tests/Domain/Achievements/AchievementTrackerTests.cs`
- `Tests.Godot/tests/UI/test_achievements_smoke.gd`
- `Tests.Godot/tests/UI/test_hud_achievements_displays_unlocked.gd`
