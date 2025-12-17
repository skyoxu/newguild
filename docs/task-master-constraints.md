# Task Master AI 硬性限制与约束

> **文档目的**：为 AI 代理提供 Task Master 的技术约束清单，用于验证和转换自定义任务格式

**版本**：Task Master AI v0.36.0
**仓库**：https://github.com/eyaltoledano/claude-task-master
**最后更新**：2025-11-30

---

## 1. 文件路径约束

### 1.1 硬编码路径

**Task Master 只能操作以下固定路径**：

```
.taskmaster/
├── tasks/
│   └── tasks.json          # [PASS] 唯一可识别的任务文件
├── docs/
│   └── prd.txt/prd.md      # [PASS] PRD 解析输入文件
├── config.json             # [PASS] 模型配置文件
└── reports/                # [PASS] 复杂度报告输出目录
```

### 1.2 不支持的功能

[FAIL] **无法指定自定义 JSON 文件路径**
- 命令如 `task-master list --file custom.json` **不存在**
- 唯一可用参数：`-p, --project <path>` （仅指定项目根目录）

[FAIL] **无法使用多个任务文件**
- 不支持类似 Git 的多分支任务管理（除了 Tag 系统）
- `tasks_back.json`、`tasks_gameplay.json` 等文件会被忽略

### 1.3 Tag 系统（v0.17+ 功能）

[PASS] **支持通过 Tag 隔离任务上下文**

```bash
# 创建新 Tag
task-master add-tag --name feature-auth

# 切换 Tag
task-master use-tag --name feature-auth

# 列出所有 Tag
task-master list-tags
```

**Tag 数据存储**：
- 仍在 `.taskmaster/tasks/tasks.json` 内
- 结构：`{"tag1": {"tasks": [...]}, "tag2": {"tasks": [...]}}`
- 默认 Tag：`"master"`

---

## 2. 任务 Schema 约束

### 2.1 根对象结构

**必须使用嵌套对象，不能是数组**

[PASS] **正确格式**：
```json
{
  "master": {
    "tasks": [
      { "id": 1, "title": "..." }
    ]
  }
}
```

[FAIL] **错误格式**：
```json
[
  { "id": "NG-0001", "title": "..." }
]
```

### 2.2 必需字段

| 字段 | 类型 | 说明 | 示例 |
|------|------|------|------|
| `id` | `number` | **必须是纯数字** | `1`, `2`, `3` |
| `title` | `string` | 任务标题 | `"Setup Project"` |
| `description` | `string` | 简短描述 | `"Initialize Godot environment"` |
| `status` | `enum` | 状态枚举 | `"pending"` / `"in-progress"` / `"done"` / `"deferred"` / `"cancelled"` / `"blocked"` |

### 2.3 可选标准字段

| 字段 | 类型 | 格式要求 | 示例 |
|------|------|----------|------|
| `details` | `string` | 详细说明（Markdown） | `"## Steps\n1. Install..."` |
| `testStrategy` | `string` | 测试策略（**必须是字符串**） | `"xUnit + GdUnit4"` |
| `priority` | `enum` | 优先级枚举 | `"high"` / `"medium"` / `"low"` |
| `dependencies` | `number[]` | **数字数组** | `[1, 2]` |
| `subtasks` | `Task[]` | 子任务数组（递归结构） | `[{...}]` |

### 2.4 ID 格式硬性规则

**仅支持数字 ID 系统**

[PASS] **允许的格式**：
- 主任务：`1`, `2`, `3`, ...
- 子任务：`1.1`, `1.2`, `2.1`, ...（CLI 显示格式，内部仍是 `id: 1` + 嵌套）
- 三级任务：`1.1.1`, `1.1.2`, ...

[FAIL] **不支持的格式**：
- 字符串 ID：`"NG-0001"`, `"T-001"`, `"TASK-123"`
- UUID：`"550e8400-e29b-41d4-a716-446655440000"`
- 混合格式：`"1A"`, `"v2.1"`

