---
PRD-ID: PRD-Guild-Manager
PRD-Refs:
  - docs/prd.txt
Story-ID: PH15-BACKLOG-B1-B2
Title: Feature Slice - Core Performance Tracking（性能追踪与门禁）
Status: Delivered
ADR-Refs:
  - ADR-0005
  - ADR-0015
  - ADR-0018
Arch-Refs:
  - CH01
  - CH06
  - CH07
  - CH09
---

本页作为“性能追踪库 + 性能门禁脚本”的审计锚点：约束 Game.Core 内的性能统计实现不依赖 Godot API，并与 CI 的性能门禁输出对齐。

## 设计约束（引用，不复制阈值）

- 性能预算与门禁阈值：引用 `ADR-0015`（不要在本页复制具体阈值）。
- 质量门禁与产物归档：引用 `ADR-0005`（产物统一写入 `logs/`）。
- 运行时与发布约束：引用 `ADR-0018`（Windows-only 模板口径）。

## 实现范围（最小可交付）

- 纯 C# 性能统计：`Game.Core/Performance/**`
- 门禁聚合与报告：`scripts/python/**`（读取性能产物并输出门禁结果）
- Godot 侧采样（如存在）：仅负责采集与落盘到 `user://logs/perf/**`，不在引擎层实现统计逻辑。

## Test-Refs

- `Game.Core.Tests/Performance/PerformanceTrackerTests.cs`

