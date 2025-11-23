# Task Master + SuperClaude 联合使用最佳实践

> **核心原则**: tasks.json 作为唯一事实来源 (SSoT)，Task Master 负责任务定义，SuperClaude 负责自动化实现与 Git 工作流

## 1. 架构概览

### 1.1 单向数据流

```
PRD 分片 → Task Master → tasks.json → Claude Code + Serena → SuperClaude → Git + PR
  (需求)     (分解)       (SSoT)         (实现)              (自动化)    (交付)
                            ↑                                    ↓
                            └────── 状态回写 (status/commits) ───┘
```

### 1.2 职责分离

| 工具 | 职责 | 输入 | 输出 |
|------|------|------|------|
| **Task Master** | 需求分解 + 任务状态管理 | PRD.txt | tasks.json |
| **Claude Code** | 代码实现 (TDD) | tasks.json + ADR + CH | .cs/.gd 代码 |
| **Serena** | Symbol-level 重构 | 跨文件修改需求 | 语义化编辑 |
| **SuperClaude** | Git 自动化 (commit/changelog/review) | Staged changes | Commit + PR + Review notes |
| **task_workflow.py** | 编排器 (可选) | Task ID | Branch + Status update |

### 1.3 任务生命周期

```
pending → in_progress → review → completed
   ↓           ↓          ↓
 blocked ←─────┴──────────┘
```

- **pending**: Task Master 生成后的初始状态
- **in_progress**: SuperClaude 创建 feature branch 后
- **review**: PR 创建后，等待合并
- **completed**: PR merged
- **blocked**: 发现依赖未满足或技术债

---

## 2. 前置准备

### 2.1 工具安装

```bash
# Task Master (已安装，使用 OpenAI provider)
npx task-master models  # 验证 API key 配置

# SuperClaude (需要安装)
# 根据 SuperClaude 官方文档安装

# GitHub CLI (用于创建 PR)
winget install GitHub.cli
gh auth login

# Python 依赖 (用于自动化脚本)
py -3 -m pip install jsonschema
```

### 2.2 tasks.json Schema 扩展

Task Master 默认字段：
```json
{
  "id": "1.1",
  "title": "任务标题",
  "description": "详细描述",
  "status": "pending",
  "priority": "high",
  "dependencies": ["1.0"],
  "adrRefs": ["ADR-0002"],
  "archRefs": ["CH01", "CH05"],
  "overlay": "docs/architecture/overlays/PRD-guild/08/..."
}
```

SuperClaude 回写字段（手动或脚本添加）：
```json
{
  "gitBranch": "feature/task-1.1-guild-creation",
  "commits": [
    {
      "sha": "abc123",
      "message": "feat(guild): add GuildCreationService",
      "timestamp": "2025-01-23T10:30:00Z"
    }
  ],
  "prUrl": "https://github.com/user/repo/pull/42",
  "testRefs": ["Tests/Core/Guild/GuildCreationTests.cs"],
  "blockers": ["等待 ADR-0007 批准"],
  "notes": "需要先完成数据库迁移脚本"
}
```

---

## 3. 工作流步骤

### Phase 1: 需求准备 (Task Master)

**3.1 合并 PRD 分片到单文件**

```bash
# Windows (PowerShell)
Get-Content docs\prd\prd_chunks\*.md | Out-File -Encoding utf8 .taskmaster\docs\prd.txt

# 或使用 Python
py -3 -c "import pathlib; pathlib.Path('.taskmaster/docs/prd.txt').write_text(''.join(p.read_text(encoding='utf-8') for p in sorted(pathlib.Path('docs/prd/prd_chunks').glob('*.md'))), encoding='utf-8')"
```

**3.2 生成任务 (调整 `-n` 参数控制任务数量)**

```bash
npx task-master parse-prd .taskmaster/docs/prd.txt -n 30
```

**3.3 校验 ADR/CH 回链**

```bash
py -3 scripts/python/validate_task_links.py
```

如果校验失败，手动编辑 `tasks/tasks.json` 补充 `adrRefs` 和 `archRefs`。

