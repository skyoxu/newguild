---
PRD-ID: PRD-Guild-Manager
PRD-Refs:
  - docs/prd.txt
Story-ID: PRD-GUILD-MANAGER-T3-AI-ECOSYSTEM
Title: Feature Slice - T3 AI Ecosystem（NPC 公会生态）
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

本页作为 T3“NPC 公会生态（AI Ecosystem）”纵切的审计锚点。

## 契约（领域事件 type）

来自当前 `tasks_gameplay.json` 的 `contractRefs`：

- `core.ai.ecosystem.step.completed`
- `core.game_turn.week_advanced`
- `core.game_turn.phase_changed`

契约索引与定义位置：`08-Contracts-Index.md`、`08-Contracts-CloudEvents-Core.md`。

## 契约定义（规划）

### 事件

- **AiEcosystemStepCompleted** (`core.ai.ecosystem.step.completed`)
  - 触发时机：NPC 公会生态完成一次“周推进/生态结算”步骤
  - 字段：`SaveId`, `Week`, `Summary`, `CompletedAt`
  - 契约位置：`Game.Core/Contracts/AI/AiEcosystemStepCompleted.cs`

## 验收与测试（规则）

- AI 生态推进必须与回合循环解耦，通过事件驱动跨模块消费。
- 当进入交付阶段时，必须补齐强类型契约与可重复的核心单测。
