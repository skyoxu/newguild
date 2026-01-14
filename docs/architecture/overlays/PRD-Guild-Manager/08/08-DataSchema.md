---
PRD-ID: PRD-Guild-Manager
PRD-Refs:
  - docs/prd.txt
Title: 08 Data Schema（T3 前置：数据字典与 Schema 规范）
Status: Draft
Arch-Refs:
  - CH05
  - CH07
ADR-Refs:
  - ADR-0006
  - ADR-0005
  - ADR-0019
---

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
- 本仓库的内容 JSON 属于“工作文件”，字段值应保持英文（或使用 `nameKey` 这类 key），避免把中文直接写进数据文件导致审计/编码/工具链不一致。
- 若必须在早期阶段展示中文：应通过文本表或资源文件实现，而不是写死在内容包 JSON 中。

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

### 11.3.0 文件通用外壳（File Envelope）

所有 Base 内容文件都必须包含：

- `contentVersion`: string（语义版本，例如 `"1.0.0"`）

本项目的 Base 内容包入口：

- `Game.Godot/Assets/Data/content/base/manifest.json`

### 11.3.1 WeightedRef（权重候选）

- `id`: string
- `weight`: int（>=0）
- `min`: int（默认 1，可选）
- `max`: int（默认 1，可选）

JSON Schema（片段）：

```json
{
  "type": "object",
  "required": ["id", "weight"],
  "properties": {
    "id": { "type": "string" },
    "weight": { "type": "integer", "minimum": 0 },
    "min": { "type": "integer", "minimum": 1 },
    "max": { "type": "integer", "minimum": 1 }
  }
}
```

### 11.3.2 RangeInt（整数区间）

- `min`: int
- `max`: int

约束：

- `min <= max`

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

JSON Schema（片段）：

```json
{
  "type": "object",
  "required": ["type"],
  "properties": {
    "type": { "type": "string" },
    "refId": { "type": "string" },
    "value": { "type": ["integer", "string", "boolean"] },
    "chance": { "type": "number", "minimum": 0.0, "maximum": 1.0 }
  }
}
```

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

JSON Schema（片段）：

```json
{
  "type": "object",
  "required": ["type"],
  "properties": {
    "type": { "type": "string" },
    "refId": { "type": "string" },
    "amount": { "type": ["integer", "number"] },
    "role": { "type": "string" },
    "value": {}
  }
}
```

### 11.3.4 RoleRatio（角色比例）

用于阵容/招募偏好等需要“坦克/治疗/输出比例”的地方：

- `tank`: number（`0..1`）
- `healer`: number（`0..1`）
- `dps`: number（`0..1`）

备注：

- 早期阶段不强制三者相加等于 1，但建议保持接近 1 以便解释。

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

建议 JSON Schema（片段，文件级）：

```json
{
  "type": "object",
  "required": ["contentVersion", "guildEvents"],
  "properties": {
    "contentVersion": { "type": "string" },
    "guildEvents": { "type": "array", "items": { "type": "object" } }
  }
}
```

## 11.6 MemberArchetypes（成员原型）

最小字段建议：

- `id` / `nameKey`
- `role`（Tank/Healer/DPS）
- `personalityTags`（用于事件条件）
- `baseRatings`（用于 AI/招募/战斗的最小评分口径）

样例文件：

- `Game.Godot/Assets/Data/content/base/member_archetypes.json`

建议 JSON Schema（片段，文件级）：

```json
{
  "type": "object",
  "required": ["contentVersion", "memberArchetypes"],
  "properties": {
    "contentVersion": { "type": "string" },
    "memberArchetypes": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["id", "nameKey", "role"],
        "properties": {
          "id": { "type": "string" },
          "nameKey": { "type": "string" },
          "role": { "enum": ["Tank", "Healer", "DPS"] },
          "personalityTags": { "type": "array", "items": { "type": "string" } },
          "baseRatings": { "type": "object" }
        }
      }
    }
  }
}
```

## 11.7 NpcGuildArchetypes（NPC 公会原型）

最小字段建议：

- `id` / `nameKey` / `style`
- `recruitmentPreferences`（Tank/Healer/DPS 小数占比）
- `tacticPreferences`（引用战术 id）
- `reputationBaseline`

样例文件：

- `Game.Godot/Assets/Data/content/base/npc_guilds.json`

建议 JSON Schema（片段，文件级）：

```json
{
  "type": "object",
  "required": ["contentVersion", "npcGuildArchetypes"],
  "properties": {
    "contentVersion": { "type": "string" },
    "npcGuildArchetypes": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["id", "nameKey", "style", "recruitmentPreferences"],
        "properties": {
          "id": { "type": "string" },
          "nameKey": { "type": "string" },
          "style": { "type": "string" },
          "recruitmentPreferences": { "type": "object" },
          "tacticPreferences": { "type": "array", "items": { "type": "string" } },
          "reputationBaseline": { "type": "integer" }
        }
      }
    }
  }
}
```

## 11.8 RecruitOffers（招募模板）

最小字段建议：

- `id` / `nameKey` / `role`
- `difficultyTier`
- `baseSuccessChance`（小数）
- `effectsOnSuccess` / `effectsOnFail`（复用 Effect 结构）

样例文件：

- `Game.Godot/Assets/Data/content/base/recruit_offers.json`

建议 JSON Schema（片段，文件级）：

