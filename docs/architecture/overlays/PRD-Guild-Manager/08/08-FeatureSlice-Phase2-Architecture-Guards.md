---
PRD-ID: PRD-Guild-Manager
Title: 08 Phase 2：架构依赖护栏（Ports/Adapters）
Status: Draft
ADR-Refs:
  - ADR-0005
  - ADR-0007
  - ADR-0011
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

- `T43`（见 `.taskmaster/tasks/tasks.json`）

## 事件与契约（ADR-0004）

- 事件类型与触发时机以 `Game.Core/Contracts/**` 为准；本页仅提供索引与口径说明。

## 验收与证据链（Draft）

- 本页为 Draft：当对应任务进入实现阶段时，将通过 view 任务 `acceptance[]` 的 `Refs:` 与测试文件内 `ACC:T<id>.<n>` anchors 建立确定性证据链。

## 备注

- 在 Phase2 扩展中避免 Core <-> Godot API 互相侵入，提供依赖矩阵与脚本校验骨架。
