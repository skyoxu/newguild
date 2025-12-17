# SuperClaude 任务映射配置

> **目的**：让 SuperClaude 在通过 `/sc` 处理任务时能够读取 `tasks.json` 和原始任务文件的完整信息

**版本**：v1.0
**创建日期**：2025-11-30
**依赖**：`scripts/python/build_taskmaster_tasks.py`

---

## 1. 背景与动机

### 1.1 双文件系统设计

本项目使用 **双文件映射** 来兼容 Task Master MCP 的约束：

```
原始任务文件（完整元数据）                Task Master 标准文件
┌─────────────────────────────┐          ┌──────────────────────────┐
│ tasks_back.json             │          │ tasks.json               │
│ tasks_gameplay.json         │  ─────>  │                          │
│ tasks_longterm.json         │ 转换映射  │ {"master": {"tasks": []}}│
│                             │          │                          │
│ - 字符串 ID: "NG-0001"      │          │ - 数字 ID: 1             │
│ - 自定义字段: adr_refs[]    │          │ - 标准字段: dependencies │
│ - 完整元数据                │          │ - 简化 details           │
│                             │          │                          │
│ + taskmaster_id: 1          │  <─关联  │                          │
│ + taskmaster_exported: true │          │                          │
└─────────────────────────────┘          └──────────────────────────┘
```

### 1.2 映射机制

**通过 `scripts/python/build_taskmaster_tasks.py` 实现**：

1. **从原始文件读取任务**（指定 `--tasks-file`）
2. **计算依赖闭包**（基于 `depends_on` 字段和 `--ids` 参数）
3. **转换为 Task Master 格式**：
   - 分配/复用数字 ID
   - 映射优先级（`P1 → high`）
   - 转换依赖关系（字符串 ID → 数字 ID）
   - 聚合自定义字段到 `details`
4. **写入 tasks.json** 的指定 Tag
5. **回写标记字段到原始文件**：
   - `taskmaster_id`：映射的数字 ID
   - `taskmaster_exported: true`：已导出标记

### 1.3 当前问题

**SuperClaude 的 `/sc` 命令无法访问原始任务的完整元数据**：

- [PASS] 能读取 `tasks.json` 中的基础信息（title、description、dependencies）
- [FAIL] 无法读取 `adr_refs`、`chapter_refs`、`test_refs`、`acceptance` 等关键字段
- [FAIL] 缺失这些字段会导致：
  - Commit 消息无法引用正确的 ADR
  - 代码审查无法检查验收标准
  - 测试策略信息不完整

---

## 2. 解决方案架构

### 2.1 增强型任务加载器

创建一个 **任务上下文增强器**（Python 脚本 + SuperClaude 配置）：

```
SuperClaude /sc 命令
       ↓
任务上下文增强器 (load_enhanced_task.py)
       ↓
┌─────────────────────────────────────────┐
│ 1. 读取 tasks.json                      │ → 获取数字 ID、基础信息
│ 2. 查找原始文件中 taskmaster_id 匹配项 │ → 获取完整元数据
│ 3. 合并数据                             │ → 构建增强任务对象
│ 4. 返回给 SuperClaude                   │ → 用于 commit/review/分析
└─────────────────────────────────────────┘
```

### 2.2 数据流

```mermaid
graph LR
    A[/sc 命令] --> B[解析任务 ID]
    B --> C[读取 tasks.json]
    C --> D[获取 taskmaster_id: N]
    D --> E[搜索原始文件]
    E --> F{找到匹配任务?}
    F -->|是| G[合并元数据]
    F -->|否| H[仅使用 tasks.json 数据]
    G --> I[生成增强上下文]
    H --> I
    I --> J[SuperClaude 处理]
```

---

## 3. 实现方案

### 3.1 创建任务加载脚本

**文件路径**：`scripts/python/load_enhanced_task.py`

**功能**：
- 输入：Task Master 数字 ID
- 输出：JSON 格式的增强任务对象

