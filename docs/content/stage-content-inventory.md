# Stage Content Inventory（按模板富化）

> 说明：本文件按“base.txt”模板的结构做了本项目适配，用“内容集”视角对齐 PRD 与任务系统。
>
> 任务状态与依赖以 `.taskmaster/tasks/tasks.json` 为准；本文件不是任务 SSoT。

## 12.1 Base（主游戏）内容规模（建议）

本项目是“公会生态模拟”，内容的主要形态不是物品与地图，而是：

- 事件内容（邮件/决策/里程碑）
- NPC 公会与成员的原型/策略
- 招募/战术/副本/社交/媒体的内容池与参数

### 12.1.0 分阶段内容目标（止损口径）

你指出“T3 看上去很庞大，但 tasks.json 里任务并不多”的核心原因是：**任务维度**与**内容维度**不一致。

- `tasks.json`（SSoT）里的任务更多是“模块级能力”，数量少但跨度大
- 内容集（事件池、原型库、参数表）会随阶段持续扩容，且大部分扩容不会在早期就进入 SSoT

因此本节把“内容规模”按阶段拆开（不是承诺，只是给后续补内容/平衡提供锚点）。

| 内容类别 | T2（已完成：最小可玩） | T3（Alpha：模块可玩） | T4（Beta：生态可用） | T5（Release：内容密度） |
|---|---:|---:|---:|---:|
| Guild Events（可决策事件） | 8–15 | 30–60 | 120–200 | 200–400 |
| Mail Rules（邮件生成规则） | 3–6 | 10–20 | 30–60 | 60–120 |
| Member Archetypes（成员原型） | 12–25 | 60–120 | 150–300 | 300–600 |
| NPC Guild Archetypes（NPC 公会原型） | 3–6 | 10–20 | 20–40 | 40–80 |
| Recruit Offers（招募模板） | 6–12 | 20–40 | 60–120 | 120–250 |
| Tactics（战术模板） | 6–10 | 15–30 | 40–80 | 80–160 |
| Raid Encounters（PVE 遭遇） | 2–4 | 6–12 | 18–30 | 30–60 |
| Social Interactions（社交互动） | 6–12 | 20–40 | 60–120 | 120–240 |
| Media Beats（媒体舆情） | 6–12 | 20–50 | 60–120 | 120–240 |
| Achievements（成就） | 6–12 | 20–40 | 40–80 | 80–160 |
| Milestones（历史里程碑） | 4–8 | 15–30 | 40–80 | 80–160 |
| Facilities（公会设施类型） | 0–3 | 6–12 | 12–20 | 20–35 |
| Diplomacy Events（外交事件池） | 0–5 | 10–20 | 20–40 | 40–80 |
| World Boss（世界 Boss） | 0 | 1–2 | 3–6 | 6–12 |
| Auction Items（拍卖品类） | 0–10 | 30–60 | 80–200 | 200–600 |

### 12.1.1 NPC 公会原型（建议至少 10–20 个）

- 每个原型至少包含：公会风格、招募偏好、战术倾向、媒体偏好（可后续增量）

### 12.1.2 成员原型（建议至少 60–120 个）

- 覆盖：角色定位（Tank/Healer/DPS）、性格、偏好、稳定标签（用于事件条件）

### 12.1.3 公会事件（建议至少 40–80；目标 200+）

- Base 目标：让“每周都有事做”，且事件能影响状态（声望/士气/成员流动）

### 12.1.4 招募内容（建议至少 20–40 条）

- 候选人生成规则、谈判选项、失败/成功反馈

### 12.1.5 战术与阵容模板（建议至少 15–30 条）

- 阵容角色分配规则（坦克/输出/治疗比例）
- 战术偏好与 AI 自动调配（先定最小可解释口径）

### 12.1.6 PVE 遭遇（建议至少 6–12 个；含 1–2 个阶段 Boss）

- 用于周结算阶段的“可见结果”，形成阶段感与反馈闭环

### 12.1.7 社交互动内容（建议至少 20–40 条）

- 亲友团、官员、论坛互动的最小事件池

### 12.1.8 媒体与声望内容（建议至少 20–50 条）

- 新闻稿/舆情/粉丝波动事件，绑定声望与资源变化

## 12.2 Base 建议 ID 清单（示例）

ID 命名空间遵循 `Base_*`（DLC 扩展使用 `DLC1_*`）。

### 12.2.1 公会事件（Base）

- `Base_GuildEvent_WelcomeNewMember`
- `Base_GuildEvent_InternalConflict`
- `Base_GuildEvent_RecruitOfferArrives`
- `Base_GuildEvent_RaidVictory`
- `Base_GuildEvent_RaidWipe`

