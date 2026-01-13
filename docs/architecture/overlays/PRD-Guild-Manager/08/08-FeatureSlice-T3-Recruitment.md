---
PRD-ID: PRD-Guild-Manager
PRD-Refs:
  - docs/prd.txt
Story-ID: PRD-GUILD-MANAGER-T3-RECRUITMENT
Title: Feature Slice - T3 Recruitment（招募系统）
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

本页作为 T3“招募系统”纵切的审计锚点。该纵切通常会产生新的领域事件与 DTO，必须遵循 ADR-0004/ADR-0020 的契约落盘规则。

## 契约（领域事件 type）

来自当前 `tasks_gameplay.json` 的 `contractRefs`：

- `core.recruitment.offer.presented`
- `core.recruitment.offer.resolved`
- `core.guild.member.joined`
- `core.game_turn.week_advanced`

契约索引与定义位置：`08-Contracts-Index.md`、`08-Contracts-CloudEvents-Core.md`。

## 契约定义（规划）

### 事件

- **RecruitmentOfferPresented** (`core.recruitment.offer.presented`)
  - 触发时机：向玩家公会展示一条招募 Offer
  - 字段：`OfferId`, `GuildId`, `CandidateId`, `Role`, `PresentedAt`
  - 契约位置：`Game.Core/Contracts/Recruitment/RecruitmentOfferPresented.cs`
- **RecruitmentOfferResolved** (`core.recruitment.offer.resolved`)
  - 触发时机：招募 Offer 被接受/拒绝/过期
  - 字段：`OfferId`, `GuildId`, `CandidateId`, `Decision`, `Reason`, `ResolvedAt`
  - 契约位置：`Game.Core/Contracts/Recruitment/RecruitmentOfferResolved.cs`

## 验收与测试（规则）

- Offer 的呈现/决议必须可重放（相同输入不应产生不可测的漂移）。
- 当进入交付阶段时，必须补齐对应的 `Game.Core/Contracts/Recruitment/**` 与 xUnit 测试挂钩。