**关键逻辑**：

```python
import json
from pathlib import Path
from typing import Optional, Dict, Any, List

def load_enhanced_task(
    task_id: int,
    tasks_json_path: str = ".taskmaster/tasks/tasks.json",
    original_files: List[str] = None,
    tag: str = "master"
) -> Dict[str, Any]:
    """
    加载增强任务信息（合并 tasks.json 和原始任务文件）

    Args:
        task_id: Task Master 数字 ID
        tasks_json_path: tasks.json 文件路径
        original_files: 原始任务文件路径列表
        tag: Task Master Tag 名称

    Returns:
        增强任务对象，包含所有元数据
    """

    # 默认原始文件列表
    if original_files is None:
        original_files = [
            ".taskmaster/tasks/tasks_back.json",
            ".taskmaster/tasks/tasks_gameplay.json",
            ".taskmaster/tasks/tasks_longterm.json"
        ]

    # 1. 读取 tasks.json 中的标准任务
    with open(tasks_json_path, 'r', encoding='utf-8') as f:
        tasks_data = json.load(f)

    standard_task = None
    if tag in tasks_data and "tasks" in tasks_data[tag]:
        for task in tasks_data[tag]["tasks"]:
            if task["id"] == task_id:
                standard_task = task
                break

    if not standard_task:
        raise ValueError(f"任务 ID {task_id} 在 Tag '{tag}' 中未找到")

    # 2. 在原始文件中查找匹配的任务
    original_task = None
    original_file_path = None

    for file_path in original_files:
        if not Path(file_path).exists():
            continue

        with open(file_path, 'r', encoding='utf-8') as f:
            tasks = json.load(f)

        for task in tasks:
            if task.get("taskmaster_id") == task_id:
                original_task = task
                original_file_path = file_path
                break

        if original_task:
            break

    # 3. 合并数据
    enhanced_task = {
        # 从 tasks.json 的标准字段
        "taskmaster_id": task_id,
        "title": standard_task["title"],
        "description": standard_task["description"],
        "status": standard_task["status"],
        "priority": standard_task["priority"],
        "dependencies": standard_task["dependencies"],
        "testStrategy": standard_task.get("testStrategy", ""),
        "details": standard_task.get("details", ""),

        # 元数据来源标记
        "_source": {
            "tasks_json": tasks_json_path,
            "original_file": original_file_path,
            "tag": tag
        }
    }

    # 4. 如果找到原始任务，添加完整元数据
    if original_task:
        enhanced_task.update({
            "original_id": original_task["id"],
            "story_id": original_task.get("story_id"),
            "adr_refs": original_task.get("adr_refs", []),
            "chapter_refs": original_task.get("chapter_refs", []),
            "overlay_refs": original_task.get("overlay_refs", []),
            "test_refs": original_task.get("test_refs", []),
            "acceptance": original_task.get("acceptance", []),
            "labels": original_task.get("labels", []),
            "owner": original_task.get("owner"),
            "layer": original_task.get("layer"),
            "test_strategy": original_task.get("test_strategy", []),
            "depends_on": original_task.get("depends_on", [])
        })

    return enhanced_task


def format_task_for_superclaude(task: Dict[str, Any]) -> str:
    """
    格式化增强任务为 SuperClaude 友好的 Markdown

    Args:
        task: 增强任务对象

    Returns:
        Markdown 格式的任务描述
    """

    md = f"""# 任务 #{task['taskmaster_id']}: {task['title']}

## 基础信息

- **原始 ID**: {task.get('original_id', 'N/A')}
- **Story ID**: {task.get('story_id', 'N/A')}
- **状态**: {task['status']}
- **优先级**: {task['priority']}
- **负责人**: {task.get('owner', 'N/A')}
- **层级**: {task.get('layer', 'N/A')}

## 任务描述

{task['description']}

## 详细说明

{task['details']}

## 架构参考

"""

    # ADR 引用
    if task.get('adr_refs'):
        md += "### ADR 引用\n\n"
        for adr in task['adr_refs']:
            md += f"- `{adr}`\n"
        md += "\n"

    # 章节引用
    if task.get('chapter_refs'):
        md += "### 章节引用\n\n"
        for ch in task['chapter_refs']:
            md += f"- `{ch}`\n"
        md += "\n"

    # Overlay 引用
    if task.get('overlay_refs'):
        md += "### Overlay 文档\n\n"
        for overlay in task['overlay_refs']:
            md += f"- `{overlay}`\n"
        md += "\n"

    # 测试策略
    if task.get('test_strategy'):
        md += "## 测试策略\n\n"
        for strategy in task['test_strategy']:
            md += f"- {strategy}\n"
        md += "\n"

    # 测试引用
    if task.get('test_refs'):
        md += "### 测试文件引用\n\n"
        for ref in task['test_refs']:
            md += f"- `{ref}`\n"
        md += "\n"

    # 验收标准
    if task.get('acceptance'):
        md += "## 验收标准\n\n"
        for criterion in task['acceptance']:
            md += f"- [ ] {criterion}\n"
        md += "\n"

    # 依赖关系
    if task.get('depends_on'):
        md += "## 依赖任务\n\n"
        for dep in task['depends_on']:
            md += f"- `{dep}`\n"
        md += "\n"

    # 标签
    if task.get('labels'):
        md += f"**标签**: {', '.join(task['labels'])}\n\n"

    return md


if __name__ == "__main__":
    import sys

    if len(sys.argv) < 2:
        print("用法: py -3 load_enhanced_task.py <task_id> [--json]")
        sys.exit(1)

    task_id = int(sys.argv[1])
    output_json = "--json" in sys.argv

    try:
        task = load_enhanced_task(task_id)

        if output_json:
            print(json.dumps(task, ensure_ascii=False, indent=2))
        else:
            print(format_task_for_superclaude(task))

    except Exception as e:
        print(f"错误: {e}", file=sys.stderr)
        sys.exit(1)
```