### 12.2.2 成员原型（Base）

- `Base_MemberArchetype_ReliableTank`
- `Base_MemberArchetype_ShyHealer`
- `Base_MemberArchetype_AmbitiousDps`

### 12.2.3 NPC 公会原型（Base）

- `Base_NpcGuild_HardcoreRaiders`
- `Base_NpcGuild_CasualSocial`
- `Base_NpcGuild_MetaChasers`

### 12.2.4 招募模板（Base）

- `Base_RecruitOffer_NewbieTank`
- `Base_RecruitOffer_VeteranHealer`

### 12.2.5 PVE 遭遇（Base）

- `Base_RaidEncounter_TrainingDummy`
- `Base_RaidEncounter_StarterDungeon`
- `Base_RaidEncounter_Boss_Trial`

## 12.3 PRD 四阶段（Phase 1–4）内容视角对齐

来自 `docs/prd.txt` 的开发里程碑（10 个月，4 阶段）：

### Phase 1（Month 1–4）：事件引擎核心

- 内容目标：少量“可跑通链路”的事件内容（10–20），用于验证条件/效果/权重与冲突处理
- 数据目标：内容包目录、ID 规范、内容版本字段、最小 tuning

### Phase 2（Month 5–7）：系统集成（8 大模块 + UI）

- 内容目标：各模块都至少有“可见的内容池”
  - 会员管理/招募：候选人 + 谈判事件
  - 作战大厅/战术中心：阵容模板与战术模板
  - 论坛/外交/后勤：媒体与社交事件池
- 数据目标：内容合并策略可用（Base + DLC），并能被 CI 校验

### Phase 3（Month 8–9）：生态内容完善

- 内容目标：事件池扩展到 200+，并建立平衡参数的可迭代流程
- 数据目标：内容版本演进与存档迁移策略成熟（避免“存档全废”）

### Phase 4（Month 10）：抛光与扩展

- 内容目标：内容分布更均衡（避免某模块事件密度过低），提供 DLC 钩子
- 数据目标：发布前的质量门禁与审计证据链完善

## 12.4 Repo 阶段（T2/T3）与 PRD 阶段映射（止损口径）

- Repo T2：偏 PRD Phase 1 的“系统骨架” + 少量可玩链路（已完成）
- Repo T3：偏 PRD Phase 2–3 的“模块内容扩展”（已规划/待实现）

## 12.4.1 你指出的问题（规划需要更细）

仅用 PRD 的 Phase 1–4 去描述 repo 的 T2/T3 会显得过粗，原因是：

- `tasks.json`（SSoT）里的 T3 任务目前是“模块级任务”（每个模块 1 条），颗粒较粗
- 大量更细颗粒的后续任务仍在 `tasks_newguild.json`（Backlog/参考视图），不应被误当作已进入 SSoT 的计划

因此这里新增“Repo 细分阶段（Micro-Phases）”，用来把 **当前 SSoT** 与 **Backlog** 分层表达。

## 12.5 Repo 细分阶段（Micro-Phases，与 tasks.json / tasks_newguild 对齐）

> 口径：
> - SSoT：`.taskmaster/tasks/tasks.json`
> - Backlog/参考：`.taskmaster/tasks/tasks_newguild.json`
> - 本节的目标是“解释与分层”，不是替代任务系统。

### R2（已完成）：T2 可玩骨架（SSoT）

- 目标：可启动、可跑回合循环、事件系统可用、门禁与审计证据链可复现
- 覆盖范围：tasks.json 任务 1–12、20–24（已完成）

### R3.0（前置已落盘）：内容/数据管线最小口径（非 Task 25/26）

- 目标：在进入 T3 之前先把“数据字典/内容包/调参/样例模板/校验门禁”固化为可执行口径
- 产物与入口：
  - `docs/architecture/overlays/PRD-Guild-Manager/08/08-DataSchema.md`
  - `docs/content/stage-content-inventory.md`
  - `docs/content/id-and-versioning-strategy.md`
  - `docs/content/global-tuning.md`
  - `Game.Godot/Assets/Data/content/base/*.json`
  - `py -3 scripts/python/validate_content_assets.py`

### R3.1：T3 持久化与迁移（SSoT Task 25/26）

- 目标：Save/Load + schema migration 可用，并提供 UI 入口与审计证据
- 任务锚点：tasks.json 25/26（当前为模块级任务，后续可下沉到更细子任务）

### R3.2：成员管理与招募（SSoT Task 13/14）