**技术原因**：
- TypeScript 类型定义：`id: number`
- 依赖项必须是数字数组才能匹配
- CLI 命令解析：`parseInt(args.id)`

---

## 3. 不支持的自定义字段

### 3.1 官方不支持扩展

**Task Master 使用固定 Schema，无插件/扩展 API**

[FAIL] **以下字段会被静默忽略**：
- `adr_refs`
- `chapter_refs`
- `overlay_refs`
- `labels`
- `owner`
- `test_refs`
- `acceptance`
- `story_id`
- `layer`
- `metadata`（即使官方文档提及，仍有解析 Bug - Issue #786）

### 3.2 已知 Bug

**Issue #786** - 元数据字段解析失败
https://github.com/eyaltoledano/claude-task-master/issues/786

即使添加 `metadata` 字段（官方文档暗示支持），解析器仍会报错或忽略。

### 3.3 字段名冲突

[FAIL] **不要使用不同字段名**：
- `depends_on` → 必须改为 `dependencies`
- `test_strategy`（数组） → 必须改为 `testStrategy`（字符串）

---

## 4. 优先级枚举值

### 4.1 标准枚举

**仅支持三种英文小写值**：

```typescript
type Priority = "high" | "medium" | "low";
```

[FAIL] **不支持的格式**：
- 数字优先级：`1`, `2`, `3`
- P 系统：`"P0"`, `"P1"`, `"P2"`
- 自定义级别：`"critical"`, `"urgent"`, `"nice-to-have"`
- 大写变体：`"HIGH"`, `"Medium"`

---

## 5. 状态枚举值

### 5.1 标准枚举

**仅支持以下六种状态**：

```typescript
type Status =
  | "pending"       // 待处理
  | "in-progress"   // 进行中
  | "done"          // 已完成
  | "deferred"      // 已延期
  | "cancelled"     // 已取消
  | "blocked";      // 被阻塞
```

[FAIL] **不支持自定义状态**：
- `"review"`, `"testing"`, `"deployed"`
- 中文状态：`"待办"`, `"进行中"`

---

## 6. 命令行限制

### 6.1 任务选择

[FAIL] **无法直接指定任务 ID 执行**

```bash
# [FAIL] 不存在的命令
task-master next --id=5
task-master run 5

# [PASS] 实际工作流
task-master next              # 自动选择下一个可用任务
task-master show 5            # 查看特定任务（不执行）
task-master set-status --id=5 --status=in-progress
```

### 6.2 文件路径参数

**唯一的路径参数**：

```bash
task-master [command] -p /path/to/project
# 或
task-master [command] --project /path/to/project
```

**作用**：指定项目根目录（自动查找 `.taskmaster/tasks/tasks.json`）

[FAIL] **不支持**：
```bash
task-master list --file custom.json        # [FAIL] 无此参数
task-master parse-prd --output custom.json  # [FAIL] 输出路径固定
```

---

## 7. PRD 解析约束

### 7.1 输入文件路径

**固定输入路径**（CLI 参数可变，但通常使用）：

```bash
task-master parse-prd .taskmaster/docs/prd.txt
# 或
task-master parse-prd .taskmaster/docs/prd.md
```

### 7.2 输出路径

**固定输出路径**（不可自定义）：

- 任务数据 → `.taskmaster/tasks/tasks.json`
- 复杂度报告 → `.taskmaster/reports/task-complexity-report.json`

### 7.3 Front Matter 限制

[FAIL] **不支持自定义 Front Matter**

PRD 文件的 YAML Front Matter 会被解析，但**不会保留到任务数据**：

```markdown
---
story_id: PRD-001
adr_refs: [ADR-0001, ADR-0002]
---
# 任务标题
```

上述元数据会丢失，仅 `title` 和正文会被解析。

---

## 8. API/MCP 集成约束

### 8.1 MCP Server 配置

**.mcp.json 最小配置**：

```json
{
  "mcpServers": {
    "task-master-ai": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "task-master-ai"]
    }
  }
}
```

