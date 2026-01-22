---
PRD-ID: PRD-Guild-Manager
Title: 08 Phase 2：内容加载与 JSON 清单接入（Assets/Data）
Status: Draft
ADR-Refs:
  - ADR-0005
  - ADR-0011
  - ADR-0019
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

- `T27`（见 `.taskmaster/tasks/tasks.json`）

## 事件与契约（ADR-0004）

- 事件类型与触发时机以 `Game.Core/Contracts/**` 为准；本页仅提供索引与口径说明。

## 验收与证据链（Draft）

- 本页为 Draft：当对应任务进入实现阶段时，将通过 view 任务 `acceptance[]` 的 `Refs:` 与测试文件内 `ACC:T<id>.<n>` anchors 建立确定性证据链。

## 备注

- 将 res://Game.Godot/Assets/Data/content/base/manifest.json 作为入口清单（不复制 PRD）。
- Contracts 仍以 Game.Core/Contracts/** 为 SSoT，页面只写事件类型与文件路径。

<!-- PHASE2_CONTRACTS_SECTION -->
## 契约定义（Phase2）

### 事件 / DTO
- `Game.Core/Contracts/Content/ContentManifest.cs`
- `Game.Core/Contracts/Content/ContentManifestEntry.cs`
- `Game.Core/Contracts/Content/ContentManifestLoaded.cs`

### 接口契约