**3.4 生成任务文件 (可选，便于查看)**

```bash
npx task-master generate
# 产出：tasks/1.1.md, tasks/1.2.md, ...
```

---

### Phase 2: 任务实现 (Claude Code + SuperClaude)

**3.5 查看下一个待办任务**

```bash
npx task-master next
```

输出示例：
```
Next task to work on:
  ID: 1.1
  Title: 实现公会创建核心逻辑
  Priority: high
  Dependencies: [] (all satisfied)
  ADRs: ADR-0002, ADR-0006
  Chapters: CH01, CH05
```

**3.6 创建 Feature Branch**

```bash
# 命名规范：feature/task-{id}-{slug}
git checkout -b feature/task-1.1-guild-creation
```

**3.7 更新任务状态为 in_progress**

```bash
npx task-master set-status 1.1 in-progress
```

**3.8 Claude Code 实现 (TDD 循环)**

在 Claude Code 中：

```
1. 读取任务需求：@tasks/1.1.md
2. 读取架构约束：@ADR-0002, @CH01, @CH05
3. 读取现有代码：@Game.Core/Guild/
4. TDD 循环：
   - 红：写失败测试 (xUnit)
   - 绿：最小化实现
   - 重构：使用 Serena 进行 symbol-level 优化
5. 验证质量门禁：
   - dotnet test --collect:"XPlat Code Coverage"
   - 覆盖率 ≥90%（见 CLAUDE.md 6.2）
```

**3.9 SuperClaude 自动生成 Commit Message**

```bash
# 暂存更改
git add Game.Core/Guild/GuildCreationService.cs Tests/Core/Guild/GuildCreationTests.cs

# SuperClaude 自动生成 commit message（含 ADR/CH/Task refs）
superclaude commit
```

SuperClaude 自动生成的 commit message 示例：
```
feat(guild): add GuildCreationService

实现公会创建核心逻辑，包括：
- 名称唯一性校验
- 初始成员分配
- 默认权限设置

Refs: ADR-0002, ADR-0006, CH01, CH05
Task: #1.1

Co-Authored-By: Claude <noreply@anthropic.com>
```

**3.10 重复步骤 3.8-3.9 直到任务完成**

增量提交，保持每个 commit 可编译、可测试。

---

### Phase 3: 代码审查与 PR (SuperClaude)

**3.11 SuperClaude 生成 Review Notes**

```bash
superclaude review --staged
```

产出 `review-notes.md`（可选，用于自查）：
```markdown
## 代码审查摘要

### 风险评估
- 安全风险：低（已遵循 ADR-0002 路径校验规范）
- 性能风险：低（单次操作 O(1) 查询）
- 技术债：无

### 测试覆盖
- 单元测试：95% (12/13 行)
- 场景测试：待补充 GdUnit4 集成测试

### 建议
- 补充异常路径测试（名称为空、超长）
- 添加并发创建的竞态测试
```

**3.12 推送分支并创建 PR**

```bash
git push -u origin feature/task-1.1-guild-creation

gh pr create \
  --title "Task 1.1: 实现公会创建核心逻辑" \
  --body "$(cat <<'EOF'
## 任务说明
实现公会创建的核心业务逻辑。

## ADR/CH 引用
- ADR-0002: 安全基线（路径校验）
- ADR-0006: 数据存储（SQLite）
- CH01: 目标与约束
- CH05: 数据模型

## 测试引用
- [x] Tests/Core/Guild/GuildCreationTests.cs (xUnit, 95% 覆盖)
- [ ] Tests/Scenes/Guild/GuildCreationSceneTests.gd (GdUnit4, 待补充)

## 质量门禁
- [x] dotnet test 通过
- [x] 覆盖率 ≥90%
- [x] ADR 回链校验通过
- [ ] GdUnit4 集成测试（后续补充）

Refs: #1.1

🤖 Generated with SuperClaude
EOF
)"
```

**3.13 更新任务状态为 review**

```bash
npx task-master set-status 1.1 review
```

