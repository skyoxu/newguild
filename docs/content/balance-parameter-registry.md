# Balance Parameter Registry（最小数值参数表）

## 定位

本文件是“参数注册表”的人读版本：说明参数的含义、单位、范围与生效范围。

机器可读版本见：

- `Game.Godot/Assets/Data/Balance/balance_params.default.json`

注意：T3 初期只需要最小参数集，避免过早做“大而全”的平衡表拖慢交付。

## 参数（最小集）

| key | default | unit | min | max | scope | 说明 |
|---|---:|---|---:|---:|---|---|
| `turn.week_duration_seconds` | 0 | seconds | 0 | 0 | core | 回合代表“游戏内一周”，该参数仅占位（由回合系统定义，不做真实计时） |
| `event.default_weight` | 1.0 | ratio | 0.0 | 10.0 | content | 事件默认权重（内容未指定 weight 时使用） |
| `recruitment.base_success_chance` | 0.35 | ratio | 0.0 | 1.0 | core | 招募基础成功率（后续由成员属性/声望修正） |
| `roster.max_members_soft` | 50 | count | 1 | 500 | core | 成员上限软门（用于 UI/提示，不作为硬拒绝） |
| `ai.guild_action_budget_per_turn` | 5 | count | 0 | 100 | ai | NPC 公会每回合行动预算（用于限制计算量） |

