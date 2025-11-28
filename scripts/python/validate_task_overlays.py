#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
验证任务 overlay 字段与 ACCEPTANCE_CHECKLIST.md 格式

检查项：
1. overlay 路径文件是否存在
2. ACCEPTANCE_CHECKLIST.md 格式是否正确（Front-Matter、必需字段）
3. ADR-Refs 引用的 ADR 是否存在

使用方法:
    py -3 scripts/python/validate_task_overlays.py
    py -3 scripts/python/validate_task_overlays.py --task-file .taskmaster/tasks/tasks_gameplay.json
"""

import json
from pathlib import Path
import re
from typing import Optional
import argparse


def extract_front_matter(content: str) -> Optional[dict]:
    """
    从 Markdown 文件提取 YAML Front-Matter

    返回: {PRD-ID, Title, Status, ADR-Refs, Test-Refs} 或 None
    """
    # 匹配 YAML Front-Matter (--- 开头和结尾)
    match = re.match(r'^---\s*\n(.*?)\n---', content, re.DOTALL)
    if not match:
        return None

    fm_text = match.group(1)
    result = {
        'PRD-ID': None,
        'Title': None,
        'Status': None,
        'ADR-Refs': [],
        'Test-Refs': []
    }

    # 解析简单的 YAML 字段（支持单行和列表）
    current_key = None
    for line in fm_text.split('\n'):
        line = line.strip()

        # 跳过注释
        if line.startswith('#'):
            continue

        # 检查是否是新的键
        if ':' in line and not line.startswith('-'):
            key, value = line.split(':', 1)
            key = key.strip()
            value = value.strip()

            if key in result:
                current_key = key
                if value:
                    # 单行值
                    if key in ['ADR-Refs', 'Test-Refs']:
                        result[key] = [value]
                    else:
                        result[key] = value
                else:
                    # 多行列表
                    result[key] = []
        # 处理列表项
        elif line.startswith('-') and current_key:
            value = line[1:].strip()
            # 移除行内注释
            if '#' in value:
                value = value.split('#')[0].strip()
            if value:
                result[current_key].append(value)

    return result


def validate_acceptance_checklist(checklist_path: Path, adr_ids: set[str]) -> list[str]:
    """
    验证 ACCEPTANCE_CHECKLIST.md 格式

    返回: 错误列表（空列表表示通过）
    """
    errors = []

    if not checklist_path.exists():
        return [f"文件不存在: {checklist_path}"]

    try:
        content = checklist_path.read_text(encoding='utf-8')
    except Exception as e:
        return [f"读取文件失败: {e}"]

    # 提取 Front-Matter
    fm = extract_front_matter(content)
    if not fm:
        errors.append("缺失 YAML Front-Matter (--- 开头和结尾)")
        return errors

    # 检查必需字段
    if not fm['PRD-ID']:
        errors.append("Front-Matter 缺失必需字段: PRD-ID")

    if not fm['Title']:
        errors.append("Front-Matter 缺失必需字段: Title")

    if not fm['Status']:
        errors.append("Front-Matter 缺失必需字段: Status")

    if not fm['ADR-Refs']:
        errors.append("Front-Matter 缺失必需字段: ADR-Refs")
    else:
        # 验证 ADR 引用是否存在
        for adr_ref in fm['ADR-Refs']:
            if adr_ref not in adr_ids:
                errors.append(f"ADR-Refs 引用的 ADR 不存在: {adr_ref}")

    if not fm['Test-Refs']:
        errors.append("Front-Matter 缺失必需字段: Test-Refs")

    # 检查内容结构（可选，检查是否包含关键章节）
    required_sections = [
        "一、文档完整性验收",
        "二、架构设计验收",
        "三、代码实现验收",
        "四、测试框架验收"
    ]

    for section in required_sections:
        if section not in content:
            errors.append(f"缺失必需章节: {section}")

    return errors


def collect_adr_ids(root: Path) -> set[str]:
    """收集所有存在的 ADR ID"""
    adr_dir = root / "docs" / "adr"
    ids: set[str] = set()
    if not adr_dir.exists():
        return ids
    for f in adr_dir.glob("ADR-*.md"):
        m = re.match(r"ADR-(\d{4})", f.stem)
        if m:
            ids.add(f"ADR-{m.group(1)}")
    return ids


def load_tasks_from_file(task_file: Path) -> list[dict]:
    """从任务文件加载任务列表"""
    if not task_file.exists():
        return []
    data = json.loads(task_file.read_text(encoding='utf-8'))
    # 兼容两种结构：直接数组 [...] 或对象 {"tasks": [...]}
    if isinstance(data, list):
        return data
    if isinstance(data, dict) and "tasks" in data:
        return data["tasks"]
    return []


def validate_task_file(
    root: Path,
    task_file: Path,
    file_label: str,
    adr_ids: set[str]
) -> tuple[int, int]:
    """
    验证单个任务文件的 overlay 引用

    返回: (总任务数, 通过的任务数)
    """
    tasks = load_tasks_from_file(task_file)
    if not tasks:
        print(f"\n{file_label}: 未找到任务或文件不存在")
        return 0, 0

    print(f"\n{'='*60}")
    print(f"{file_label}: {len(tasks)} 个任务")
    print(f"{'='*60}")

    passed = 0
    tasks_with_overlays = 0

    for task in sorted(tasks, key=lambda x: x.get("id", "")):
        tid = task.get("id")

        # 获取 overlay_refs 或 overlay
        overlay_refs = task.get("overlay_refs")
        if overlay_refs:
            overlays = overlay_refs if isinstance(overlay_refs, list) else [overlay_refs]
        else:
            overlay = task.get("overlay")
            overlays = [overlay] if overlay else []

        if not overlays:
            continue

        tasks_with_overlays += 1
        print(f"\n== {tid} ==")

        task_passed = True
        for overlay_path in overlays:
            full_path = root / overlay_path

            # 检查路径是否存在
            if not full_path.exists():
                print(f"  错误: overlay 文件不存在: {overlay_path}")
                task_passed = False
                continue

            # 如果是 ACCEPTANCE_CHECKLIST.md，验证格式
            if full_path.name == "ACCEPTANCE_CHECKLIST.md":
                errors = validate_acceptance_checklist(full_path, adr_ids)
                if errors:
                    print(f"  错误: ACCEPTANCE_CHECKLIST.md 格式问题:")
                    for err in errors:
                        print(f"    - {err}")
                    task_passed = False
                else:
                    print(f"  overlay OK: {overlay_path}")
            else:
                print(f"  overlay OK: {overlay_path}")

        if task_passed:
            passed += 1

    if tasks_with_overlays == 0:
        print("\n(无任务包含 overlay 字段)")

    return tasks_with_overlays, passed


def main() -> None:
    parser = argparse.ArgumentParser(
        description="验证任务 overlay 字段与 ACCEPTANCE_CHECKLIST.md 格式",
        formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument(
        "--task-file",
        type=str,
        help="指定要验证的任务文件路径（默认验证所有 .taskmaster/tasks/*.json）"
    )

    args = parser.parse_args()

    root = Path(__file__).resolve().parents[2]

    # 收集 ADR IDs
    adr_ids = collect_adr_ids(root)
    print(f"发现 {len(adr_ids)} 个 ADR ID: {sorted(list(adr_ids)[:10])} ...")
    print()

    # 确定要处理的任务文件
    if args.task_file:
        task_files = [Path(args.task_file)]
    else:
        tasks_dir = root / ".taskmaster" / "tasks"
        task_files = list(tasks_dir.glob("*.json"))

    if not task_files:
        print("错误: 未找到任何任务文件")
        return

    # 处理每个任务文件
    total_checked = 0
    total_passed = 0

    for task_file in task_files:
        checked, passed = validate_task_file(root, task_file, task_file.name, adr_ids)
        total_checked += checked
        total_passed += passed

    # 汇总
    print(f"\n{'='*60}")
    print("汇总")
    print(f"{'='*60}")
    print(f"检查的任务数（有 overlay 的）: {total_checked}")
    print(f"通过的任务数: {total_passed}")

    if total_checked == 0:
        print("\n提示: 未找到任何包含 overlay 字段的任务")
    elif total_passed < total_checked:
        failed = total_checked - total_passed
        print(f"\n错误: {failed} 个任务未通过验证")
        raise SystemExit(1)
    else:
        print("\n所有 overlay 验证通过!")


if __name__ == "__main__":
    main()
