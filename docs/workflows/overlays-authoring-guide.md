# overlays 编写与维护指南（newguild 口径）

本指南面向当前仓库（Windows-only Godot 4.5 + C#）。目标是让 overlays 成为“可执行规范”的载体：能被任务视图引用、能被脚本确定性校验、能与 Contracts/测试证据链对齐，并且避免口径漂移。

## 0. 核心结论（先记住这几条）

1) overlays 不是 PRD 的复制粘贴，也不是 Tasks 的替代；它是“功能纵切（08）”的落点，强调边界、事件、验收与测试证据链。
2) 08 章只写在 overlays：`docs/architecture/overlays/<PRD-ID>/08/`；Base 不写具体模块内容（Base 仅保留模板/写作约束）。
3) 合约（Contracts）SSoT 在代码：`Game.Core/Contracts/**`；文档只引用路径与 `EventType`，不复制字段定义。
4) 任务语义 SSoT 在任务文件：
   - master：`.taskmaster/tasks/tasks.json`
   - view：`.taskmaster/tasks/tasks_back.json`、`.taskmaster/tasks/tasks_gameplay.json`
   overlays 必须可被 view 回链引用并通过脚本校验。
5) 阈值/策略（安全、可观测性、质量门禁）以 Base + Accepted ADR 为准；overlays 只引用，不复制。

## 1. overlays 的目录结构（固定）

每个 PRD-ID 一套 overlays（只建议一个 08 目录）：

```
docs/
  architecture/
    overlays/
      <PRD-ID>/
        08/
          _index.md
          ACCEPTANCE_CHECKLIST.md
          08-FeatureSlice-<Topic>.md
          08-Contracts-*.md
```

命名建议：

- 文件名使用英文（路径稳定、避免 CI/Windows 的编码与 diff 差异风险）。
- 标题/正文使用中文（便于团队阅读）。
- 按任务拆页时，优先 `08-FeatureSlice-<Topic>.md` 或 `08-T<task-id>-<slug>.md` 这种“按 Task 可定位”的命名。

## 2. front matter 约束（哪些必须有）

### 2.1 `_index.md`（建议有）

`_index.md` 用于导航与口径提示，建议包含 front matter：

```md
---
PRD-ID: <PRD-ID>
Title: 08 章功能纵切索引（契约与测试对齐）
Arch-Refs:
  - CH01
  - CH03
ADR-Refs:
  - ADR-0004
  - ADR-0005
Status: Proposed
---
```

### 2.2 `ACCEPTANCE_CHECKLIST.md`（必须有）

该文件会被校验脚本检查（确定性门禁），必须包含 YAML front matter，且至少包含：

- `PRD-ID`
- `Title`
- `Status`
- `ADR-Refs`
- `Arch-Refs`
- `Test-Refs`

示例：

```md
---
PRD-ID: <PRD-ID>
Title: <标题>
Status: Proposed
ADR-Refs:
  - ADR-0004
  - ADR-0005
  - ADR-0019
Arch-Refs:
  - CH01
  - CH06
Test-Refs:
  - Game.Core.Tests/...
---
```

注意：

- `ADR-Refs` 必须指向 `docs/adr/ADR-*.md` 实际存在的 ADR。
- Checklist 的正文应只写“检查清单”，不要复制阈值/策略（引用 ADR/CH 即可）。

### 2.3 其他 08 页面（强烈推荐）

其他 08 页面建议包含 front matter，至少标记：

- `PRD-ID`
- `Title`
- `Status`
- `ADR-Refs`（至少 1 条 Accepted ADR）
- `Arch-Refs`（至少 1 个 CH）
- `Test-Refs`（仅当本页被用作“验收证据锚点”时需要；纯规划/模板页可省略）

补充规则（止损）：

- 若本页被 `.taskmaster/tasks/tasks_back.json` 或 `.taskmaster/tasks/tasks_gameplay.json` 的 `acceptance[].refs` / `test_refs` 引用，则必须补齐 `Test-Refs`，且路径必须为仓库内真实存在的测试文件（例如 `.cs` / `.gd`）。
- 若本页仅用于规划或作为索引的补充说明（`Status: Draft/Template`，且不承载验收证据链），可不写 `Test-Refs`，避免为了“凑路径”而编造测试文件。

## 3. overlays 08 应该写什么（写作边界）

overlays 的价值是“能落地且可审计”。建议每页只写以下 4 类信息：