手动编辑 `tasks/tasks.json`，添加 `prUrl` 字段：
```json
{
  "id": "1.1",
  "status": "review",
  "prUrl": "https://github.com/user/repo/pull/42"
}
```

**3.14 PR 合并后，标记为 completed**

```bash
npx task-master set-status 1.1 completed
```

---

## 4. 自动化脚本 (可选)

为了减少手动操作，可以创建 `scripts/python/task_workflow.py` 编排器：

### 4.1 脚本功能

```bash
# 查看下一个任务（考虑依赖）
py -3 scripts/python/task_workflow.py next

# 开始任务（创建 branch + 更新状态）
py -3 scripts/python/task_workflow.py start 1.1

# 提交代码（调用 SuperClaude + 记录 commit SHA）
py -3 scripts/python/task_workflow.py commit 1.1

# 完成任务（创建 PR + 更新状态）
py -3 scripts/python/task_workflow.py finish 1.1

# 标记完成（PR merged 后）
py -3 scripts/python/task_workflow.py complete 1.1

# 标记阻塞
py -3 scripts/python/task_workflow.py block 1.1 "等待 ADR-0007 批准"
```

### 4.2 脚本实现要点

**依赖检查**：
```python
def can_start(task_id: str) -> bool:
    task = get_task(task_id)
    for dep_id in task.get("dependencies", []):
        dep_task = get_task(dep_id)
        if dep_task["status"] != "completed":
            return False
    return True
```

**Branch 命名**：
```python
def create_branch(task_id: str):
    task = get_task(task_id)
    slug = task["title"].lower().replace(" ", "-")[:30]
    branch = f"feature/task-{task_id}-{slug}"
    subprocess.run(["git", "checkout", "-b", branch], check=True)
    return branch
```

**Commit 记录**：
```python
def record_commit(task_id: str):
    # 获取最新 commit SHA
    sha = subprocess.check_output(["git", "rev-parse", "HEAD"]).decode().strip()
    message = subprocess.check_output(["git", "log", "-1", "--format=%B"]).decode().strip()

    # 写入 tasks.json
    task = get_task(task_id)
    if "commits" not in task:
        task["commits"] = []
    task["commits"].append({
        "sha": sha,
        "message": message,
        "timestamp": datetime.now().isoformat()
    })
    save_tasks()
```

**PR 创建**：
```python
def create_pr(task_id: str):
    task = get_task(task_id)
    title = f"Task {task_id}: {task['title']}"

    # 生成 PR body
    body = f"""
## 任务说明
{task['description']}

## ADR/CH 引用
{', '.join(task['adrRefs'])} | {', '.join(task['archRefs'])}

## 测试引用
{chr(10).join(f"- [ ] {ref}" for ref in task.get('testRefs', []))}

Refs: #{task_id}
    """.strip()

    # 调用 gh CLI
    result = subprocess.run(
        ["gh", "pr", "create", "--title", title, "--body", body],
        capture_output=True, text=True, check=True
    )

    # 解析 PR URL
    pr_url = result.stdout.strip().split("\n")[-1]
    task["prUrl"] = pr_url
    save_tasks()
```

---

## 5. 常见问题

### Q1: 如何处理任务依赖？

**场景**：任务 1.2 依赖任务 1.1 完成。

**解决方案**：
1. Task Master 自动在 `dependencies` 字段记录依赖关系
2. 使用 `npx task-master next` 时自动跳过依赖未满足的任务
3. 脚本 `task_workflow.py next` 会自动检查依赖状态

**手动处理**：
```bash
# 查看任务 1.2 的依赖
cat tasks/tasks.json | jq '.tasks[] | select(.id=="1.2") | .dependencies'
# 输出: ["1.1"]

# 查看任务 1.1 的状态
npx task-master get-task 1.1
# 如果 status != "completed"，则不能开始 1.2
```

### Q2: 如何处理 blocked 任务？

**场景**：任务 2.3 需要等待 ADR-0010 批准。

