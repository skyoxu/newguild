---
PRD-ID: PRD-Guild-Manager
Title: 08 Phase 2：内容驱动事件（EventCatalog + EventEngine）与命名收口
Status: Draft
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

## 范围与非目标（止损）

- 范围：仅覆盖 Phase 2 的“内容驱动 + UI 入口”相关纵切；不替代 PRD/Tasks。
- 非目标：不复制 Base/ADR 阈值，不在文档复制 Contracts 字段定义。

## 关联任务（SSoT）

- `T28`（见 `.taskmaster/tasks/tasks.json`）
- `T29`（见 `.taskmaster/tasks/tasks.json`）
- `T42`（见 `.taskmaster/tasks/tasks.json`）

## 事件与契约（ADR-0004）

- 事件类型与触发时机以 `Game.Core/Contracts/**` 为准；本页仅提供索引与口径说明。

## 验收与证据链（Draft）

- 本页为 Draft：当对应任务进入实现阶段时，将通过 view 任务 `acceptance[]` 的 `Refs:` 与测试文件内 `ACC:T<id>.<n>` anchors 建立确定性证据链。

## 备注

- 禁止引入第二套事件引擎：只扩展现有 EventEngine/GameTurnSystem 的输入与驱动方式。
- 事件命名统一迁移（core.*.*）作为止损前置，避免 Phase2 新内容继续使用旧名。

<!-- PHASE2_CONTRACTS_SECTION -->
## 契约定义（Phase2）

### 事件 / DTO
- `Game.Core/Contracts/Events/EventCatalogDefinition.cs`
- `Game.Core/Contracts/Events/EventDefinition.cs`
- `Game.Core/Contracts/Events/EventChainDefinition.cs`
- `Game.Core/Contracts/Events/EventCatalogLoaded.cs`

### 接口契约
- `Game.Core/Ports/IEventCatalog.cs`
