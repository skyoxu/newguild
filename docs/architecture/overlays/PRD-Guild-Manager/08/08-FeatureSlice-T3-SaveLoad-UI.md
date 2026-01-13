---
PRD-ID: PRD-Guild-Manager
PRD-Refs:
  - docs/prd.txt
Story-ID: PRD-GUILD-MANAGER-T3-SAVELOAD-UI
Title: Feature Slice - T3 Save/Load UI Entry（菜单/调试入口）
Status: Planned
ADR-Refs:
  - ADR-0004
  - ADR-0005
  - ADR-0007
  - ADR-0018
  - ADR-0019
Arch-Refs:
  - CH02
  - CH04
  - CH05
  - CH06
---

本页作为 T3“Save/Load UI 入口”纵切的审计锚点。

## 契约（领域事件 type）

来自当前 `tasks_gameplay.json` 的 `contractRefs`：

- `core.save.requested`
- `core.save.completed`
- `core.save.failed`
- `core.load.requested`
- `core.load.completed`
- `core.load.failed`

契约索引与定义位置：`08-Contracts-Index.md`、`08-Contracts-CloudEvents-Core.md`、`08-Contracts-Security.md`。

## 验收与测试（规则）

- 错误信息对玩家侧必须脱敏（ADR-0019）；调试日志只能在 Debug/CI 编译启用。
- 当进入交付阶段时，必须补齐契约文件与测试挂钩。
