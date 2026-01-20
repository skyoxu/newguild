---
PRD-ID: PRD-Guild-Manager
PRD-Refs:
  - docs/prd.txt
Story-ID: PH16-BACKLOG-B2
Title: Feature Slice - Core Observability（结构化日志与脱敏）
Status: Delivered
ADR-Refs:
  - ADR-0003
  - ADR-0005
  - ADR-0015
  - ADR-0018
Arch-Refs:
  - CH01
  - CH03
  - CH06
  - CH07
  - CH09
---

本页作为“可观测性客户端（ObservabilityClient）”纵切的审计锚点：约束核心日志/事件采集的结构化输出、脱敏策略与门禁取证方式。

## 设计约束（引用，不复制口径）

- 可观测性与发布健康：引用 `ADR-0003`。
- 质量门禁与产物归档：引用 `ADR-0005`。
- 性能预算相关约束：引用 `ADR-0015`（仅引用，不在本页复制阈值）。
- 运行时与发布约束：引用 `ADR-0018`（Windows-only 模板口径）。

## 实现范围（最小可交付）

- 纯 C# Observability：`Game.Core/Observability/**`（不得依赖 Godot API）
- 脱敏策略：在 Core 内实现可单测验证的 scrubber（避免把路径/敏感字段写入日志/上报）

## Test-Refs

- `Game.Core.Tests/Observability/ObservabilityClientTests.cs`

