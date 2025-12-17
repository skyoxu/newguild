---
PRD-ID: PRD-Guild-Manager
Title: 质量指标（Quality Metrics）契约更新
Arch-Refs:
  - CH01
  - CH03
ADR-Refs:
  - ADR-0004
  - ADR-0005
Test-Refs:
  - scripts/python/tests/test_validate_audit_logs.py
  - scripts/python/quality_gates.py
  - scripts/ci/quality_gate.ps1
Contracts-Refs:
  - scripts/python/quality_gates.py
  - scripts/python/task_links_validate.py
  - scripts/python/validate_audit_logs.py
  - scripts/ci/quality_gate.ps1
Status: Proposed
---

本页为功能纵切（08 章）对应“质量指标”契约更新的记录与验收口径。

变更意图（引用，不复制口径）

- 指标事件与 DTO 统一归口（ADR‑0004），相关阈值与门禁由基线章节维护（ADR‑0005），此处仅登记功能影响与测试。

影响范围

- 产出契约（脚本与产物）：
  - 质量门禁入口：`scripts/ci/quality_gate.ps1`、`scripts/python/quality_gates.py`
  - 回链校验：`scripts/python/task_links_validate.py`
  - 审计日志校验：`scripts/python/validate_audit_logs.py`
- 受影响模块：质量门禁、发布健康、审计日志与归档工件

验收要点（就地）

- 单测覆盖基本结构；E2E 占位用例存在（见 Test-Refs）

示例：当前 Godot+C# 契约引用

- 公会成员加入事件契约：`Game.Core/Contracts/Guild/GuildMemberJoined.cs`（per ADR-0020）