### 3.2 配置 SuperClaude 任务上下文

**方案 A：通过 CLAUDE.md 添加任务增强指令**

在 `CLAUDE.md` 或 `.claude/CLAUDE.md` 中添加：

```markdown
## SuperClaude 任务处理增强

**重要**：在使用 `/sc` 命令处理任务时，必须加载完整任务上下文。

### 任务信息获取流程

当处理任务 ID `N` 时，按以下顺序执行：

1. **加载增强任务上下文**：
   ```bash
   py -3 scripts/python/load_enhanced_task.py N
   ```

2. **读取输出的 Markdown 文档**（包含所有元数据）

3. **使用增强上下文进行操作**：
   - Commit 消息必须引用 `adr_refs` 中的 ADR
   - 代码审查必须检查 `acceptance` 中的验收标准
   - 测试必须覆盖 `test_refs` 中的文件

### 示例工作流

```bash
# 1. 获取任务 5 的完整上下文
py -3 scripts/python/load_enhanced_task.py 5

# 2. 查看输出（包含 ADR-0001, ADR-0002 引用）
# 3. 使用 SuperClaude commit 时自动引用这些 ADR

# 或获取 JSON 格式（用于脚本集成）
py -3 scripts/python/load_enhanced_task.py 5 --json
```

### 配置文件路径

**默认原始任务文件**（可通过环境变量覆盖）：
- `.taskmaster/tasks/tasks_back.json`
- `.taskmaster/tasks/tasks_gameplay.json`
- `.taskmaster/tasks/tasks_longterm.json`

**环境变量**：
- `TASK_ORIGINAL_FILES`：逗号分隔的原始任务文件路径
  ```bash
  export TASK_ORIGINAL_FILES=".taskmaster/tasks/tasks_back.json,.taskmaster/tasks/custom.json"
  ```
```