**注意**：
- `env` 块可省略（继承系统环境变量）
- 必需环境变量：至少一个 AI 模型 API Key（`ANTHROPIC_API_KEY` / `OPENAI_API_KEY` / `PERPLEXITY_API_KEY` 等）

### 8.2 MCP 工具限制

**MCP 工具与 CLI 命令一一对应**：

| MCP 工具 | CLI 命令 | 限制 |
|----------|----------|------|
| `get_tasks` | `task-master list` | 无过滤参数（除 status/tag） |
| `next_task` | `task-master next` | 无法指定 ID |
| `get_task` | `task-master show <id>` | 仅数字 ID |
| `parse_prd` | `task-master parse-prd` | 输出路径固定 |

---

## 9. 兼容性转换规则

### 9.1 从 tasks_back.json 转换到标准格式

**转换映射表**：

| tasks_back.json | 标准 tasks.json | 转换规则 |
|-----------------|----------------|----------|
| `"id": "NG-0001"` | `"id": 1` | 提取数字后缀或重新编号 |
| `"priority": "P1"` | `"priority": "high"` | `P0/P1 → high, P2 → medium, P3+ → low` |
| `"depends_on": [...]` | `"dependencies": [...]` | 字段改名 + 转换 ID 为数字 |
| `"test_strategy": [...]` | `"testStrategy": "..."` | 数组合并为字符串（换行分隔） |
| `"story_id"` | [FAIL] 丢弃 | 无对应字段 |
| `"adr_refs"` | [FAIL] 丢弃 | 可写入 `details` 或 `description` |
| `"chapter_refs"` | [FAIL] 丢弃 | 同上 |
| `"labels"` | [FAIL] 丢弃 | 可用 Tag 系统部分替代 |

### 9.2 根结构转换

**从数组转换为嵌套对象**：

```python
# 伪代码
if isinstance(data, list):
    data = {
        "master": {
            "tasks": data
        }
    }
```

### 9.3 自定义字段保留策略

**方案 A - 写入 details 字段（Markdown）**：

```json
{
  "id": 1,
  "title": "任务标题",
  "details": "## 元数据\n- Story ID: PRD-001\n- ADRs: ADR-0001, ADR-0002\n- Owner: architecture\n\n## 实现细节\n..."
}
```

**方案 B - 双文件系统**：
- `tasks.json` → Task Master 操作
- `tasks_back.json` → 元数据仓库（手动维护或脚本同步）

---

## 10. 已知 Bug 与限制

### 10.1 FastMCP 警告

**症状**：
```
[FastMCP warning] could not infer client capabilities after 10 attempts
```

**影响**：MCP 服务器功能正常，但缺少采样能力推断

**解决方案**：可忽略，或重启 Claude Code

### 10.2 元数据解析失败

**Issue #786**：即使按文档添加 `metadata` 字段，仍无法正确解析

**规避方案**：避免使用 `metadata`，所有自定义数据写入 `details` 字段

### 10.3 依赖项循环检测

**症状**：循环依赖会导致 `task-master next` 永久阻塞

**预防**：使用 `task-master validate-dependencies` 定期检查

---

## 11. 推荐工作流

### 11.1 双文件同步策略

```
tasks_back.json  (源头真相)
       ↓
   转换脚本
       ↓
  tasks.json     (Task Master 操作)
```

**脚本职责**：
1. 读取 `tasks_back.json`
2. 转换 ID/优先级/字段名
3. 提取自定义字段到 `details`
4. 写入 `tasks.json`（保持 `{"master": {"tasks": []}}` 结构）

### 11.2 Git 集成建议

**.gitignore 配置**：
```gitignore
.taskmaster/tasks/tasks.json   # [PASS] 追踪（标准格式）
.taskmaster/tasks/tasks_*.json # [FAIL] 忽略（自定义格式）
```

**分支策略**：
- 每个功能分支使用独立 Tag
- 主分支使用 `master` Tag

---

## 12. 验证清单

**在转换 tasks_back.json 前，检查以下项**：

