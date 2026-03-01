---
PRD-ID: PRD-Guild-Manager
PRD-Refs:
  - docs/prd.txt
Story-ID: PRD-GUILD-MANAGER-T3-PVE-RAID
Title: Feature Slice - T3 PVE Raid（副本/遭遇）
Status: In Progress
ADR-Refs:
  - ADR-0004
  - ADR-0005
  - ADR-0006
  - ADR-0018
Arch-Refs:
  - CH01
  - CH04
  - CH05
  - CH06
  - CH07
---

本页作为 T3“PVE 副本/遭遇”纵切的审计锚点。

## 契约（领域事件 type）

来自当前 `tasks_gameplay.json` 的 `contractRefs`：

- `core.raid.scheduled`
- `core.raid.resolved`
- `core.game_turn.week_advanced`

契约索引与定义位置：`08-Contracts-Index.md`、`08-Contracts-CloudEvents-Core.md`。

## 契约定义（规划）

### 事件

- **RaidScheduled** (`core.raid.scheduled`)
  - 触发时机：为某周安排一次副本/遭遇
  - 字段：`RaidId`, `GuildId`, `Week`, `EncounterId`, `ScheduledAt`
  - 契约位置：`Game.Core/Contracts/Raid/RaidScheduled.cs`
- **RaidResolved** (`core.raid.resolved`)
  - 触发时机：副本/遭遇结算完成（成功/失败）
  - 字段：`RaidId`, `GuildId`, `Week`, `Result`, `RewardPoints`, `ResolvedAt`
  - 契约位置：`Game.Core/Contracts/Raid/RaidResolved.cs`

## 验收与测试（T51 对齐）

- `ACC:T51.1`：Raid 流程必须形成 `scheduled -> resolved` 单次闭环，`success/failed` 可区分；进入终态后不可继续推进或重复结算。  
  Refs: `Game.Core.Tests/Services/RaidEncounterStateMachineTests.cs`、`Game.Core.Tests/Services/RaidEncounterDomainEventsTests.cs`
- `ACC:T51.2`：UI 入口触发一次真实遭遇后必须展示结果摘要；当 demo 关闭（如 `GD_ENABLE_PLAYABLE=0`）时不得出现 resolved 摘要。  
  Refs: `Tests.Godot/tests/UI/test_hud_raid_encounter_demo.gd`
- `ACC:T51.3`：Activity/Feed 必须观察到 `core.raid.resolved`，并校验该次 resolved 载荷含 `result` 字段且与同次 UI 反馈一致；单次触发不得产生重复 resolved。  
  Refs: `Tests.Godot/tests/Playability/Phase2/test_task51_raid_feedback_feed.gd`
- `ACC:T51.4`：必须产出可回放的 `logs/**` 证据，并校验 `action/reason/target/caller` 等审计字段完整。  
  Refs: `Tests.Godot/tests/UI/test_hud_raid_encounter_demo.gd`

## Test-Refs

- `Game.Core.Tests/Services/RaidEncounterStateMachineTests.cs`
- `Game.Core.Tests/Services/RaidEncounterDomainEventsTests.cs`
- `Tests.Godot/tests/UI/test_hud_raid_encounter_demo.gd`
- `Tests.Godot/tests/Playability/Phase2/test_task51_raid_feedback_feed.gd`
