---
PRD-ID: PRD-Guild-Manager
Title: 08 章功能纵切索引（契约与测试对齐）
Updated: true
Arch-Refs:
  - CH01
  - CH03
  - CH04
---

本索引聚合本次契约变更的功能纵切页面与对应测试/数据引用（仅引用 Base/ADR 口径，不在此处复制阈值与策略）。

## 契约与验收页

- 外链白名单（ALLOWED_EXTERNAL_HOSTS）：`08-Contracts-Allowed-External-Hosts.md`
- CloudEvent 契约：`08-Contracts-CloudEvent.md`
- CloudEvents Core 契约：`08-Contracts-CloudEvents-Core.md`
- 公会管理事件（Guild Manager Events）：`08-Contracts-Guild-Manager-Events.md`
- 质量指标（Quality Metrics）：`08-Contracts-Quality-Metrics.md`
- 安全契约：`08-Contracts-Security.md`
- 功能纵切：公会管理器：`08-FeatureSlice-Guild-Manager.md`

## T3 前置：数据/内容/调参（不进入 Task 25/26）

这些产物用于在进入 T3 之前先固定“数据字典、内容包、全局调参、样例模板”的最小可执行口径。

- 数据字典与 Schema 规范：`08-DataSchema.md`
- 各阶段内容集清单（PRD Phase 1-4 + Repo T2/T3）：`docs/content/stage-content-inventory.md`
- 稳定 ID 与版本策略（内容/事件/存档）：`docs/content/id-and-versioning-strategy.md`
- 全局节奏与数值参数（人读）：`docs/content/global-tuning.md`
- 最小可用样例 JSON（Base 内容包示例）：
  - `Game.Godot/Assets/Data/content/base/manifest.json`
  - `Game.Godot/Assets/Data/content/base/guild_events.json`
  - `Game.Godot/Assets/Data/content/base/member_archetypes.json`
  - `Game.Godot/Assets/Data/content/base/npc_guilds.json`
  - `Game.Godot/Assets/Data/content/base/recruit_offers.json`
  - `Game.Godot/Assets/Data/content/base/raid_encounters.json`
  - `Game.Godot/Assets/Data/content/base/tactics.json`
  - `Game.Godot/Assets/Data/content/base/media_beats.json`
  - `Game.Godot/Assets/Data/content/base/social_interactions.json`
  - `Game.Godot/Assets/Data/content/base/tuning.json`
- 内容校验脚本（CI 硬门禁）：`scripts/python/validate_content_assets.py`
  - 产物：`logs/ci/<YYYY-MM-DD>/content-validation/`

## 示例：当前 Godot + C# 契约引用

- `Game.Core/Contracts/Guild/GuildMemberJoined.cs`（per ADR-0020）