**解决方案**：
```bash
# 标记为 blocked
npx task-master set-status 2.3 blocked

# 手动编辑 tasks.json 添加原因
{
  "id": "2.3",
  "status": "blocked",
  "blockers": ["等待 ADR-0010 (国际化策略) 批准"],
  "notes": "需要确认多语言资源文件格式"
}
```

**解除阻塞**：
```bash
# ADR-0010 批准后
npx task-master set-status 2.3 pending

# 删除 blockers 字段
# (手动编辑 tasks.json 或使用 jq)
```

### Q3: PR 模板如何生成？

**方案 1**：使用 `gh pr create --body "..."`（见 3.12）

**方案 2**：使用 `.github/PULL_REQUEST_TEMPLATE.md`

创建模板文件：
```markdown
## 任务说明
<!-- 从 tasks.json 复制 description -->

## ADR/CH 引用
<!-- 自动填充：ADR-0002, CH01 -->

## 测试引用
- [ ] Tests/Core/...
- [ ] Tests/Scenes/...

## 质量门禁
- [ ] dotnet test 通过
- [ ] 覆盖率 ≥90%
- [ ] ADR 回链校验通过
- [ ] GdUnit4 集成测试通过

Refs: #<TASK_ID>
```

**自动化填充**：
```python
def fill_pr_template(task_id: str) -> str:
    task = get_task(task_id)
    template = Path(".github/PULL_REQUEST_TEMPLATE.md").read_text()

    # 替换占位符
    body = template.replace("<TASK_ID>", task_id)
    body = body.replace("<!-- 从 tasks.json 复制 description -->", task["description"])
    body = body.replace("<!-- 自动填充：ADR-0002, CH01 -->",
                       f"{', '.join(task['adrRefs'])} | {', '.join(task['archRefs'])}")
    return body
```

### Q4: 如何批量执行任务？

**场景**：有 5 个独立任务（无依赖关系），想并行处理。

**方案**：使用 Git worktree + 多个 Claude Code 会话

```bash
# 主分支保持在 main
git worktree add ../newguild-task-1.1 -b feature/task-1.1
git worktree add ../newguild-task-1.2 -b feature/task-1.2

# 在不同终端/IDE 实例中分别处理
# Terminal 1: cd ../newguild-task-1.1 && code .
# Terminal 2: cd ../newguild-task-1.2 && code .
```

**注意**：SQLite 数据库文件冲突，建议测试时使用内存数据库。

---

## 6. 进阶技巧

### 6.1 自动化测试集成

在 `superclaude commit` 之前，自动运行测试：

```bash
# .git/hooks/pre-commit (需要 chmod +x)
#!/usr/bin/env python3
import subprocess
import sys

def run_tests():
    # 运行单元测试
    result = subprocess.run(["dotnet", "test"], capture_output=True)
    if result.returncode != 0:
        print("❌ 单元测试失败，拒绝提交")
        print(result.stderr.decode())
        return False

    # 运行覆盖率门禁
    result = subprocess.run([
        "dotnet", "test", "--collect:XPlat Code Coverage"
    ], capture_output=True)
    # 解析 coverage.json，检查是否 ≥90%
    # ...

    return True

if __name__ == "__main__":
    if not run_tests():
        sys.exit(1)
```

### 6.2 Release 管理

使用 SuperClaude 自动生成 CHANGELOG：

```bash
# 生成 v0.2.0 的 changelog
superclaude changelog --from v0.1.0 --to HEAD

# 输出到 CHANGELOG.md
superclaude changelog --from v0.1.0 --to HEAD >> CHANGELOG.md
```

自动聚合 tasks.json 中的 completed 任务：

```python
def generate_release_notes(version: str) -> str:
    tasks = get_all_tasks()
    completed = [t for t in tasks if t["status"] == "completed"]

    notes = f"# Release {version}\n\n"
    for task in completed:
        notes += f"- **{task['id']}**: {task['title']}\n"
        if "prUrl" in task:
            notes += f"  - PR: {task['prUrl']}\n"
        notes += f"  - ADRs: {', '.join(task['adrRefs'])}\n\n"

    return notes
```

### 6.3 任务复盘与度量

从 tasks.json 提取度量数据：

