---
PRD-ID: PRD-Guild-Manager
PRD-Refs:
  - docs/prd.txt
Story-ID: PRD-GUILD-MANAGER-T3-SOCIAL
Title: Feature Slice - T3 Social（社交/关系）
Status: Planned
ADR-Refs:
  - ADR-0004
  - ADR-0005
  - ADR-0007
  - ADR-0018
Arch-Refs:
  - CH01
  - CH04
  - CH06
---

本页作为 T3“社交/关系”纵切的审计锚点。

## 契约（领域事件 type）

来自当前 `tasks_gameplay.json` 的 `contractRefs`：

- `core.social.interaction.triggered`
- `core.social.relationship.changed`
- `core.guild.member.joined`
- `core.guild.member.left`
- `core.game_turn.week_advanced`

契约索引与定义位置：`08-Contracts-Index.md`、`08-Contracts-CloudEvents-Core.md`。

## 契约定义（规划）

### 事件

- **SocialInteractionTriggered** (`core.social.interaction.triggered`)
  - 触发时机：触发一次社交交互（聊天/冲突/合作等）
  - 字段：`InteractionId`, `GuildId`, `ActorId`, `TargetId`, `InteractionType`, `TriggeredAt`
  - 契约位置：`Game.Core/Contracts/Social/SocialInteractionTriggered.cs`
- **SocialRelationshipChanged** (`core.social.relationship.changed`)
  - 触发时机：关系值变化（用于驱动媒体/声望等下游模块）
  - 字段：`GuildId`, `SubjectId`, `OtherId`, `OldValue`, `NewValue`, `ChangedAt`
  - 契约位置：`Game.Core/Contracts/Social/SocialRelationshipChanged.cs`

## 验收与测试（规则）

- 社交事件必须可被媒体/声望模块复用（跨模块解耦）。
- 当进入交付阶段时，必须补齐 `Game.Core/Contracts/Social/**` 与测试挂钩。
