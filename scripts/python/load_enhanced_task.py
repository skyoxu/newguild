#!/usr/bin/env python3
"""
任务上下文增强加载器

功能：从 tasks.json 和原始任务文件中加载完整任务信息
用途：为 SuperClaude 提供包含所有元数据的增强任务上下文
"""

import json
import sys
import os
from pathlib import Path
from typing import Optional, Dict, Any, List

def load_enhanced_task(
    task_id: int,
    tasks_json_path: str = ".taskmaster/tasks/tasks.json",
    original_files: Optional[List[str]] = None,
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

    Raises:
        FileNotFoundError: 文件不存在
        ValueError: 任务 ID 未找到
    """

    # 默认原始文件列表
    if original_files is None:
        # 从环境变量读取
        env_files = os.getenv("TASK_ORIGINAL_FILES")
        if env_files:
            original_files = [f.strip() for f in env_files.split(",")]
        else:
            original_files = [
                ".taskmaster/tasks/tasks_back.json",
                ".taskmaster/tasks/tasks_gameplay.json",
                ".taskmaster/tasks/tasks_longterm.json"
            ]

    # 1. 读取 tasks.json 中的标准任务
    if not Path(tasks_json_path).exists():
        raise FileNotFoundError(f"tasks.json 文件不存在: {tasks_json_path}")

    with open(tasks_json_path, 'r', encoding='utf-8') as f:
        tasks_data = json.load(f)

    standard_task = None
    if tag in tasks_data and "tasks" in tasks_data[tag]:
        for task in tasks_data[tag]["tasks"]:
            if task["id"] == task_id:
                standard_task = task
                break

    if not standard_task:
        available_tags = list(tasks_data.keys())
        raise ValueError(
            f"任务 ID {task_id} 在 Tag '{tag}' 中未找到\n"
            f"可用 Tag: {available_tags}"
        )

    # 2. 在原始文件中查找匹配的任务
    original_task = None
    original_file_path = None

    for file_path in original_files:
        if not Path(file_path).exists():
            continue

        with open(file_path, 'r', encoding='utf-8') as f:
            try:
                tasks = json.load(f)
            except json.JSONDecodeError:
                print(f"警告: 无法解析 {file_path}", file=sys.stderr)
                continue

        # 处理数组或对象格式
        if isinstance(tasks, dict):
            tasks = tasks.get("tasks", [])

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
        "dependencies": standard_task.get("dependencies", []),
        "testStrategy": standard_task.get("testStrategy", ""),
        "details": standard_task.get("details", ""),
        "subtasks": standard_task.get("subtasks", []),

        # 元数据来源标记
        "_source": {
            "tasks_json": tasks_json_path,
            "original_file": original_file_path,
            "tag": tag,
            "has_original": original_task is not None
        }
    }

    # 4. 如果找到原始任务，添加完整元数据
    if original_task:
        enhanced_task.update({
            "original_id": original_task.get("id"),
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
    else:
        # 未找到原始任务，设置默认值
        enhanced_task.update({
            "original_id": None,
            "story_id": None,
            "adr_refs": [],
            "chapter_refs": [],
            "overlay_refs": [],
            "test_refs": [],
            "acceptance": [],
            "labels": [],
            "owner": None,
            "layer": None,
            "test_strategy": [],
            "depends_on": []
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

- **原始 ID**: {task.get('original_id') or 'N/A'}
- **Story ID**: {task.get('story_id') or 'N/A'}
- **状态**: {task['status']}
- **优先级**: {task['priority']}
- **负责人**: {task.get('owner') or 'N/A'}
- **层级**: {task.get('layer') or 'N/A'}

## 任务描述

{task['description']}

"""

    # 详细说明
    if task['details']:
        md += f"""## 详细说明

{task['details']}

"""

    # 架构参考
    has_arch_refs = (
        task.get('adr_refs') or
        task.get('chapter_refs') or
        task.get('overlay_refs')
    )

    if has_arch_refs:
        md += "## 架构参考\n\n"

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
        md += "## 原始依赖任务\n\n"
        for dep in task['depends_on']:
            md += f"- `{dep}`\n"
        md += "\n"

    if task.get('dependencies'):
        md += "## Task Master 依赖任务\n\n"
        for dep in task['dependencies']:
            md += f"- 任务 ID: `{dep}`\n"
        md += "\n"

    # 标签
    if task.get('labels'):
        md += f"\n**标签**: {', '.join(task['labels'])}\n"

    # 来源信息
    md += f"\n---\n\n"
    md += f"**数据来源**:\n"
    md += f"- Task Master: `{task['_source']['tag']}` Tag\n"
    if task['_source']['has_original']:
        md += f"- 原始文件: `{task['_source']['original_file']}`\n"
    else:
        md += f"- ⚠️ 未找到原始任务文件映射\n"

    return md


def main():
    """命令行入口"""
    import argparse

    parser = argparse.ArgumentParser(
        description="加载增强任务上下文（合并 tasks.json 和原始任务文件）"
    )
    parser.add_argument(
        "task_id",
        type=int,
        help="Task Master 数字 ID"
    )
    parser.add_argument(
        "--json",
        action="store_true",
        help="输出 JSON 格式（默认 Markdown）"
    )
    parser.add_argument(
        "--tag",
        default="master",
        help="Task Master Tag 名称（默认: master）"
    )
    parser.add_argument(
        "--tasks-json",
        default=".taskmaster/tasks/tasks.json",
        help="tasks.json 文件路径"
    )
    parser.add_argument(
        "--original-files",
        action="append",
        help="原始任务文件路径（可多次指定）"
    )

    args = parser.parse_args()

    try:
        task = load_enhanced_task(
            task_id=args.task_id,
            tasks_json_path=args.tasks_json,
            original_files=args.original_files,
            tag=args.tag
        )

        if args.json:
            print(json.dumps(task, ensure_ascii=False, indent=2))
        else:
            print(format_task_for_superclaude(task))

    except FileNotFoundError as e:
        print(f"❌ 文件错误: {e}", file=sys.stderr)
        sys.exit(1)
    except ValueError as e:
        print(f"❌ 任务错误: {e}", file=sys.stderr)
        sys.exit(1)
    except Exception as e:
        print(f"❌ 未知错误: {e}", file=sys.stderr)
        import traceback
        traceback.print_exc()
        sys.exit(1)


if __name__ == "__main__":
    main()