```json
{
  "type": "object",
  "required": ["contentVersion", "recruitOffers"],
  "properties": {
    "contentVersion": { "type": "string" },
    "recruitOffers": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["id", "nameKey", "role", "difficultyTier", "baseSuccessChance"],
        "properties": {
          "id": { "type": "string" },
          "nameKey": { "type": "string" },
          "role": { "enum": ["Tank", "Healer", "DPS"] },
          "difficultyTier": { "type": "integer", "minimum": 1 },
          "baseSuccessChance": { "type": "number", "minimum": 0.0, "maximum": 1.0 },
          "effectsOnSuccess": { "type": "array", "items": { "type": "object" } },
          "effectsOnFail": { "type": "array", "items": { "type": "object" } }
        }
      }
    }
  }
}
```

## 11.10 RaidEncounters（PVE 遭遇）

最小字段建议：

- `id` / `nameKey`
- `difficultyTier`
- `recommendedRoleRatio`（Tank/Healer/DPS 小数占比）
- `successReputationDelta` / `failMoraleDelta`（min/max）

样例文件：

- `Game.Godot/Assets/Data/content/base/raid_encounters.json`

建议 JSON Schema（片段，文件级）：

```json
{
  "type": "object",
  "required": ["contentVersion", "raidEncounters"],
  "properties": {
    "contentVersion": { "type": "string" },
    "raidEncounters": { "type": "array", "items": { "type": "object" } }
  }
}
```

## 11.11 Tactics（战术模板）

最小字段建议：

- `id` / `nameKey`
- `tags`
- `modifiers`（只允许小数概率或整数增量；不得混用百分号表示）

样例文件：

- `Game.Godot/Assets/Data/content/base/tactics.json`

建议 JSON Schema（片段，文件级）：

```json
{
  "type": "object",
  "required": ["contentVersion", "tactics"],
  "properties": {
    "contentVersion": { "type": "string" },
    "tactics": { "type": "array", "items": { "type": "object" } }
  }
}
```

## 11.12 MediaBeats（媒体事件）

最小字段建议：

- `id` / `nameKey`
- `weight`
- `reputationDelta`（min/max）

样例文件：

- `Game.Godot/Assets/Data/content/base/media_beats.json`

建议 JSON Schema（片段，文件级）：

```json
{
  "type": "object",
  "required": ["contentVersion", "mediaBeats"],
  "properties": {
    "contentVersion": { "type": "string" },
    "mediaBeats": { "type": "array", "items": { "type": "object" } }
  }
}
```

## 11.13 SocialInteractions（社交互动）

最小字段建议：

- `id` / `nameKey`
- `weight`
- `effects`（复用 Effect 结构）

样例文件：

- `Game.Godot/Assets/Data/content/base/social_interactions.json`

建议 JSON Schema（片段，文件级）：

```json
{
  "type": "object",
  "required": ["contentVersion", "socialInteractions"],
  "properties": {
    "contentVersion": { "type": "string" },
    "socialInteractions": { "type": "array", "items": { "type": "object" } }
  }
}
```

## 11.14 未来内容类别（T4+ 规划锚点，非 T3 交付）

> 说明：这些类别在 `docs/content/stage-content-inventory.md` 里被纳入后续阶段（R4.x），用于“内容规模与数据结构”收敛。
> 这里先给出数据字典的最小字段建议与文件级 schema 要求，避免后续内容扩容时结构漂移。

### 11.14.1 Achievements（成就）

- 文件建议：`Game.Godot/Assets/Data/content/base/achievements.json`
- 最小字段建议：`id`, `nameKey`, `category`, `trigger`, `rewards`

文件级 JSON Schema（片段）：

```json
{
  "type": "object",
  "required": ["contentVersion", "achievements"],
  "properties": {
    "contentVersion": { "type": "string" },
    "achievements": { "type": "array", "items": { "type": "object" } }
  }
}
```

### 11.14.2 Milestones（历史里程碑）

- 文件建议：`Game.Godot/Assets/Data/content/base/milestones.json`
- 最小字段建议：`id`, `nameKey`, `conditions`, `effects`

### 11.14.3 Facilities（公会设施类型与升级曲线）

- 文件建议：`Game.Godot/Assets/Data/content/base/facilities.json`
- 最小字段建议：`id`, `nameKey`, `kind`, `levels[]`（每级成本/效果）

### 11.14.4 Diplomacy / Alliances（外交与联盟事件池）

- 文件建议：`Game.Godot/Assets/Data/content/base/diplomacy_events.json`
- 最小字段建议：`id`, `nameKey`, `weight`, `conditions`, `effects`

### 11.14.5 WorldBoss（世界 Boss 与难度/奖励配置）

- 文件建议：`Game.Godot/Assets/Data/content/base/world_bosses.json`
- 最小字段建议：`id`, `nameKey`, `difficulty`, `rewardTable`

### 11.14.6 Economy（拍卖/公会银行的“内容侧”）

- 文件建议：
  - `Game.Godot/Assets/Data/content/base/auction_items.json`
  - `Game.Godot/Assets/Data/content/base/bank_items.json`
- 最小字段建议：`id`, `nameKey`, `rarity`, `baseValue`（以及按系统需要扩展）

## 11.15 文件清单（Base 内容包）

Base 内容包的文件列表由 `manifest.json` 决定，当前最小样例如下：

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
