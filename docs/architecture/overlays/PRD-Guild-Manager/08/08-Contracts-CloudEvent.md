---
PRD-ID: PRD-Guild-Manager
Title: CloudEvent 契约更新（事件封装与字段）
Arch-Refs:
  - CH01
  - CH03
ADR-Refs:
  - ADR-0004
  - ADR-0005
Test-Refs:
  - Game.Core.Tests/Contracts/DomainEventTests.cs
Contracts-Refs:
  - Game.Core/Contracts/DomainEvent.cs
Status: Proposed
---

本页为功能纵切（08 章）对应“CloudEvent”契约更新的记录与验收口径。

变更意图（引用，不复制口径）

- 统一事件封装与字段必填校验（见 ADR‑0004）；保持跨模块一致的事件命名规范 `${DOMAIN_PREFIX}.<entity>.<action>`。
- 质量门禁引用 ADR‑0005，相关测试与校验在 CI 执行。

影响范围

- 合同文件：`Game.Core/Contracts/DomainEvent.cs`
- 受影响模块：事件总线发布/订阅、日志关联与可观测性埋点

验收要点（就地）

 - 单测覆盖基本构造与字段校验；E2E 占位用例存在（见 Test-Refs）

示例：当前 Godot+C# 契约引用

- 公会成员加入事件契约：`Game.Core/Contracts/Guild/GuildMemberJoined.cs`（per ADR-0020）
