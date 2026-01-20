---
PRD-ID: PRD-Guild-Manager
PRD-Refs:
  - docs/prd.txt
Story-ID: PRD-GUILD-MANAGER-CORE-AI-COORDINATOR
Title: Feature Slice - AI Coordinator（AI 协调器）
Status: Delivered
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

本页作为“AI 协调器（AICoordinator）”纵切的审计锚点，用于约束 AI 行为编排与回合阶段之间的契约边界。

## 契约（领域事件 type）

来自当前 `tasks_gameplay.json` 的 `contractRefs`：

- `core.ai.cycle.started`
- `core.ai.intent.issued`
- `core.ai.cycle.completed`
- `core.game_turn.phase_changed`

契约索引与定义位置：`08-Contracts-Index.md`、`08-Contracts-CloudEvents-Core.md`。

## 契约定义（规划）

> 说明：以下字段为当前阶段的“最小强类型契约”，用于稳定跨模块消费与验收挂钩；后续若发生不兼容变更，应按 ADR-0004 变更 `EventType`（或引入版本化 type）。

### 事件

- **AiCycleStarted** (`core.ai.cycle.started`)
  - 触发时机：AI Simulation 阶段开始（与回合阶段解耦，仅由 Core 发布）
  - 字段：`SaveId`, `Week`, `StartedAt`
  - 契约位置：`Game.Core/Contracts/AI/AiCycleStarted.cs`
- **AiIntentIssued** (`core.ai.intent.issued`)
  - 触发时机：AI 生成可被下游消费的“意图”（例如招募/袭击/社交等）
  - 字段：`SaveId`, `Week`, `IntentId`, `IntentType`, `ActorId`, `TargetId`, `IssuedAt`
  - 契约位置：`Game.Core/Contracts/AI/AiIntentIssued.cs`
- **AiCycleCompleted** (`core.ai.cycle.completed`)
  - 触发时机：AI Simulation 阶段结束
  - 字段：`SaveId`, `Week`, `IntentsIssued`, `CompletedAt`
  - 契约位置：`Game.Core/Contracts/AI/AiCycleCompleted.cs`
- **GameTurnPhaseChanged** (`core.game_turn.phase_changed`)
  - 触发时机：回合阶段推进（用于驱动 AI 周期的挂钩点）
  - 契约位置：`Game.Core/Contracts/GameLoop/GameTurnPhaseChanged.cs`

## 验收与测试（规则）

- AI 意图必须通过领域事件发布，禁止 Core 内部模块直接相互调用造成隐式依赖。
- 本次纵切已在仓库落地对应契约文件与单测挂钩，并通过确定性门禁。

## Test-Refs

- `Game.Core.Tests/Domain/AICoordinatorTests.cs`
- `Game.Core.Tests/Domain/AICoordinatorPerformanceTests.cs`
- `Game.Core.Tests/Services/AICoordinatorConcurrencyTests.cs`