```python
def analyze_velocity():
    tasks = get_all_tasks()
    completed = [t for t in tasks if t["status"] == "completed"]

    # 计算完成率
    completion_rate = len(completed) / len(tasks) * 100

    # 计算平均 commit 数
    avg_commits = sum(len(t.get("commits", [])) for t in completed) / len(completed)

    # 按优先级分组
    by_priority = {}
    for task in tasks:
        priority = task.get("priority", "medium")
        if priority not in by_priority:
            by_priority[priority] = {"total": 0, "completed": 0}
        by_priority[priority]["total"] += 1
        if task["status"] == "completed":
            by_priority[priority]["completed"] += 1

    print(f"完成率: {completion_rate:.1f}%")
    print(f"平均每任务 commit 数: {avg_commits:.1f}")
    print("\n按优先级统计:")
    for p, stats in by_priority.items():
        rate = stats["completed"] / stats["total"] * 100
        print(f"  {p}: {stats['completed']}/{stats['total']} ({rate:.1f}%)")
```

---

## 7. 完整示例：从 PRD 到交付

```bash
# ========== Phase 1: 任务准备 ==========
# 1. 合并 PRD
Get-Content docs\prd\prd_chunks\*.md | Out-File -Encoding utf8 .taskmaster\docs\prd.txt

# 2. 生成任务
npx task-master parse-prd .taskmaster\docs\prd.txt -n 30

# 3. 校验
py -3 scripts\python\validate_task_links.py

# 4. 生成任务文件
npx task-master generate


# ========== Phase 2: 任务实现 ==========
# 5. 查看下一个任务
npx task-master next
# 输出: Task 1.1: 实现公会创建核心逻辑

# 6. 开始任务
git checkout -b feature/task-1.1-guild-creation
npx task-master set-status 1.1 in-progress

# 7. Claude Code 实现 (TDD)
# - 读取 @tasks/1.1.md
# - 引用 @ADR-0002, @CH01
# - 编写测试 → 实现 → 重构

# 8. 提交代码
git add .
superclaude commit
# SuperClaude 自动生成：
# feat(guild): add GuildCreationService
# Refs: ADR-0002, ADR-0006, CH01, CH05
# Task: #1.1

# 9. 重复 7-8 直到完成


# ========== Phase 3: PR 与合并 ==========
# 10. 生成 review notes
superclaude review --staged

# 11. 创建 PR
git push -u origin feature/task-1.1-guild-creation
gh pr create --title "Task 1.1: 实现公会创建核心逻辑" --body "..."

# 12. 更新状态
npx task-master set-status 1.1 review

# 13. PR 合并后
npx task-master set-status 1.1 completed
```

---

## 8. 检查清单

### 任务开始前
- [ ] `git status` 确认工作区干净
- [ ] `git branch` 确认在 main 分支
- [ ] `npx task-master next` 确认任务依赖满足
- [ ] 任务的 ADR/CH 引用已阅读

### 实现过程中
- [ ] 遵循 TDD 循环（红→绿→重构）
- [ ] 每个 commit 可编译、可测试
- [ ] Commit message 包含 ADR/CH/Task refs
- [ ] 覆盖率 ≥90%（`dotnet test --collect:"XPlat Code Coverage"`）

### PR 创建前
- [ ] `dotnet test` 全部通过
- [ ] `py -3 scripts/python/validate_task_links.py` 通过
- [ ] `superclaude review --staged` 无高风险问题
- [ ] PR body 包含 ADR/CH refs 和 Test-Refs

### PR 合并后
- [ ] `npx task-master set-status <id> completed`
- [ ] 删除本地 feature branch
- [ ] 更新 tasks.json 的 `prUrl` 字段

---

## 9. 参考资料

- Task Master 文档：[README.md](../../.taskmaster/README.md)
- SuperClaude 使用指南：(根据实际安装路径补充)
- ADR 目录：[docs/adr/](../adr/)
- 测试框架指南：[docs/testing-framework.md](../testing-framework.md)
- 项目 Rulebook: [CLAUDE.md](../../CLAUDE.md)
