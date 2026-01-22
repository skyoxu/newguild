---
PRD-ID: PRD-Guild-Manager
Title: 08 Phase 2：官员系统（Officer Slots + UI Entry）
Status: Draft
ADR-Refs:
  - ADR-0005
  - ADR-0006
  - ADR-0018
Arch-Refs:
  - CH01
  - CH05
  - CH06
  - CH07
---

## 范围与非目标（止损）

- 范围：仅覆盖 Phase 2 的“内容驱动 + UI 入口”相关纵切；不替代 PRD/Tasks。
- 非目标：不复制 Base/ADR 阈值，不在文档复制 Contracts 字段定义。

## 关联任务（SSoT）

- `T38`（见 `.taskmaster/tasks/tasks.json`）
- `T39`（见 `.taskmaster/tasks/tasks.json`）

## 事件与契约（ADR-0004）

- 事件类型与触发时机以 `Game.Core/Contracts/**` 为准；本页仅提供索引与口径说明。

## 验收与证据链（Draft）

- 本页为 Draft：当对应任务进入实现阶段时，将通过 view 任务 `acceptance[]` 的 `Refs:` 与测试文件内 `ACC:T<id>.<n>` anchors 建立确定性证据链。

## 备注

- 官员数据与规则在 Core；UI 仅调用服务与展示，不直接写 SQL。
- 需随 Save/Load 持久化，避免新入口不可回放。
