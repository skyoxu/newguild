---
Title: Acceptance Semantics Methodology
Status: Proposed
Scope: newguild task views (tasks_back / tasks_gameplay)
---

# Acceptance 语义方法论（用于“反向抽取义务”与审计）

目的：把“任务验收（acceptance）”从主观描述变为可审计的结构化语义，使我们能做两件事：
1) 反向推导“任务必须做什么”（obligations），作为后续对齐锚点；
2) 发现 acceptance 过少/漏项导致的“回链齐全但接线漏了”。

约束：
- Windows-only；产物统一落盘到 `logs/`。
- `contractRefs` / `test_refs` / `artifactRefs` 等字段语义，以 `docs/workflows/acceptance-check-and-llm-review.md` 为 SSoT。

---

## 1. 输入来源（事实优先）

反向抽取时只信任以下事实源（按优先级）：
1) 视图任务（SSoT）：`.taskmaster/tasks/tasks_gameplay.json` 与 `.taskmaster/tasks/tasks_back.json`
2) Master 任务（语义源）：`.taskmaster/tasks/tasks.json` 的 `title/description/subtasks/details/testStrategy`
3) Contracts（事件名口径）：`Game.Core/Contracts/**` 的 `EventType` 常量
4) Overlays（章节定位）：`docs/architecture/overlays/**/08/*.md`

---

## 2. Obligation 分类（审计维度）

对每个任务输出以下类别的“必须做什么”清单（每条 obligation 应可被证据锚定）：

- Functional：功能行为与用户可见结果（包含 UI 接线时也算功能）
- Contracts：会发布/消费的领域事件（以 `contractRefs` 为最小覆盖定义）
- Tests：必须存在的测试（以 `test_refs` 为事实锚点；策略在 `test_strategy`）
- Observability：日志/审计/可观测产物（以 `artifactRefs` 或脚本输出路径作为锚点）
- Wiring：跨层连接（Core↔Adapters↔Scenes/Signals），包含“可点击、可进入、可见状态”
- Gates：质量门禁/依赖护栏（脚本化、可复现、可阻断）

---

## 3. “最小覆盖定义”（强规则）

### 3.1 contractRefs（领域事件）

- UI 订阅事件的任务：`contractRefs` 必须覆盖 UI 真正消费的事件（例如 UI feed/filter/render 使用的事件 type）。
- Core 计算点任务：`contractRefs` 必须覆盖 Core 会发布的事件。

这条规则是为了避免“文档回链齐全，但实际接线漏了”。

### 3.2 acceptance（验收条目）最低信息量

反向抽取会判定 acceptance 是否“过少/不足”。最低信息量建议：
- 至少包含 3 个维度：Functional + Tests +（Contracts 或 Wiring）
- 若 `contractRefs` 非空：acceptance 应明确“何时发布/何处消费/可观测输出”
- 若 `layer` 为 `adapter`：acceptance 应包含“可点击/可进入/不会被透明层挡住”等可用性断言

---

## 4. 输出（审计锚点）

脚本 `scripts/python/llm_extract_task_obligations.py` 会输出：
- `logs/ci/<YYYY-MM-DD>/acceptance-obligations/obligations.json`
- `logs/ci/<YYYY-MM-DD>/acceptance-obligations/obligations.md`

并在报告中标记：
- obligations（任务必须做什么）
- acceptance gaps（acceptance 过少或缺少关键维度）
- follow-ups（建议回填到视图文件的 acceptance/test_strategy/test_refs 的位置）

