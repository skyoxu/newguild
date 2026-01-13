---
PRD-ID: PRD-Guild-Manager
PRD-Refs:
  - docs/prd.txt
Story-ID: PRD-GUILD-MANAGER-CORE-EVENT-ENGINE
Title: Feature Slice - Event Engine Core（事件引擎核心）
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

本页作为“事件引擎核心（Event Engine Core）”纵切的审计锚点，用于后续提任务接线时对齐契约与验收。

## 契约（领域事件 type）

来自当前 `tasks_gameplay.json` 的 `contractRefs`：

- `core.guild.created`
- `core.guild.member.joined`
- `core.guild.member.left`

契约索引与定义位置：`08-Contracts-Index.md`、`08-Contracts-Guild-Manager-Events.md`。

## 验收与测试（规则）

- 事件发布遵循 ADR-0004：type 命名稳定、强类型契约落盘、禁止 UI 层自造字符串契约。
- 当本纵切进入交付阶段并被门禁校验时，必须补齐 Test-Refs 指向真实测试文件（xUnit/GdUnit4）。
