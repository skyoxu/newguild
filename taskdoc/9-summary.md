# Task 9 上下文汇总

## Task 9 核心目标（来自 tasks.json / tasks_back.json）
- 以 ADR-0019 为基线，梳理 GameLoop 安全审计（F001–F007），区分 T2_BLOCKING 与 NG_BACKLOG。
- 让 GameLoop 相关任务的 ADR/Chapter/Test-Refs 映射收口，确保 `py -3 scripts/python/task_links_validate.py` 不再因缺失回链报错。

## 事件/契约现状（面向 ADR-0004）
- `core.game_turn.*`：已在 `Game.Core/Contracts/GameLoop/**` 存在强类型合同（含 `EventType` 常量）。
- `security.*.denied`：当前在 `Game.Core/Services/Security*Adapter.cs` 内以字符串 `Type` 构造 `DomainEvent`，未在 `Game.Core/Contracts/**` 找到对应强类型合同（这对“契约对齐/收口”是一个明显风险点）。

## 事件总线与消费链（面向实现/审计）
- `IEventBus` 定义：`Game.Core/Services/EventBus.cs`。
- Godot 适配层实现：`Game.Godot/Adapters/EventBusAdapter.cs`（Node + IEventBus）。
- 审计落盘：`Game.Godot/Adapters/SecurityAuditLogger.cs` 写 `user://logs/security-audit.jsonl`。

## 本次 Serena 上下文文件（UTF-8）
- `taskdoc/9-1-find_symbol.txt`
- `taskdoc/9-2-search_interfaces.txt`
- `taskdoc/9-3-find_event_contracts.txt`
- `taskdoc/9-4a-find_references-IEventBus.txt`
- `taskdoc/9-4b-find_references-GameTurnStarted.txt`
- `taskdoc/9-4c-find_references-SecurityAdapters.txt`

## 下一步（仅建议，不做实现）
- 如果 Task 9 需要把 `security.*.denied` 纳入 Contracts SSoT，建议先对照 ADR-0004 统一事件结构/命名，再决定是否新增强类型 contracts（否则映射收口会长期依赖“字符串约定”）。
