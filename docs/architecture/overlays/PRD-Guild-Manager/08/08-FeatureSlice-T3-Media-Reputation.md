---
PRD-ID: PRD-Guild-Manager
PRD-Refs:
  - docs/prd.txt
Story-ID: PRD-GUILD-MANAGER-T3-MEDIA
Title: Feature Slice - T3 Media & Reputation（媒体/声望）
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

本页作为 T3“媒体/声望”纵切的审计锚点。

## 契约（领域事件 type）

来自当前 `tasks_gameplay.json` 的 `contractRefs`：

- `core.media.beat.triggered`
- `core.reputation.changed`
- `core.raid.resolved`
- `core.social.relationship.changed`

契约索引与定义位置：`08-Contracts-Index.md`、`08-Contracts-CloudEvents-Core.md`。

## 契约定义（规划）

### 事件

- **MediaBeatTriggered** (`core.media.beat.triggered`)
  - 触发时机：上游事件（raid/social 等）触发一条媒体/舆论内容
  - 字段：`BeatId`, `GuildId`, `SourceEventType`, `Headline`, `TriggeredAt`
  - 契约位置：`Game.Core/Contracts/Media/MediaBeatTriggered.cs`
- **ReputationChanged** (`core.reputation.changed`)
  - 触发时机：声望值变化（媒体事件、社交、战斗等引起）
  - 字段：`GuildId`, `OldValue`, `NewValue`, `Reason`, `ChangedAt`
  - 契约位置：`Game.Core/Contracts/Media/ReputationChanged.cs`

## 验收与测试（规则）

- 媒体事件应由 raid/social 等上游事件驱动，避免隐式耦合。
- 当进入交付阶段时，必须补齐契约文件与测试挂钩。