---

## 4. 使用指南

### 4.1 基础用法

**场景 1：查看任务完整信息**

```bash
# Markdown 格式（人类可读）
py -3 scripts/python/load_enhanced_task.py 5

# JSON 格式（脚本解析）
py -3 scripts/python/load_enhanced_task.py 5 --json
```

**场景 2：SuperClaude Commit 集成**

```bash
# 在 commit 前加载任务上下文
TASK_CONTEXT=$(py -3 scripts/python/load_enhanced_task.py 5)

# SuperClaude 会自动从 TASK_CONTEXT 提取 ADR 引用
superclaude commit --task 5 --context "$TASK_CONTEXT"
```

### 4.2 自定义原始文件路径

**临时指定**：

```bash
py -3 scripts/python/load_enhanced_task.py 5 \
  --original-files .taskmaster/tasks/tasks_back.json \
  --original-files .taskmaster/tasks/custom.json
```

**全局配置**（环境变量）：

```bash
# Windows PowerShell
$env:TASK_ORIGINAL_FILES=".taskmaster/tasks/tasks_back.json,.taskmaster/tasks/tasks_gameplay.json"

# Bash
export TASK_ORIGINAL_FILES=".taskmaster/tasks/tasks_back.json,.taskmaster/tasks/tasks_gameplay.json"
```

### 4.3 集成到 Git Hook

**Pre-commit Hook 示例**：

```bash
#!/bin/bash
# .git/hooks/pre-commit

# 获取当前任务 ID（从分支名或 commit message 提取）
TASK_ID=$(git branch --show-current | grep -oP '(?<=task-)\d+')

if [ -n "$TASK_ID" ]; then
    echo " 加载任务 #$TASK_ID 的完整上下文..."
    TASK_JSON=$(py -3 scripts/python/load_enhanced_task.py "$TASK_ID" --json)

    # 提取 ADR 引用
    ADR_REFS=$(echo "$TASK_JSON" | jq -r '.adr_refs[]' 2>/dev/null)

    if [ -n "$ADR_REFS" ]; then
        echo "[PASS] 任务关联 ADR："
        echo "$ADR_REFS"
    fi
fi
```

---

## 5. 输出格式规范

### 5.1 增强任务 JSON Schema

```json
{
  "taskmaster_id": 5,
  "title": "实现公会契约接口",
  "description": "创建 GuildContracts 目录并实现核心接口",
  "status": "in-progress",
  "priority": "high",
  "dependencies": [1, 2],
  "testStrategy": "xUnit 单元测试 + GdUnit4 集成测试",
  "details": "## 实现细节\n...",

  "original_id": "NG-0020",
  "story_id": "PRD-NEWGUILD-VS-0001",
  "adr_refs": ["ADR-0001", "ADR-0002", "ADR-0011"],
  "chapter_refs": ["CH01", "CH03", "CH08"],
  "overlay_refs": ["docs/architecture/overlays/PRD-NEWGUILD/08/guild-contracts.md"],
  "test_refs": [
    "Tests/Core/Contracts/GuildContractsTests.cs",
    "Tests/Scenes/test_guild_hud.gd"
  ],
  "acceptance": [
    "GuildContracts/ 目录存在且包含所有接口定义",
    "通过 xUnit 单元测试覆盖率 ≥ 90%",
    "GdUnit4 场景测试验证 Signals 连通性"
  ],
  "labels": ["contracts", "architecture", "T2"],
  "owner": "architecture",
  "layer": "core",
  "test_strategy": [
    "单元测试：覆盖所有契约接口的基本功能",
    "集成测试：验证 Godot 场景与契约的集成"
  ],
  "depends_on": ["NG-0001", "NG-0013"],

  "_source": {
    "tasks_json": ".taskmaster/tasks/tasks.json",
    "original_file": ".taskmaster/tasks/tasks_back.json",
    "tag": "master"
  }
}
```

