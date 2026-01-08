# Global Tuning（全局节奏参数）

> 说明：本文件按“Global Tuning.txt”模板做了本项目适配，给出“可解释、可校验、可迭代”的最小数值口径。
>
> 机器可读版本见：`Game.Godot/Assets/Data/content/base/tuning.json`

## 13.1 Tick 与结算（Turn / Loop）

本项目是周回合制（每回合=游戏内一周），但仍需要统一“引擎内刷新/模拟节奏”：

- `Global_UIRefreshSeconds`：UI 刷新频率（建议 `0.2 ~ 0.5`）
- `AI_ActionBudgetPerTurn`：NPC 公会每回合行动预算（用于限制计算量）

## 13.2 概率与权重口径

- 概率统一用小数：`0.1 = 10%`
- 事件/候选抽样权重统一用整数 `weight`（`>=0`）

## 13.3 招募（Recruitment Tuning）

建议最小参数（T3 之前先固化“可解释”口径）：

- `Recruitment_BaseSuccessChance`：基础成功率（建议 `0.2 ~ 0.5`）
- `Recruitment_NegotiationMaxRounds`：谈判轮数上限（建议 `2 ~ 5`）
- `Recruitment_CooldownWeeks`：招募冷却（建议 `0 ~ 2`）

## 13.4 成员与阵容（Roster / Role）

- `Roster_MaxMembersSoft`：成员上限软门（建议 `30 ~ 80`）
- `Roster_RoleRatioTargets`：阵容角色目标比例（Tank/Healer/DPS）

## 13.5 PVE（Raid / Encounter）

T3 早期建议只做“可见结果”与“可解释反馈”，避免过早引入复杂战斗模型：

- `Raid_BaseSuccessChance`：基础成功率（建议 `0.4 ~ 0.7`，后续由阵容/战术修正）
- `Raid_RewardReputationMin/Max`：胜利声望区间（建议 `1 ~ 5`）
- `Raid_PenaltyMoraleMin/Max`：失败士气惩罚区间（建议 `-5 ~ -1`）

## 13.6 媒体与声望（Media / Reputation）

- `Media_PostsPerTurnMin/Max`：每回合媒体动态数量（建议 `0 ~ 3`）
- `Reputation_DeltaPerEventMin/Max`：单事件声望变动区间（建议 `-5 ~ 5`）

## 13.7 快进（Fast Forward）

快进等价于“离线收益”的概念：对多周推进做上限与粒度控制，避免性能与数值漂移。

- `FastForward_MaxWeeks`：一次快进最大周数（建议 `4 ~ 52`）
- `FastForward_WeekChunk`：分块推进粒度（建议 `1`）

## 13.8 最小可用默认值（见 JSON）

默认值与字段结构以 `Game.Godot/Assets/Data/content/base/tuning.json` 为准；本文件只给出解释与建议区间。
