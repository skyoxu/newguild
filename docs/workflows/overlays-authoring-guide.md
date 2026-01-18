# overlays 编写与维护指南（newguild 口径）

本指南面向当前仓库（Windows-only Godot 4.5 + C#）。目标是让 `docs/architecture/overlays/**` 成为“可执行规范”的载体：能被任务视图引用、能被脚本确定性校验、能与 Contracts/测试证据对齐，并且 overlays 自身具备防漂移能力。

## 0. 核心结论（止损优先）

1) overlays 不是 PRD 的复制粘贴，也不是 Tasks 的替代；它是“功能纵切（08）”的落点，强调边界、事件、验收与证据链。
2) `<PRD-ID>` 在 overlays 路径中被视为“命名空间/范围 ID（namespace/scope id）”：它用于稳定分组与回链锚点，不要求强绑定到某份 PRD 文档内容，更不要求 overlays 承载 PRD 细节。
3) 08 章只写在 overlays：`docs/architecture/overlays/<PRD-ID>/08/`；base 不写任何具体模块内容（base 仅保留模板与写作约束）。
4) Contracts 的单一事实源（SSoT）在代码：`Game.Core/Contracts/**`。overlays 只写 `EventType`、触发时机、以及契约文件路径；禁止复制字段定义，避免口径漂移。
5) 任务语义 SSoT 在任务文件：
   - master：`.taskmaster/tasks/tasks.json`
   - view：`.taskmaster/tasks/tasks_back.json`、`.taskmaster/tasks/tasks_gameplay.json`
   overlays 只需要提供稳定锚点，支持 view 回链与脚本校验；不要求 overlays 承载任务/契约细节。
6) 阈值/策略（安全、可观测性、质量门禁）以 Base + Accepted ADR 为准；overlays 只引用，不复制。

## 1. overlays 目录结构（固定）

每个 `<PRD-ID>` 一套 overlays（建议只有一个 `08/` 目录）：

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
- 文件名使用英文（路径稳定，降低 CI/Windows 编码与 diff 风险）。
- 标题/正文使用中文（便于团队阅读）。
- 按任务拆页时，优先 `08-FeatureSlice-<Topic>.md` 或 `08-T<task-id>-<slug>.md` 这类“按 Task 可定位”的命名。

## 2. front matter 约束（哪些必须有）

### 2.1 `08/_index.md`（强烈建议有）

用于导航与口径提示；建议包含 YAML front matter：

```md
---
PRD-ID: <PRD-ID> # 作为命名空间/范围 ID
Title: 08 功能纵切索引（契约与测试对齐）
Status: Draft
ADR-Refs:
  - ADR-0004
  - ADR-0005
Arch-Refs:
  - CH01
---
```

### 2.2 `08/ACCEPTANCE_CHECKLIST.md`（必须有）

该文件会被脚本校验（确定性门禁）。必须包含 YAML front matter，至少包含：
- `PRD-ID`
- `Title`
- `Status`
- `ADR-Refs`
- `Arch-Refs`
- `Test-Refs`（用于“验收证据链锚点”时必须维护；只做模板/索引补充时可以为空，但要明确标注）

### 2.3 其他 08 页面（推荐）

其他 08 页面建议包含 front matter，至少标注：
- `PRD-ID`
- `Title`
- `Status`
- `ADR-Refs`（至少 1 条 Accepted ADR）
- `Arch-Refs`（至少 1 个 CH）

止损规则（与当前仓库真实格式对齐）：
- 本项目 view 任务的 `acceptance` 是“字符串数组”，不是结构化对象；每条验收末尾使用 `Refs:` 标注证据文件路径（仓库相对路径，必须真实存在），例如：`... Refs: Game.Core.Tests/...Tests.cs`
- 若某个 overlays 页面被 view 任务 `overlay_refs` 引用，且该页面用于承载“验收证据链锚点”，则建议在该页面 front matter 里维护 `Test-Refs`（与 view 的 `Refs:` 指向一致）。
- 若 overlays 页面仅用于规划/索引补充（`Status: Draft/Template` 且不承载验收证据链），可以不填 `Test-Refs`，避免为了“必须存在”而编造测试文件。

## 3. overlays 08 写作边界（不引入新复杂度）

overlays 的价值是“能落地且可审计”，但不要求它承载任务/契约细节。建议每页只写以下 4 类信息：

1) 范围/非目标：明确止损边界，避免模块无边界膨胀。
2) 确定性输入：配置路径、seed、开关、数据口等（保证可复现与可测）。
3) 事件口径（ADR-0004）：只列 `EventType` + 触发时机；不在文档复制字段定义。
4) 验收条款与证据链：验收条款应能落到 view 任务的 `acceptance[]`，并通过测试文件末尾的 `Refs:`（路径存在）与测试内的 `ACC:T<id>.<n>` anchors（内容对齐）提供证据。

禁止项（高频踩坑）：
- 把 Base/ADR 的阈值与策略复制进 overlays（会漂移）。
- 把 Contracts 的字段定义复制进 overlays（会漂移）。
- 把“个人操作步骤/命令习惯”写成验收条款（应放到 `test_strategy` 或 workflow 文档）。

## 4. Contracts SSoT 与事件命名（ADR-0004）

事件命名遵循 CloudEvents-like `type`：
- 领域事件：`core.*`
- 安全/审计事件：`security.*`
- UI/Screen：`ui.*`、`screen.*`（默认不进入领域 `contractRefs`，除非明确将其定义为跨层契约）

Contracts（事件/DTO/接口）只落盘到：
- `Game.Core/Contracts/**`（纯 C#，不依赖 Godot API）

overlays 只做引用：
- 写 `EventType`
- 写触发点
- 写契约文件路径（例如 `Game.Core/Contracts/Guild/GuildMemberJoined.cs`）

## 5. 任务视图如何引用 overlays（回链规则）

任务视图文件：
- `.taskmaster/tasks/tasks_back.json`（契约/验收视图）
- `.taskmaster/tasks/tasks_gameplay.json`（玩法实现视图）

建议每个任务包含：
- `overlay_refs` 至少包含：
  - `docs/architecture/overlays/<PRD-ID>/08/_index.md`
  - `docs/architecture/overlays/<PRD-ID>/08/ACCEPTANCE_CHECKLIST.md`
  - 该任务对应的纵切页面（例如 `08-FeatureSlice-<Topic>.md`）
- `contractRefs`：本任务关心的领域/安全事件 `EventType`（只列 type，不列字段）。
- `acceptance[]`：字符串数组；每条验收条款以 `Refs: <repo-relative-test-path>` 结尾（路径必须真实存在）。
- `test_refs[]`：包含 acceptance 中所有 `Refs:` 的并集（必须是仓库内真实路径）。
- `artifactRefs`：门禁/产物锚点（允许占位；不参与 `test_refs` 的“必须存在”硬规则）。

## 6. 推荐的确定性校验顺序（Windows）

建议顺序（只列核心）：
1) overlays 回链与 checklist schema：
   - `py -3 scripts/python/validate_task_overlays.py`
2) 任务回链与引用完整性：
   - `py -3 scripts/python/task_links_validate.py`
3) Contracts 回链（防漂移）：
   - `py -3 scripts/python/validate_contracts.py`
4) 视图语义（Refs/contractRefs）：
   - `py -3 scripts/python/validate_view_ref_semantics.py`
   - `py -3 scripts/python/audit_view_contractrefs_vs_contracts.py`

