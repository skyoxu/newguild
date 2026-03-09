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

  - `PRD-GUILD-MANAGER-PHASE2-CONTENT` → `08-FeatureSlice-Phase2-Content-Loading.md`（T27 / GM-0301）
  - `PRD-GUILD-MANAGER-PHASE2-EVENT-CONTRACTS` → `08-FeatureSlice-Phase2-Events-ContentDriven.md`（T28 / GM-0302）
  - `PRD-GUILD-MANAGER-PHASE2-EVENT-CATALOG` → `08-FeatureSlice-Phase2-Events-ContentDriven.md`（T29,T42 / GM-0303）
  - `PRD-GUILD-MANAGER-PHASE2-UI-RESPONSIVE` → `08-FeatureSlice-Phase2-UI-Usability.md`（T30-33 / GM-0304..GM-0307）
  - `PRD-GUILD-MANAGER-PHASE2-TACTICAL-CENTER` → `08-FeatureSlice-Phase2-Tactical-Rewards.md`（T34 / GM-0308）
  - `PRD-GUILD-MANAGER-PHASE2-REWARDS` → `08-FeatureSlice-Phase2-Tactical-Rewards.md`（T35-37 / GM-0309..GM-0311）
  - `PRD-GUILD-MANAGER-PHASE2-OFFICERS` → `08-FeatureSlice-Phase2-Officers.md`（T38-39 / GM-0312..GM-0313）
  - `PRD-GUILD-MANAGER-PHASE2-WORLDGEN` → `08-FeatureSlice-Phase2-Worldgen.md`（T40-41 / GM-0314..GM-0315）
  - `PHASE2-BACK-DEPENDENCY-GUARDS` → `08-FeatureSlice-Phase2-Architecture-Guards.md`（T43）

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

<!-- V11_INDEX_START -->
## V1.1 阶段任务导航（T53-T102）

- 治理/安全/数据阶段总览：
  - `08-FeatureSlice-V11-Governance-Stabilization.md`
- 玩法深度阶段总览：
  - `08-FeatureSlice-V11-Gameplay-Depth.md`

映射规则：

- `tasks.json` 为主 SSoT（T53-T102）
- `tasks_back.json` 只承载治理/安全/数据任务
- `tasks_gameplay.json` 只承载玩法功能任务

执行策略：

- 从 T53 起保持严格串行（前一任务完成后再执行下一任务）
- 每个阶段任务必须具备 `ACC:T<id>.<n>` 对应证据与 `logs/**` 工件
<!-- V11_INDEX_END -->

<!-- BEGIN:T53-T102-MAP -->
## T53-T102 ????????????

> ???? `tasks.json` ? `tasks_back/tasks_gameplay` ???? overlay ????????????????????