- [ ] 根结构是 `{"master": {"tasks": [...]}}`，不是数组
- [ ] 所有 `id` 字段是数字类型（`1` 而非 `"1"` 或 `"NG-0001"`）
- [ ] `priority` 只包含 `"high"` / `"medium"` / `"low"`
- [ ] `status` 只包含 6 种标准状态
- [ ] `dependencies` 字段名正确且值为数字数组
- [ ] `testStrategy` 是字符串，不是数组
- [ ] 移除所有自定义字段（或迁移到 `details`）
- [ ] 运行 `task-master validate-dependencies` 无错误
- [ ] 运行 `task-master list` 能正常显示任务

---

## 附录 A：完整 Schema 示例

```json
{
  "master": {
    "tasks": [
      {
        "id": 1,
        "title": "Setup Godot Project",
        "description": "Initialize Godot 4.5 environment with C# support",
        "details": "## Metadata\n- Story ID: PRD-NEWGUILD-001\n- ADRs: ADR-0001, ADR-0011\n- Owner: architecture\n\n## Steps\n1. Install Godot 4.5\n2. Configure .NET 8 SDK\n3. Initialize project structure",
        "testStrategy": "Manual verification: Project opens in editor, C# compilation works",
        "priority": "high",
        "dependencies": [],
        "status": "done",
        "subtasks": [
          {
            "id": 1,
            "title": "Install Godot 4.5",
            "description": "Download and install Godot 4.5.1 .NET version",
            "status": "done",
            "priority": "high",
            "dependencies": [],
            "subtasks": []
          }
        ]
      }
    ]
  }
}
```

---

## 附录 B：转换脚本模板（Python）

```python
import json
from pathlib import Path

def convert_tasks_back_to_standard(input_file: str, output_file: str):
    """转换 tasks_back.json 到 Task Master 标准格式"""

    # 读取源文件
    with open(input_file, 'r', encoding='utf-8') as f:
        tasks_back = json.load(f)

    # 转换每个任务
    converted_tasks = []
    for i, task in enumerate(tasks_back, start=1):
        converted = {
            "id": i,  # 重新编号为纯数字
            "title": task["title"],
            "description": task["description"],
            "status": task["status"],

            # 优先级映射
            "priority": {
                "P0": "high", "P1": "high",
                "P2": "medium",
                "P3": "low"
            }.get(task.get("priority", "P2"), "medium"),

            # 依赖项转换（假设 depends_on 包含字符串 ID）
            "dependencies": [],  # 需要手动映射字符串 ID → 数字 ID

            # 合并自定义字段到 details
            "details": f"""## 元数据
- Story ID: {task.get('story_id', 'N/A')}
- ADR Refs: {', '.join(task.get('adr_refs', []))}
- Chapter Refs: {', '.join(task.get('chapter_refs', []))}
- Owner: {task.get('owner', 'N/A')}
- Labels: {', '.join(task.get('labels', []))}

## 测试策略
{chr(10).join(task.get('test_strategy', []))}

## 验收标准
{chr(10).join(task.get('acceptance', []))}
""",

            # 测试策略（数组转字符串）
            "testStrategy": "\n".join(task.get("test_strategy", [])),

            "subtasks": []  # 递归处理子任务
        }
        converted_tasks.append(converted)

    # 包装为标准格式
    standard_format = {
        "master": {
            "tasks": converted_tasks
        }
    }

    # 写入输出文件
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(standard_format, f, ensure_ascii=False, indent=2)

    print(f"[PASS] 转换完成：{len(converted_tasks)} 个任务")
    print(f" 输出文件：{output_file}")

# 使用示例
convert_tasks_back_to_standard(
    ".taskmaster/tasks/tasks_back.json",
    ".taskmaster/tasks/tasks.json"
)
```

---

**文档维护者**：Claude Code AI
**参考资源**：
- https://github.com/eyaltoledano/claude-task-master
- https://docs.task-master.dev
- Issue #786: https://github.com/eyaltoledano/claude-task-master/issues/786
