# Stage Content Inventory（按模板富化）

> 说明：本文件按“base.txt”模板的结构做了本项目适配，用“内容集”视角对齐 PRD 与任务系统。
>
> 任务状态与依赖以 `.taskmaster/tasks/tasks.json` 为准；本文件不是任务 SSoT。

## 12.1 Base（主游戏）内容规模（建议）

本项目是“公会生态模拟”，内容的主要形态不是物品与地图，而是：

- 事件内容（邮件/决策/里程碑）
- NPC 公会与成员的原型/策略
- 招募/战术/副本/社交/媒体的内容池与参数

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

## 12.5 最小可用样例与校验（必须可运行）

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