| Task | Title | Status | Back IDs | Gameplay IDs | Overlay |
|---|---|---|---|---|---|
| T53 | V1.1 治理：稳定性门禁基线统一 | pending | NG-0053 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T54 | V1.1 治理：Headless Flaky 采集与根因分类 | pending | NG-0054 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T55 | V1.1 治理：测试编排与证据消费规范 | pending | NG-0055 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T56 | V1.1 治理：性能预算硬门与场景基准 | pending | NG-0056 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/_index.md` |
| T57 | V1.1 安全治理：Release Health 门禁固化 | pending | NG-0057 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T58 | V1.1 玩法：招募到入会闭环调参与反馈（阶段1） | pending | - | GM-0322 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Recruitment.md` |
| T59 | V1.1 玩法：Raid 结果驱动成长与排名联动（阶段1） | pending | - | GM-0323 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-PVE-Raid.md` |
| T60 | V1.1 玩法：两周循环与存档回放一致性（阶段1） | pending | - | GM-0324 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Core-Game-Loop.md` |
| T61 | V1.1 数据治理：schemaVersion 与迁移矩阵 | pending | NG-0058 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T62 | V1.1 数据治理：跨版本存档回放与损坏恢复（阶段1） | pending | NG-0059 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T63 | V1.1 治理：观测与审计日志字段统一 | pending | NG-0060 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/_index.md` |
| T64 | V1.1 治理：CI 诊断摘要与止损模板 | pending | NG-0061 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T65 | V1.1 治理：Task/ADR/Overlay/Test-Refs 自动体检 | pending | NG-0062 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/_index.md` |
| T66 | V1.1 收口：RC 准入评审包（阶段1） | pending | NG-0063 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T67 | V1.1 玩法深度：招募链路边界与异常分支（阶段1） | pending | - | GM-0325 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Recruitment.md` |
| T68 | V1.1 玩法深度：成员管理状态机与角色变更（阶段1） | pending | - | GM-0326 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Member-Management.md` |
| T69 | V1.1 玩法深度：Raid 进阶规则与结果可解释性（阶段1） | pending | - | GM-0327 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-PVE-Raid.md` |
| T70 | V1.1 玩法深度：经济与后勤联动闭环（阶段1） | pending | - | GM-0328 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Tactical-Rewards.md` |
| T71 | V1.1 玩法深度：媒体事件与声望反馈闭环（阶段1） | pending | - | GM-0329 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Media-Reputation.md` |
| T72 | V1.1 玩法扩展：WorldBoss 从入口到可玩闭环（阶段1） | pending | - | GM-0330 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Guild-Manager.md` |
| T73 | V1.1 玩法扩展：PVP 从入口到匹配结算（阶段1） | pending | - | GM-0331 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Social.md` |
| T74 | V1.1 平衡治理：玩法遥测与数值调参闭环（阶段1） | pending | - | GM-0332 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Core-Observability.md` |
| T75 | V1.1 收口：玩法全链路回归与发布前验收（阶段1） | pending | - | GM-0333 | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T76 | V1.1 玩法：招募到入会闭环调参与反馈（阶段2） | pending | - | GM-0334 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Recruitment.md` |
| T77 | V1.1 玩法：Raid 结果驱动成长与排名联动（阶段2） | pending | - | GM-0335 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-PVE-Raid.md` |
| T78 | V1.1 玩法：两周循环与存档回放一致性（阶段2） | pending | - | GM-0336 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Core-Game-Loop.md` |
| T79 | V1.1 数据治理：跨版本存档回放与损坏恢复（阶段2） | pending | NG-0064 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T80 | V1.1 收口：RC 准入评审包（阶段2） | pending | NG-0065 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T81 | V1.1 玩法深度：招募链路边界与异常分支（阶段2） | pending | - | GM-0337 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Recruitment.md` |
| T82 | V1.1 玩法深度：成员管理状态机与角色变更（阶段2） | pending | - | GM-0338 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Member-Management.md` |
| T83 | V1.1 玩法深度：Raid 进阶规则与结果可解释性（阶段2） | pending | - | GM-0339 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-PVE-Raid.md` |
| T84 | V1.1 玩法深度：经济与后勤联动闭环（阶段2） | pending | - | GM-0340 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Tactical-Rewards.md` |
| T85 | V1.1 玩法深度：媒体事件与声望反馈闭环（阶段2） | pending | - | GM-0341 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Media-Reputation.md` |
| T86 | V1.1 玩法扩展：WorldBoss 从入口到可玩闭环（阶段2） | pending | - | GM-0342 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Guild-Manager.md` |
| T87 | V1.1 玩法扩展：WorldBoss 从入口到可玩闭环（阶段3） | pending | - | GM-0343 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Guild-Manager.md` |
| T88 | V1.1 玩法扩展：PVP 从入口到匹配结算（阶段2） | pending | - | GM-0344 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Social.md` |
| T89 | V1.1 玩法扩展：PVP 从入口到匹配结算（阶段3） | pending | - | GM-0345 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Social.md` |
| T90 | V1.1 平衡治理：玩法遥测与数值调参闭环（阶段2） | pending | - | GM-0346 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Core-Observability.md` |
| T91 | V1.1 平衡治理：玩法遥测与数值调参闭环（阶段3） | pending | - | GM-0347 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Core-Observability.md` |
| T92 | V1.1 收口：玩法全链路回归与发布前验收（阶段2） | pending | - | GM-0348 | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T93 | V1.1 收口：玩法全链路回归与发布前验收（阶段3） | pending | - | GM-0349 | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T94 | V1.1 玩法：两周循环与存档回放一致性（阶段3） | pending | - | GM-0350 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Core-Game-Loop.md` |
| T95 | V1.1 玩法深度：招募链路边界与异常分支（阶段3） | pending | - | GM-0351 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Recruitment.md` |
| T96 | V1.1 玩法深度：成员管理状态机与角色变更（阶段3） | pending | - | GM-0352 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Member-Management.md` |
| T97 | V1.1 玩法深度：Raid 进阶规则与结果可解释性（阶段3） | pending | - | GM-0353 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-PVE-Raid.md` |
| T98 | V1.1 玩法深度：经济与后勤联动闭环（阶段3） | pending | - | GM-0354 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Tactical-Rewards.md` |
| T99 | V1.1 玩法深度：媒体事件与声望反馈闭环（阶段3） | pending | - | GM-0355 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Media-Reputation.md` |
| T100 | V1.1 收口：玩法全链路回归与发布前验收（阶段4） | pending | - | GM-0356 | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T101 | V1.1 收口：玩法全链路回归与发布前验收（阶段5） | pending | - | GM-0357 | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T102 | V1.1 收口：玩法全链路回归与发布前验收（阶段6） | pending | - | GM-0358 | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |

- ?????`.taskmaster/tasks/tasks.json` + `.taskmaster/tasks/tasks_back.json` + `.taskmaster/tasks/tasks_gameplay.json`
- ?????2026-03-09
<!-- END:T53-T102-MAP -->
