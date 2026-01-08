---
PRD-ID: PRD-Guild-Manager
Title: 08 章功能纵切索引（契约与测试对齐）
Updated: true
Arch-Refs:
  - CH01
  - CH03
---

本索引聚合本次契约变更的功能纵切页面与对应测试引用（仅引用 01/02/03 章口径，不在此处复制阈值/策略）。

## 契约与验收页

- 外链白名单（ALLOWED_EXTERNAL_HOSTS）：`08-Contracts-Allowed-External-Hosts.md`
- CloudEvent 契约：`08-Contracts-CloudEvent.md`
- CloudEvents Core 契约：`08-Contracts-CloudEvents-Core.md`
- 公会管理事件（Guild Manager Events）：`08-Contracts-Guild-Manager-Events.md`
- 质量指标（Quality Metrics）：`08-Contracts-Quality-Metrics.md`
- 安全契约：`08-Contracts-Security.md`
- T3 前置：Save/Load 数据字典与 Schema 规范：`08-DataSchema-SaveLoad.md`
- 功能纵切：公会管理器：`08-功能纵切-公会管理器.md`

## T3 前置产物（内容与参数）

这些产物用于在进入 T3 之前先固定“数据/内容/参数/版本”的最小口径，不替代 Contracts 的 SSoT。

- 阶段内容集清单（T2/T3）：`docs/content/stage-content-inventory.md`
- 稳定 ID 与版本策略：`docs/content/id-and-versioning-strategy.md`
- 平衡参数注册表（人读）：`docs/content/balance-parameter-registry.md`
- 平衡参数默认值（机读）：`Game.Godot/Assets/Data/Balance/balance_params.default.json`
- 最小事件内容模板（机读）：`Game.Godot/Assets/Data/Templates/event_definition_minimal.json`
- 内容校验脚本（CI 硬门禁）：`scripts/python/validate_content_assets.py`（产物：`logs/ci/<YYYY-MM-DD>/content-validation/`）

## 示例：当前 Godot + C# 契约引用

- `Game.Core/Contracts/Guild/GuildMemberJoined.cs`（per ADR-0020）
