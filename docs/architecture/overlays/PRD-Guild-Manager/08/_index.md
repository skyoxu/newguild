---
PRD-ID: PRD-Guild-Manager
PRD-Refs:
  - docs/prd.txt
Title: 08 章功能纵切索引（契约与测试对齐）
Updated: true
Arch-Refs:
  - CH01
  - CH03
  - CH04
---

本索引用于聚合本项目“公会管理器（Guild Manager）”相关的功能纵切页面与契约/测试引用。

## 使用策略（止损）

- SSoT：`.taskmaster/tasks/tasks.json`
- 视图：`.taskmaster/tasks/tasks_back.json`、`.taskmaster/tasks/tasks_gameplay.json`
- 需求池：`.taskmaster/tasks/tasks_newguild.json` 仅作为未来需求来源，不作为工作流/门禁脚本读取对象；当其中任务被提升到视图或 SSoT 时，再补齐对应的 Overlay 锚点与契约/Test-Refs。
- 08 章只登记纵切影响范围与验收挂钩；跨切面阈值/策略一律引用 Base/ADR，不在 08 章复制粘贴。
- Windows PowerShell 5.1 查看 UTF-8 文档时请显式指定编码：`Get-Content -Encoding utf8 <path>`，避免把 UTF-8 无 BOM 误读为 ANSI 而产生“看起来像乱码”的错觉。

## 契约与验收页

- 契约索引（避免口径漂移）：`08-Contracts-Index.md`
- 外链白名单（ALLOWED_EXTERNAL_HOSTS）：`08-Contracts-Allowed-External-Hosts.md`
- CloudEvent 契约：`08-Contracts-CloudEvent.md`
- CloudEvents Core 契约：`08-Contracts-CloudEvents-Core.md`
- 公会管理器事件（Guild Manager Events）：`08-Contracts-Guild-Manager-Events.md`
- 质量指标（Quality Metrics）：`08-Contracts-Quality-Metrics.md`
- 安全契约：`08-Contracts-Security.md`
- 数据库迁移（schema_version）
  - Roster：`08-Migrations-Guild-Roster.md`
  - Recruitment：`08-Migrations-Guild-Recruitment.md`
- 纵切总览（当前 SSoT）：`08-FeatureSlice-Guild-Manager.md`
- 纵切验收清单：`ACCEPTANCE_CHECKLIST.md`

## 功能纵切（按 story_id 拆页，便于后续提任务接线）

这些页面作为“提任务进视图/SSoT 时”的审计锚点：先落盘页面，再把视图任务的 `overlay_refs` 指向它们（并补齐 Test-Refs/Contracts-Refs）。

- story_id → overlay page（用于 view 任务 `overlay_refs` 精确接线）
  - `PRD-GUILD-MANAGER-CORE-EVENT-ENGINE` → `08-FeatureSlice-Core-Event-Engine.md`（GM-0101）
  - `PRD-GUILD-MANAGER-CORE-GAME-LOOP` → `08-FeatureSlice-Core-Game-Loop.md`（GM-0103）
  - `PRD-GUILD-MANAGER-CORE-AI-COORDINATOR` → `08-FeatureSlice-Core-AI-Coordinator.md`（GM-0102）
  - `PH15-BACKLOG-B1-B2` → `08-FeatureSlice-Core-Performance-Tracking.md`（Task 20 / NG-0015）
  - `PH16-BACKLOG-B2` → `08-FeatureSlice-Core-Observability.md`（Task 21 / NG-0024）
  - `PRD-GUILD-MANAGER-T3-MEMBER-MANAGEMENT` → `08-FeatureSlice-T3-Member-Management.md`（GM-0202）
  - `PRD-GUILD-MANAGER-T3-RECRUITMENT` → `08-FeatureSlice-T3-Recruitment.md`（GM-0203）
  - `PRD-GUILD-MANAGER-T3-AI-ECOSYSTEM` → `08-FeatureSlice-T3-AI-Ecosystem.md`（GM-0201）
  - `PRD-GUILD-MANAGER-T3-PVE-RAID` → `08-FeatureSlice-T3-PVE-Raid.md`（GM-0204）
  - `PRD-GUILD-MANAGER-T3-SOCIAL` → `08-FeatureSlice-T3-Social.md`（GM-0205）
  - `PRD-GUILD-MANAGER-T3-MEDIA` → `08-FeatureSlice-T3-Media-Reputation.md`（GM-0206）
  - `PRD-GUILD-MANAGER-T3-SAVELOAD-UI` → `08-FeatureSlice-T3-SaveLoad-UI.md`（GM-0207）

- Core
  - `08-FeatureSlice-Core-Event-Engine.md`
  - `08-FeatureSlice-Core-Game-Loop.md`
  - `08-FeatureSlice-Core-AI-Coordinator.md`
  - `08-FeatureSlice-Core-Performance-Tracking.md`
  - `08-FeatureSlice-Core-Observability.md`
- T3
  - `08-FeatureSlice-T3-Member-Management.md`
  - `08-FeatureSlice-T3-Recruitment.md`
  - `08-FeatureSlice-T3-AI-Ecosystem.md`
  - `08-FeatureSlice-T3-PVE-Raid.md`
  - `08-FeatureSlice-T3-Social.md`
  - `08-FeatureSlice-T3-Media-Reputation.md`
  - `08-FeatureSlice-T3-SaveLoad-UI.md`

## T3 前置：数据/内容/调参（已落盘）

这些产物用于在进入 T3 之前先固化“数据字典、内容包、全局调参、样例模板”的最小可执行口径：

- 数据字典与 Schema 规范：`08-DataSchema.md`
- 各阶段内容集清单（PRD Phase 1-4 + Repo T2/T3）：`docs/content/stage-content-inventory.md`
- ID 与版本策略（内容/事件/存档）：`docs/content/id-and-versioning-strategy.md`
- 全局节奏与数值参数：`docs/content/global-tuning.md`
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

## Overlay 自检（建议把输出归档到 logs/ci）

- 校验任务引用的 overlay 路径是否存在：`py -3 scripts/python/validate_task_overlays.py`
- 校验 overlay 的 Test-Refs 指向是否存在（如启用）：`py -3 scripts/python/validate_overlay_test_refs.py --overlay docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Guild-Manager.md --out logs/ci/<YYYY-MM-DD>/overlay-test-refs`
