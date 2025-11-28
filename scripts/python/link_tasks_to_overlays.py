#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
批量更新任务的 overlay 字段

扫描 docs/architecture/overlays/<PRD-ID>/08/ 目录，
自动匹配 .taskmaster/tasks/*.json 中的任务与对应的 ACCEPTANCE_CHECKLIST.md，
并填充 overlay 字段（或 overlay_refs）。

使用方法:
    py -3 scripts/python/link_tasks_to_overlays.py
    py -3 scripts/python/link_tasks_to_overlays.py --task-file .taskmaster/tasks/tasks_gameplay.json
    py -3 scripts/python/link_tasks_to_overlays.py --dry-run
"""

import json
from pathlib import Path
import re
from typing import Optional
import argparse


def find_acceptance_checklists(root: Path) -> dict[str, str]:
    """
    扫描 overlays 目录，收集所有 ACCEPTANCE_CHECKLIST.md 的路径

    返回: {PRD-ID: 相对路径}
    """
    overlays_root = root / "docs" / "architecture" / "overlays"
    if not overlays_root.exists():
        return {}

    result: dict[str, str] = {}
    for prd_dir in overlays_root.iterdir():
        if not prd_dir.is_dir():
            continue

        checklist = prd_dir / "08" / "ACCEPTANCE_CHECKLIST.md"
        if checklist.exists():
            rel_path = checklist.relative_to(root)
            prd_id = prd_dir.name
            result[prd_id] = str(rel_path).replace("\\", "/")

    return result


def extract_prd_id_from_story(story_id: str) -> Optional[str]:
    """
    从 story_id 提取 PRD-ID

    示例:
    - "PRD-GUILD-MANAGER-CORE-EVENT-ENGINE" → "PRD-Guild-Manager"
    - "PRD-NEWGUILD-VS-0001" → "PRD-Newguild" (根据实际存在的 overlay 目录决定)
    """
    if not story_id:
        return None

    # 提取 PRD- 开头的部分
    match = re.match(r"PRD-([A-Z]+(?:-[A-Z]+)*)", story_id, re.IGNORECASE)
    if not match:
        return None

    # 转换为标准格式：PRD-Guild-Manager (首字母大写，其余小写)
    parts = match.group(1).split("-")
    formatted = "-".join(p.capitalize() for p in parts)
    return f"PRD-{formatted}"


def match_task_to_overlay(task: dict, checklists: dict[str, str]) -> Optional[str]:
    """
    匹配任务到对应的 ACCEPTANCE_CHECKLIST.md

    匹配策略:
    1. 从 story_id 提取 PRD-ID
    2. 在 checklists 中查找对应的路径
    3. 如果 story_id 不含 PRD-ID，返回 None
    """
    story_id = task.get("story_id", "")
    prd_id = extract_prd_id_from_story(story_id)

    if not prd_id:
        return None

    return checklists.get(prd_id)


def update_task_overlays(
    task_file: Path,
    checklists: dict[str, str],
    dry_run: bool = False
) -> tuple[int, int]:
    """
    更新任务文件中的 overlay 字段

    返回: (更新数量, 跳过数量)
    """
    if not task_file.exists():
        print(f"错误: 任务文件不存在 {task_file}")
        return 0, 0

    # 读取任务文件
    with open(task_file, "r", encoding="utf-8") as f:
        data = json.load(f)

    # 兼容两种结构：直接数组 [...] 或对象 {"tasks": [...]}
    if isinstance(data, list):
        tasks = data
    elif isinstance(data, dict) and "tasks" in data:
        tasks = data["tasks"]
    else:
        print(f"错误: 任务文件结构不符合预期 {task_file}")
        return 0, 0

    updated = 0
    skipped = 0

    for task in tasks:
        tid = task.get("id")
        story_id = task.get("story_id", "")

        # 如果已有 overlay 或 overlay_refs，跳过
        if task.get("overlay") or task.get("overlay_refs"):
            print(f"跳过 {tid}: 已存在 overlay 字段")
            skipped += 1
            continue

        # 匹配 overlay
        overlay_path = match_task_to_overlay(task, checklists)
        if not overlay_path:
            print(f"跳过 {tid}: 无法从 story_id='{story_id}' 匹配到 overlay")
            skipped += 1
            continue

        # 更新字段
        print(f"更新 {tid}: overlay = {overlay_path}")
        if not dry_run:
            # 优先使用 overlay_refs（与现有任务格式一致）
            if "overlay_refs" in task or any("overlay_refs" in t for t in tasks):
                task["overlay_refs"] = [overlay_path]
            else:
                task["overlay"] = overlay_path

        updated += 1

    # 保存文件
    if not dry_run and updated > 0:
        with open(task_file, "w", encoding="utf-8") as f:
            if isinstance(data, list):
                json.dump(tasks, f, ensure_ascii=False, indent=2)
            else:
                json.dump(data, f, ensure_ascii=False, indent=2)
        print(f"\n已保存 {task_file}")

    return updated, skipped


def main() -> None:
    parser = argparse.ArgumentParser(
        description="批量更新任务的 overlay 字段",
        formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument(
        "--task-file",
        type=str,
        help="指定要更新的任务文件路径（默认更新所有 .taskmaster/tasks/*.json）"
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="仅预览变更，不实际修改文件"
    )

    args = parser.parse_args()

    root = Path(__file__).resolve().parents[2]

    # 收集 ACCEPTANCE_CHECKLIST.md
    checklists = find_acceptance_checklists(root)
    print(f"发现 {len(checklists)} 个 ACCEPTANCE_CHECKLIST.md:")
    for prd_id, path in checklists.items():
        print(f"  {prd_id} → {path}")
    print()

    if not checklists:
        print("警告: 未找到任何 ACCEPTANCE_CHECKLIST.md")
        return

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
    total_updated = 0
    total_skipped = 0

    for task_file in task_files:
        print(f"\n{'='*60}")
        print(f"处理: {task_file.name}")
        print(f"{'='*60}")

        updated, skipped = update_task_overlays(task_file, checklists, args.dry_run)
        total_updated += updated
        total_skipped += skipped

    # 汇总
    print(f"\n{'='*60}")
    print("汇总")
    print(f"{'='*60}")
    print(f"更新: {total_updated} 个任务")
    print(f"跳过: {total_skipped} 个任务")

    if args.dry_run:
        print("\n[DRY RUN] 未实际修改任何文件")
        print("移除 --dry-run 参数以应用变更")
    else:
        print("\n完成！")


if __name__ == "__main__":
    main()
