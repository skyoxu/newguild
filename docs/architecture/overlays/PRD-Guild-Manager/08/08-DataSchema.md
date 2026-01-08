# 08-DataSchema（T3 前置：数据字典与 Schema 规范）

> 说明：本文件按“Schema.txt”模板的结构做了本项目适配，目标是让数据与内容在进入 T3 前先可被自动化校验，避免后期返工。

## 11.1 通用约定（必须统一）

### 11.1.1 内容 ID 命名空间（Content IDs）

- Base 内容：`Base_*`
- DLC1 内容：`DLC1_*`
- 所有引用一律用 `id` 字符串，不使用数组下标。

本项目推荐的 ID 族（示例）：

- 公会事件：`Base_GuildEvent_*`
- 成员原型：`Base_MemberArchetype_*`
- NPC 公会原型：`Base_NpcGuild_*`
- 招募模板：`Base_RecruitOffer_*`
- PVE 遭遇：`Base_RaidEncounter_*`
- 媒体事件：`Base_MediaBeat_*`
- 社交互动：`Base_SocialInteraction_*`
- 战术模板：`Base_Tactic_*`

### 11.1.2 时间单位

- 所有周期/间隔字段默认单位：`seconds`（浮点或整数均可，推荐支持小数）。
- 回合推进按“周”为主（`week`），但跨文件统一用明确字段名表达，例如 `cooldownWeeks`、`durationWeeks`。

### 11.1.3 百分比表示

- 统一用小数：`0.1 = 10%`。

### 11.1.4 本地化（可选但建议）

- `nameKey` / `descKey` 指向文本表（后续可接入多语言）。
- T2/T3 允许直接用中文字符串，但字段必须保留，避免后续重构。

### 11.1.5 权重表（候选/抽样）

- 权重字段名统一：`weight`（整数，`>=0`）。
- 允许 `min` / `max` 控制数量范围（可选）。

### 11.1.6 Content 版本与 Save 版本

- 内容配置文件级字段：`contentVersion`（语义版本字符串，例如 `"1.0.0"`）。
- 存档版本：
  - `schemaVersion`：DB schema 版本（SQLite 迁移 runner 负责）
  - `saveVersion`：逻辑快照版本（Core 映射器负责，与 DB schemaVersion 区分）

### 11.1.7 安全与路径（与 ADR-0019 对齐）

- 所有可写文件只允许 `user://`。
- 在 Release/secure 模式下，错误信息与审计 `reason` 不得泄露绝对路径或 SQL 原文。

## 11.2 内容包目录与合并策略（建议实现）

本项目的内容目录约定（对齐 Godot 工程结构）：

- Base：`res://Game.Godot/Assets/Data/content/base/*.json`
- DLC1：`res://Game.Godot/Assets/Data/content/dlc/dlc1/manifest.json` + `res://.../dlc1/*.json`

合并规则（建议由 `ContentPackLoader` 或等价组件实现）：

1. 加载 Base → 再加载启用 DLC 的内容包
2. 以 `id` 合并到统一字典
3. 若 `id` 冲突：视为配置错误（直接报错并阻止启动，避免“暗覆盖”）

## 11.3 公共结构（Data Dictionary）

### 11.3.1 WeightedRef（权重候选）

- `id`: string
- `weight`: int（>=0）
- `min`: int（默认 1，可选）
- `max`: int（默认 1，可选）

### 11.3.2 Condition（触发条件，最小集）

`type` 建议最小枚举（按 PRD 语义做本项目适配）：

- `WeekAtLeast`
- `GuildReputationAtLeast`
- `GuildMemberCountAtLeast`
- `HasRoleCountAtLeast`（例如 Tank/Healer/DPS）
- `FlagEquals`（通用开关）
- `RandomRoll`（按概率触发，`chance` 使用小数）

字段（按 type 变化）：

- `refId`: string（例如 flagId）
- `value`: int（例如周数/人数/声望阈值）
- `chance`: number（`0..1`，仅 RandomRoll 使用）

### 11.3.3 Effect（效果/结果，最小集）

`type` 建议最小枚举：

- `AdjustGuildReputation`
- `AdjustMemberMorale`
- `AddMember`
- `RemoveMember`
- `ChangeMemberRole`
- `AddResource`（货币/资源的通用入口，避免早期拆太细）
- `QueueMail`（进入玩家阶段邮箱/决策点）
- `ScheduleRaid`（创建 PVE 事件/日程）
- `SetFlag`

字段示例：

- `amount`: number 或 int（按类型决定）
- `role`: string（例如 `"Tank" | "Healer" | "DPS"`，可在后续契约中固化）

## 11.4 GuildEvents（公会事件内容）

### 11.4.1 Data Dictionary（单条事件）