1) 范围/非目标：止损边界（避免模块无限膨胀）。
2) 确定性输入：配置路径、seed、选项档位（保证可复现与可测）。
3) 事件口径（ADR-0004）：只列 `EventType` + 触发点，不在文档复制字段定义。
4) 验收条款与证据链：验收条款必须能落到 view 任务的 `acceptance[]`，并通过测试文件 `Refs:` 与 `ACC:T<id>.<n>` anchors 证明。

禁止项（高频踩坑）：

- 把 Base/ADR 的阈值/策略复制进 overlays（会漂移）。
- 把 Contracts 的字段定义复制进 overlays（会漂移）。
- 把“个人操作步骤/命令习惯”写成验收条款（应放到 `test_strategy` 或 workflow 文档）。

## 4. Contracts 与事件命名（必须对齐）

### 4.1 事件命名规则（ADR-0004）

事件命名遵循 CloudEvents-like `type`：

- 领域事件：`core.*`（进入 `contractRefs` / overlay 08 / 跨模块消费时必须是领域事件）
- 安全/审计事件：`security.*`（同样需要 `Game.Core/Contracts/**` 可解析的 `EventType` 常量）
- UI / Screen：`ui.*`、`screen.*`（不进入领域 `contractRefs`，除非你明确把它定义为跨层契约）

### 4.2 合约落盘位置（SSoT）

Contracts（事件/DTO/接口）只落盘到：

- `Game.Core/Contracts/**`（纯 C#，不依赖 Godot API）

overlays 只做引用：

- 写 `EventType`
- 写触发点
- 写契约文件路径（例如 `Game.Core/Contracts/Guild/GuildMemberJoined.cs`）

### 4.3 “最小覆盖”回链要求（止损规则）

- UI 订阅事件的任务：`contractRefs` 必须覆盖 UI 真正消费的事件。
- Core 计算点任务：`contractRefs` 必须覆盖会发布的事件。

## 5. 任务视图如何引用 overlays（推荐绑定规则）

任务视图文件：

- `.taskmaster/tasks/tasks_back.json`（契约/验收视图）
- `.taskmaster/tasks/tasks_gameplay.json`（玩法实现视图）

建议每个任务包含：

- `overlay_refs` 至少包含：
  - `docs/architecture/overlays/<PRD-ID>/08/_index.md`
  - `docs/architecture/overlays/<PRD-ID>/08/ACCEPTANCE_CHECKLIST.md`
  - 该任务对应的纵切页（例如 `08-FeatureSlice-<Topic>.md`）
- `contractRefs`：本任务关心的领域/安全事件 `EventType`（只列 type，不列字段）。
- `acceptance[]`：每条验收条款以 `Refs: <repo-relative-test-path>` 结尾。
- `test_refs[]`：包含 acceptance 中所有 `Refs:` 的并集（且必须是仓库内真实路径）。
- `artifactRefs`：门禁/产物锚点（允许占位，不参与 test_refs 的“存在性”硬规则）。

## 6. 创建一套新的 overlays（一步步）

以新 PRD-ID（示例：`PRD-XXX`）为例：

1) 建目录：
   - `docs/architecture/overlays/PRD-XXX/08/`
2) 写索引：
   - `docs/architecture/overlays/PRD-XXX/08/_index.md`
3) 写验收清单：
   - `docs/architecture/overlays/PRD-XXX/08/ACCEPTANCE_CHECKLIST.md`
4) 写纵切页：
   - `08-FeatureSlice-<Topic>.md`（按模块/闭环）
   - `08-Contracts-*.md`（按契约专题/口径）
5) 回填 view 任务（最关键）：
   - 把每个 Task 的 `overlay_refs` 指到 `_index.md`、`ACCEPTANCE_CHECKLIST.md`、以及该任务对应页。
6) 跑确定性校验，确保回链不漂移（见下一节）。

## 7. 推荐的确定性校验（Windows）

执行顺序建议：

1) overlays 回链与 checklist schema：
   - `py -3 scripts/python/validate_task_overlays.py`
2) 任务回链与引用完整性：
   - `py -3 scripts/python/task_links_validate.py`
3) 合约回链（防漂移）：
   - `py -3 scripts/python/validate_contracts.py`
4) 视图语义校验（Refs/contractRefs）：
   - `py -3 scripts/python/validate_view_ref_semantics.py`
   - `py -3 scripts/python/audit_view_contractrefs_vs_contracts.py`
5) 生成对齐材料（非 SSoT）：
   - `py -3 scripts/python/generate_contracts_catalog.py --prd-id <PRD-ID>`

取证要求：

- 所有输出统一落到 `logs/ci/<YYYY-MM-DD>/`（详见 `AGENTS.md` 6.3）。
