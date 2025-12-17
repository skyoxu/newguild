---
PRD-ID: PRD-Guild-Manager
Title: 外链白名单（ALLOWED_EXTERNAL_HOSTS）契约调整
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
  - Tests.Godot/tests/Security/Hard/test_security_integration.gd
  - Tests.Godot/tests/Security/Hard/test_audit_log_compliance.gd
Contracts-Refs:
  - Game.Core/Interfaces/ISecurityUrlValidator.cs
  - Game.Core/Services/SecurityUrlAdapter.cs
  - Game.Godot/Adapters/Security/SecurityUrlAdapter.cs
  - Tests.Godot/Game.Godot/Adapters/Security/SecurityUrlAdapterFactory.cs
  - Tests.Godot/Game.Godot/Adapters/Security/security_url_adapter_factory.gd
Status: Proposed
---

本页为功能纵切（08 章）对应“外链白名单（ALLOWED_EXTERNAL_HOSTS）”契约的变更说明与验收约束。

变更意图（引用，不复制口径）

- 引用 01/02/03 章的统一口径：事件与契约治理（ADR‑0004）、质量门禁（ADR‑0005）。
- 在 Godot + C# 模板中，外链访问必须通过白名单统一管理（见 ADR‑0019）；任何新增对外暴露能力需在契约中显式声明并通过此页落档。

影响范围

- 相关接口与实现（见 Contracts-Refs）：
  - URL 校验与白名单：`Game.Core/Interfaces/ISecurityUrlValidator.cs`、`Game.Core/Services/SecurityUrlAdapter.cs`
  - Godot 侧适配：`Game.Godot/Adapters/Security/SecurityUrlAdapter.cs`
- 受影响模块：外链访问、审计日志与安全告警链路

验收要点（就地）

- 单元测试与 GdUnit4 场景测试存在（见 Test-Refs）
- 白名单策略在代码中显式声明与校验，未出现未授权外链放行

回归与风控

- 仅通过白名单适配层暴露 API；禁止绕过 ADR‑0019 中定义的 Godot 安全基线（外链/文件/权限等约束）。

示例：当前 Godot+C# 契约引用

- 公会成员加入事件契约：`Game.Core/Contracts/Guild/GuildMemberJoined.cs`（per ADR-0020）