### 5.2 Markdown 输出示例

```markdown
# 任务 #5: 实现公会契约接口

## 基础信息

- **原始 ID**: NG-0020
- **Story ID**: PRD-NEWGUILD-VS-0001
- **状态**: in-progress
- **优先级**: high
- **负责人**: architecture
- **层级**: core

## 任务描述

创建 GuildContracts 目录并实现核心接口

## 详细说明

## 实现细节
...

## 架构参考

### ADR 引用

- `ADR-0001`
- `ADR-0002`
- `ADR-0011`

### 章节引用

- `CH01`
- `CH03`
- `CH08`

### Overlay 文档

- `docs/architecture/overlays/PRD-NEWGUILD/08/guild-contracts.md`

## 测试策略

- 单元测试：覆盖所有契约接口的基本功能
- 集成测试：验证 Godot 场景与契约的集成

### 测试文件引用

- `Tests/Core/Contracts/GuildContractsTests.cs`
- `Tests/Scenes/test_guild_hud.gd`

## 验收标准

- [ ] GuildContracts/ 目录存在且包含所有接口定义
- [ ] 通过 xUnit 单元测试覆盖率 ≥ 90%
- [ ] GdUnit4 场景测试验证 Signals 连通性

## 依赖任务

- `NG-0001`
- `NG-0013`

**标签**: contracts, architecture, T2
```

---

## 6. 故障排查

### 6.1 任务未找到

**错误**：`任务 ID 5 在 Tag 'master' 中未找到`

**原因**：
- tasks.json 中不存在该 ID
- Tag 名称错误

**解决**：
```bash
# 检查 tasks.json 中的任务列表
cat .taskmaster/tasks/tasks.json | jq '.master.tasks[] | {id, title}'

# 指定正确的 Tag
py -3 scripts/python/load_enhanced_task.py 5 --tag feature-docs
```

### 6.2 原始任务未关联

**现象**：输出中 `original_id` 为 `N/A`，缺少元数据

**原因**：
- 原始文件中没有 `taskmaster_id` 字段
- 未运行 `build_taskmaster_tasks.py` 建立映射

**解决**：
```bash
# 重新运行映射脚本
py -3 scripts/python/build_taskmaster_tasks.py \
  --tasks-file .taskmaster/tasks/tasks_back.json \
  --tag master
```

### 6.3 自定义文件路径无效

**错误**：原始任务文件未找到

**解决**：
```bash
# 检查文件是否存在
ls -la .taskmaster/tasks/

# 使用绝对路径
py -3 scripts/python/load_enhanced_task.py 5 \
  --original-files "C:\buildgame\newguild\.taskmaster\tasks\tasks_back.json"
```

---

## 7. 扩展与集成

### 7.1 SuperClaude Slash Command

创建 `.claude/commands/task-context.md`：

```markdown
加载任务 $ARGUMENTS 的完整上下文（包括元数据）

执行步骤：

1. 运行任务增强加载器：
   ```bash
   py -3 scripts/python/load_enhanced_task.py $ARGUMENTS
   ```

2. 解析输出的 Markdown

3. 提取关键信息：
   - ADR 引用（用于 commit message）
   - 验收标准（用于任务完成检查）
   - 测试文件（用于运行相关测试）

4. 显示任务摘要
```

### 7.2 CI/CD 集成

**GitHub Actions 示例**：