- `id`: string（Base_/DLC1_ 命名空间）
- `nameKey`: string
- `descKey`: string（可选）
- `category`: string（示例：`"Roster" | "Recruitment" | "Raid" | "Social" | "Media"`）
- `weight`: int（>=0）
- `cooldownWeeks`: int（>=0，可选；默认 0）
- `conditions`: Condition[]
- `effects`: Effect[]

### 11.4.2 JSON Schema（片段）

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "guild-event.schema.json",
  "type": "object",
  "required": ["id", "nameKey", "category", "weight", "conditions", "effects"],
  "properties": {
    "id": { "type": "string" },
    "nameKey": { "type": "string" },
    "descKey": { "type": "string" },
    "category": { "type": "string" },
    "weight": { "type": "integer", "minimum": 0 },
    "cooldownWeeks": { "type": "integer", "minimum": 0 },
    "conditions": { "type": "array", "items": { "type": "object" } },
    "effects": { "type": "array", "items": { "type": "object" } }
  }
}
```

### 11.4.3 样例文件（最小可用）

- `Game.Godot/Assets/Data/content/base/guild_events.json`

## 11.6 MemberArchetypes（成员原型）

最小字段建议：

- `id` / `nameKey`
- `role`（Tank/Healer/DPS）
- `personalityTags`（用于事件条件）
- `baseRatings`（用于 AI/招募/战斗的最小评分口径）

样例文件：

- `Game.Godot/Assets/Data/content/base/member_archetypes.json`

## 11.7 NpcGuildArchetypes（NPC 公会原型）

最小字段建议：

- `id` / `nameKey` / `style`
- `recruitmentPreferences`（Tank/Healer/DPS 小数占比）
- `tacticPreferences`（引用战术 id）
- `reputationBaseline`

样例文件：

- `Game.Godot/Assets/Data/content/base/npc_guilds.json`

## 11.8 RecruitOffers（招募模板）

最小字段建议：

- `id` / `nameKey` / `role`
- `difficultyTier`
- `baseSuccessChance`（小数）
- `effectsOnSuccess` / `effectsOnFail`（复用 Effect 结构）

样例文件：

- `Game.Godot/Assets/Data/content/base/recruit_offers.json`

## 11.10 RaidEncounters（PVE 遭遇）

最小字段建议：

- `id` / `nameKey`
- `difficultyTier`
- `recommendedRoleRatio`（Tank/Healer/DPS 小数占比）
- `successReputationDelta` / `failMoraleDelta`（min/max）

样例文件：

- `Game.Godot/Assets/Data/content/base/raid_encounters.json`

## 11.11 Tactics（战术模板）

最小字段建议：

- `id` / `nameKey`
- `tags`
- `modifiers`（只允许小数概率或整数增量；不得混用百分号表示）

样例文件：

- `Game.Godot/Assets/Data/content/base/tactics.json`

## 11.12 MediaBeats（媒体事件）

最小字段建议：

- `id` / `nameKey`
- `weight`
- `reputationDelta`（min/max）

样例文件：

- `Game.Godot/Assets/Data/content/base/media_beats.json`

## 11.13 SocialInteractions（社交互动）

最小字段建议：

- `id` / `nameKey`
- `weight`
- `effects`（复用 Effect 结构）

样例文件：

- `Game.Godot/Assets/Data/content/base/social_interactions.json`

## 11.5 全局节奏与数值参数（Global Tuning）

- `Game.Godot/Assets/Data/content/base/tuning.json`
- 人读说明：`docs/content/global-tuning.md`

## 11.9 Save/Load + SQLite Schema（与 ADR-0006/0019 对齐）

> 本节给出 Save/Load 的最小 SQLite schema 规范，迁移规则与审计要求。

### 11.9.1 表：schema_version（单行）

- `id`：主键，固定为 1
- `version`：当前 `schemaVersion`（int，>=1）
- `updated_at_utc`：ISO8601 文本（或 INTEGER epoch；实现中需固定一致）

### 11.9.2 表：save_slots（存档槽位索引）

- `save_id`：TEXT 主键（稳定 ID，不允许空）
- `created_at_utc`：TEXT（ISO8601）
- `updated_at_utc`：TEXT（ISO8601）
- `schema_version`：INTEGER
- `save_version`：INTEGER（逻辑快照版本，与 `schema_version` 区分）

### 11.9.3 表：save_kv（早期阶段的最小 payload）

- `save_id`：TEXT（FK -> save_slots.save_id）
- `k`：TEXT
- `v`：TEXT（JSON string 或 plain string；实现中需固定一致）
- `(save_id, k)`：组合唯一

### 11.9.4 迁移规则与失败审计

迁移失败必须写入 `security-audit.jsonl`，格式严格遵循 ADR-0019 五字段：

- `ts`
- `action`（例如 `db.migration.failed`）
- `reason`（Release/secure 模式不得包含绝对路径/SQL 原文）
- `target`（例如 `user://saves/game.db`）
- `caller`（例如 `SqliteDataStore` / `MigrationRunner`）
