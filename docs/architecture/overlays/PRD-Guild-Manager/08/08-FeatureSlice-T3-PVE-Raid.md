---
PRD-ID: PRD-Guild-Manager
PRD-Refs:
  - docs/prd.txt
Story-ID: PRD-GUILD-MANAGER-T3-PVE-RAID
Title: Feature Slice - T3 PVE Raid（副本/遭遇）
Status: Planned
ADR-Refs:
  - ADR-0004
  - ADR-0005
  - ADR-0007
  - ADR-0018
Arch-Refs:
  - CH01
  - CH04
  - CH06
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

## 验收与测试（规则）

- 副本调度与结算必须通过事件驱动对外发布，支持 UI/媒体/声望等模块消费。
- 当进入交付阶段时，必须补齐 `Game.Core/Contracts/Raid/**` 与对应测试用例。
