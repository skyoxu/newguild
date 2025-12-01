# 任务上下文增强器 - 快速开始

## 概述

`load_enhanced_task.py` 脚本合并 `tasks.json` 和原始任务文件（如 `tasks_back.json`）的信息，为 SuperClaude 提供完整任务上下文。

## 基本用法

### 查看任务完整信息

```bash
# Markdown 格式（人类可读）
py -3 scripts/python/load_enhanced_task.py 1

# JSON 格式（脚本解析）
py -3 scripts/python/load_enhanced_task.py 1 --json
```

### 指定 Tag

```bash
py -3 scripts/python/load_enhanced_task.py 5 --tag feature-docs
```

### 自定义原始文件路径

```bash
py -3 scripts/python/load_enhanced_task.py 5 \
  --original-files .taskmaster/tasks/tasks_back.json \
  --original-files .taskmaster/tasks/custom.json
```

## 集成到工作流

### 1. 开始任务前

```bash
# 加载任务 5 的完整上下文
py -3 scripts/python/load_enhanced_task.py 5

# 输出包含：
# - ADR 引用（用于 commit message）
# - 验收标准（用于任务完成检查）
# - 测试文件引用（用于运行相关测试）
# - 章节和 Overlay 文档引用
```

### 2. SuperClaude Commit 集成

```bash
# 获取任务上下文（JSON 格式）
TASK_JSON=$(py -3 scripts/python/load_enhanced_task.py 5 --json)

# 提取 ADR 引用
echo "$TASK_JSON" | jq -r '.adr_refs[]'

# 提取验收标准
echo "$TASK_JSON" | jq -r '.acceptance[]'
```

### 3. 环境变量配置

```bash
# Windows PowerShell
$env:TASK_ORIGINAL_FILES=".taskmaster/tasks/tasks_back.json,.taskmaster/tasks/tasks_gameplay.json"

# Bash/Git Bash
export TASK_ORIGINAL_FILES=".taskmaster/tasks/tasks_back.json,.taskmaster/tasks/tasks_gameplay.json"
```

## 输出示例

### Markdown 输出

```markdown
# 任务 #5: 实现公会契约接口

## 基础信息

- **原始 ID**: NG-0020
- **Story ID**: PRD-NEWGUILD-VS-0001
- **状态**: pending
- **优先级**: high
- **负责人**: architecture
- **层级**: core

## 架构参考

### ADR 引用

- `ADR-0001`
- `ADR-0002`
- `ADR-0011`

### 章节引用

- `CH01`
- `CH03`
- `CH08`

## 验收标准

- [ ] GuildContracts/ 目录存在且包含所有接口定义
- [ ] 通过 xUnit 单元测试覆盖率 ≥ 90%
- [ ] GdUnit4 场景测试验证 Signals 连通性
```

### JSON 输出

```json
{
  "taskmaster_id": 5,
  "title": "实现公会契约接口",
  "original_id": "NG-0020",
  "story_id": "PRD-NEWGUILD-VS-0001",
  "adr_refs": ["ADR-0001", "ADR-0002", "ADR-0011"],
  "chapter_refs": ["CH01", "CH03", "CH08"],
  "acceptance": [
    "GuildContracts/ 目录存在且包含所有接口定义",
    "通过 xUnit 单元测试覆盖率 ≥ 90%"
  ]
}
```

## 常见问题

### Q: 任务未找到

**错误**: `任务 ID 5 在 Tag 'master' 中未找到`

**解决**:
```bash
# 检查 tasks.json 中的任务列表
cat .taskmaster/tasks/tasks.json | jq '.master.tasks[] | {id, title}'

# 或指定正确的 Tag
py -3 scripts/python/load_enhanced_task.py 5 --tag feature-docs
```

### Q: 原始任务未关联

**现象**: 输出中 `original_id` 为 `null`，缺少元数据

**原因**: 未运行 `build_taskmaster_tasks.py` 建立映射

**解决**:
```bash
py -3 scripts/python/build_taskmaster_tasks.py \
  --tasks-file .taskmaster/tasks/tasks_back.json \
  --tag master
```

### Q: 中文乱码

**原因**: Windows 终端编码问题

**解决**:
```bash
# PowerShell
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# 或使用 Windows Terminal（自动支持 UTF-8）
```

## 相关文档

- `docs/superclaude-task-mapping.md` - 完整架构文档
- `docs/task-master-constraints.md` - Task Master 技术约束
- `cifix.txt` - 映射脚本说明
