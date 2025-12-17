---
PRD-ID: PRD-Guild-Manager
Title: 安全事件与策略契约更新（Security Contracts）
Arch-Refs:
  - CH01
  - CH03
ADR-Refs:
  - ADR-0019
  - ADR-0004
  - ADR-0005
Test-Refs:
  - Game.Core.Tests/Security/SecurityAdapterTests.cs
  - Tests.Godot/tests/Security/Hard/test_url_security_adapter.gd
  - Tests.Godot/tests/Security/Hard/test_file_security_adapter.gd
  - Tests.Godot/tests/Security/Hard/test_process_security_adapter.gd
  - Tests.Godot/tests/Security/Hard/test_security_integration.gd
  - Tests.Godot/tests/Security/Hard/test_audit_log_compliance.gd
Contracts-Refs:
  - Game.Core/Interfaces/ISecurityUrlValidator.cs
  - Game.Core/Interfaces/ISecurityFileValidator.cs
  - Game.Core/Interfaces/ISecurityProcessValidator.cs
  - Game.Core/Domain/SafeResourcePath.cs
  - Game.Core/Services/SecurityUrlAdapter.cs
  - Game.Core/Services/SecurityProcessAdapter.cs
  - Game.Godot/Adapters/Security/SecurityUrlAdapter.cs
  - Tests.Godot/Game.Godot/Adapters/Security/SecurityFileAdapter.cs
  - Tests.Godot/Game.Godot/Adapters/Security/SecurityProcessAdapter.cs
Status: Proposed
---

本页为功能纵切（08 章）对应“Security Contracts”变更登记与验收要点（仅引用 01/02/03 章口径，不复制阈值/策略）。

变更意图（引用）

- Godot 安全基线：见 ADR‑0019（资源与文件访问、外链与权限约束）；
- 契约与事件统一：见 ADR‑0004；
- 质量门禁与发布健康：见 ADR‑0005。

影响范围

- 相关接口与实现（见 Contracts-Refs）：
  - URL/外链：`Game.Core/Interfaces/ISecurityUrlValidator.cs`、`Game.Core/Services/SecurityUrlAdapter.cs`
  - 文件：`Game.Core/Domain/SafeResourcePath.cs`、`Game.Core/Interfaces/ISecurityFileValidator.cs`
  - 进程：`Game.Core/Interfaces/ISecurityProcessValidator.cs`、`Game.Core/Services/SecurityProcessAdapter.cs`
- 受影响模块：安全校验、审计日志与告警链路

验收要点

- 单元测试与 GdUnit4 场景测试存在（见 Test‑Refs）。

示例：当前 Godot+C# 契约引用

- 公会成员加入事件契约：`Game.Core/Contracts/Guild/GuildMemberJoined.cs`（per ADR-0020）
