---
PRD-ID: PRD-Guild-Manager
PRD-Refs:
  - docs/prd.txt
Story-ID: PRD-GUILD-MANAGER-CORE-GAME-LOOP
Title: Feature Slice - Game Loop Core（回合循环与时间推进）
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

本页作为“回合循环与时间推进（Game Loop）”纵切的审计锚点。

## 契约（领域事件 type）

来自当前 `tasks_gameplay.json` 的 `contractRefs`：

- `core.game_turn.started`
- `core.game_turn.phase_changed`
- `core.game_turn.week_advanced`

契约索引与定义位置：`08-Contracts-Index.md`、`08-Contracts-CloudEvents-Core.md`。

## 验收与测试（规则）

- 回合推进必须是确定性的：相同输入产生相同事件序列（Core 层可用 xUnit 固化）。
- 当本纵切进入交付阶段并被门禁校验时，必须补齐 Test-Refs 指向真实测试文件。