```yaml
name: Validate Task Context

on:
  pull_request:
    branches: [main, develop]

jobs:
  check-task-mapping:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3

      - name: Setup Python
        uses: actions/setup-python@v4
        with:
          python-version: '3.13'

      - name: Extract Task ID from PR Title
        id: task
        run: |
          TASK_ID=$(echo "${{ github.event.pull_request.title }}" | grep -oP '(?<=task-)\d+')
          echo "task_id=$TASK_ID" >> $GITHUB_OUTPUT

      - name: Load Enhanced Task Context
        if: steps.task.outputs.task_id != ''
        run: |
          py -3 scripts/python/load_enhanced_task.py ${{ steps.task.outputs.task_id }} --json > task_context.json

      - name: Validate ADR References
        if: steps.task.outputs.task_id != ''
        run: |
          ADR_REFS=$(cat task_context.json | jq -r '.adr_refs[]')
          for adr in $ADR_REFS; do
            if [ ! -f "docs/adr/$adr.md" ]; then
              echo "[FAIL] ADR 文件不存在: $adr"
              exit 1
            fi
          done
          echo "[PASS] 所有 ADR 引用有效"
```

---

## 8. 最佳实践

### 8.1 工作流建议

1. **开始任务前**：
   ```bash
   # 加载完整上下文
   py -3 scripts/python/load_enhanced_task.py 5

   # 检查 ADR 引用
   # 查看验收标准
   # 了解测试策略
   ```

2. **开发过程中**：
   - 遵循 `adr_refs` 中指定的架构决策
   - 参考 `chapter_refs` 和 `overlay_refs` 的文档
   - 按照 `test_strategy` 编写测试

3. **Commit 时**：
   - 引用相关 ADR（从 `adr_refs` 提取）
   - 提及任务 ID 和 Story ID

4. **任务完成前**：
   - 逐项检查 `acceptance` 标准
   - 运行 `test_refs` 中的所有测试
   - 更新任务状态

### 8.2 团队协作

**约定**：
- [PASS] 所有任务必须通过 `build_taskmaster_tasks.py` 映射
- [PASS] Commit 消息必须包含任务 ID 和 ADR 引用
- [PASS] PR 描述必须附加任务上下文（使用 `load_enhanced_task.py` 生成）

---

## 附录 A：快速参考

### 命令速查表

| 命令 | 用途 |
|------|------|
| `py -3 scripts/python/load_enhanced_task.py N` | 加载任务 N 的完整上下文（Markdown） |
| `py -3 scripts/python/load_enhanced_task.py N --json` | 加载任务 N 的完整上下文（JSON） |
| `py -3 scripts/python/load_enhanced_task.py N --tag T` | 从指定 Tag 加载任务 |
| `py -3 scripts/python/load_enhanced_task.py N --original-files F1 --original-files F2` | 指定原始文件路径 |

### 环境变量

| 变量 | 说明 | 示例 |
|------|------|------|
| `TASK_ORIGINAL_FILES` | 原始任务文件路径（逗号分隔） | `.taskmaster/tasks/tasks_back.json,.taskmaster/tasks/custom.json` |

### 输出字段映射

| tasks.json 字段 | 原始文件字段 | 增强输出字段 |
|----------------|-------------|-------------|
| `id` | `taskmaster_id` | `taskmaster_id` |
| - | `id` | `original_id` |
| `title` | `title` | `title` |
| `description` | `description` | `description` |
| `status` | `status` | `status` |
| `priority` | 从 `P1/P2/P3` 映射 | `priority` |
| `dependencies` | 从 `depends_on` 映射 | `dependencies` |
| `testStrategy` | 从 `test_strategy[]` 合并 | `testStrategy` |
| `details` | 聚合自定义字段 | `details` |
| - | `story_id` | `story_id` |
| - | `adr_refs` | `adr_refs` |
| - | `chapter_refs` | `chapter_refs` |
| - | `overlay_refs` | `overlay_refs` |
| - | `test_refs` | `test_refs` |
| - | `acceptance` | `acceptance` |
| - | `labels` | `labels` |
| - | `owner` | `owner` |
| - | `layer` | `layer` |

---

**文档维护者**：Claude Code AI
**相关文档**：
- `docs/task-master-constraints.md` - Task Master 技术约束
- `cifix.txt` - 映射脚本说明
- `scripts/python/build_taskmaster_tasks.py` - 双文件映射脚本