- 目标：Roster/Recruitment 有最小可玩链路，并能被 Save/Load 持久化
- 任务锚点：tasks.json 13/14
- Backlog（参考拆分，来自 tasks_newguild.json）：
  - 18 Member Management Module
  - 20 Recruitment System
  - 34 Guild Roster System
  - 82 Member Management and Attribute System

### R3.3：AI 生态扩展（SSoT Task 15）

- 目标：NPC 公会与成员行为从“能跑”提升到“可解释 + 可控性能”
- 任务锚点：tasks.json 15
- Backlog（参考拆分）：
  - 3 AI Coordinator System
  - 13 AI Ecosystem
  - 41 NPC Member Contribution
  - 91 AI Simulation Module

### R3.4：PVE 遭遇（SSoT Task 17）

- 目标：周结算阶段能产出“可见结果”，形成阶段感
- 任务锚点：tasks.json 17
- Backlog（参考拆分）：
  - 21 Combat Simulation System
  - 23 Combat Encounter System
  - 61 World Boss Entity

### R3.5：社交与媒体（SSoT Task 18/19）

- 目标：社交互动与媒体舆情进入事件池与回合循环，形成反馈闭环
- 任务锚点：tasks.json 18/19
- Backlog（参考拆分）：
  - 19 Social Interaction System
  - 24 Media and Community Interaction System

### R4（未来扩展）：模块化与 DLC（Backlog）

- 目标：DLC/插件内容包的合并与兼容性治理
- Backlog（参考）：
  - 8 Modular Plugin System
  - 10 DLC and Expansion Support
  - 22 DLC and Plugin Support

### R4.1（未来扩展）：成就/奖励/里程碑（Backlog）

- 目标：把“周循环结果”变成可累积的长期目标（奖励、叙事、历史记录），并与媒体/粉丝预期联动
- Backlog（参考）：
  - 25 Design Achievement and Reward System
  - 28 Develop History Milestone System
  - 29 Create Historical Ranking System
  - 35 Implement Legendary Highlight System
  - 51 Design Achievement System
  - 52 Integrate Fan Expectations with Achievements
  - 54 Develop Guild Reputation System

### R4.2（未来扩展）：外交与联盟（Backlog）

- 目标：把 NPC 公会生态扩展为“关系网络”，并为世界事件（世界 Boss）提供协作/对抗入口
- Backlog（参考）：
  - 31 Implement Diplomatic Attitude System
  - 32 Develop Diplomatic Event Pool
  - 63 Design Alliance Management System
  - 67 Build Alliance Matching Engine

### R4.3（未来扩展）：公会基地/设施/经济（Backlog）

- 目标：把资源投入/长期建设引入循环（设施升级、库存/公会银行、拍卖市场）
- Backlog（参考）：
  - 38 Implement Guild Base Core System
  - 39 Develop Facility Management System
  - 40 Implement Facility Upgrade Requirements
  - 101 Implement GuildBank and Inventory Management
  - 42 Create Auction House System
  - 102 Develop AuctionHouseSystem

### R4.4（未来扩展）：世界 Boss 与难度调节（Backlog）

- 目标：引入跨公会的大型事件（世界 Boss），并提供动态难度/奖励分配，形成可持续的周期挑战
- Backlog（参考）：
  - 60 Design World Boss System Interface
  - 61 Implement World Boss Entity
  - 64 Create Reward Distribution System
  - 65 Implement Dynamic Difficulty System
  - 66 Develop World Boss Manager Class
  - 68 Create Difficulty Adjustment Engine
  - 69 Validate System Integration and Performance

### R4.9（未来扩展）：整合与体验强化（Backlog）

- 目标：从“模块能跑”转向“模块可解释、可搜索、可调参、可测”，避免内容规模上去后失控
- Backlog（参考）：
  - 23 Develop User Experience Enhancements
  - 45 Develop Intelligent Mail System
  - 46 Design Event-Driven Mail Generation System
  - 47 Implement Advanced Filter and Search System
  - 48 Design Responsive User Interface
  - 49 Implement Core UI Components
  - 59 Conduct System Integration Testing
  - 72 Develop Data Architecture and State Management
  - 76 Integrate Core Game Modules
  - 80 Establish Data Structures and Memory Management

> 注：tasks_newguild.json 是 Backlog/参考视图，本文件只取其“主题/依赖线索”用于阶段拆分。
> 更细的分组报告会落盘到：`logs/ci/<YYYY-MM-DD>/backlog-phase-analysis/`。

## 12.6 最小可用样例与校验（必须可运行）

本项目的最小可用样例（Base 内容包）：

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

CI 校验入口：

- `py -3 scripts/python/validate_content_assets.py`
- 产物：`logs/ci/<YYYY-MM-DD>/content-validation/`
