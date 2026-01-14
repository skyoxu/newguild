---
PRD-ID: PRD-Guild-Manager
PRD-Refs:
  - docs/prd.txt
Story-ID: PRD-GUILD-MANAGER-T3-MEMBER-MANAGEMENT
Title: Feature Slice - T3 Member Management（成员管理 / Roster）
Status: Planned
ADR-Refs:
  - ADR-0004
  - ADR-0005
  - ADR-0007
  - ADR-0018
Arch-Refs:
  - CH01
  - CH04
  - CH05
  - CH06
---

本页作为 T3“成员管理/roster”纵切的审计锚点。

## 契约（领域事件 type）

来自当前 `tasks_gameplay.json` 的 `contractRefs`：

- `core.guild.created`
- `core.guild.disbanded`
- `core.guild.member.joined`
- `core.guild.member.left`
- `core.guild.member.role_changed`

契约索引与定义位置：`08-Contracts-Index.md`、`08-Contracts-Guild-Manager-Events.md`。

## 交付规则（止损）

- 领域事件必须有强类型契约落盘到 `Game.Core/Contracts/**`，并在实现后补齐 Contracts-Refs/Test-Refs。
- UI 层只订阅事件并刷新展示，禁止直接修改 Core 状态。
