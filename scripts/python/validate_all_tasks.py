#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
增强版任务回链校验工具

校验所有任务文件中的 ADR/CH 回链：
- tasks_back.json（所有 NG-* 任务）
- tasks_gameplay.json（所有 GM-* 任务）

使用方法:
    py -3 scripts/python/validate_all_tasks.py
"""

import json
from pathlib import Path
import re


# ADR 到 Chapter 的映射关系（来自 check_tasks_back_references.py）
ADR_FOR_CH = {
    "ADR-0002": ["CH02"],
    "ADR-0019": ["CH02"],
    "ADR-0003": ["CH03"],
    "ADR-0004": ["CH04"],
    "ADR-0006": ["CH05"],
    "ADR-0007": ["CH05", "CH06"],
    "ADR-0005": ["CH07"],
    "ADR-0011": ["CH07", "CH10"],
    "ADR-0008": ["CH10"],
    "ADR-0015": ["CH09"],
    "ADR-0018": ["CH01", "CH06", "CH07"],
    "ADR-0024": ["CH06", "CH07"],
    "ADR-0023": ["CH05"],
}


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


def collect_overlay_paths(root: Path) -> set[str]:
    """收集所有 overlay 文件路径"""
    overlay_root = root / "docs" / "architecture" / "overlays" / "PRD-Guild-Manager" / "08"
    if not overlay_root.exists():
        return set()
    paths: set[str] = set()
    for p in overlay_root.glob("*"):
        rel = p.relative_to(root)
        paths.add(str(rel).replace("\\", "/"))
    return paths


def load_tasks_from_file(task_file: Path) -> list[dict]:
    """从任务文件加载任务列表"""
    if not task_file.exists():
        return []
    data = json.loads(task_file.read_text(encoding="utf-8"))
    # 兼容两种结构：直接数组 [...] 或对象 {"tasks": [...]}
    if isinstance(data, list):
        return data
    if isinstance(data, dict) and "tasks" in data:
        return data["tasks"]
    return []


def validate_task(task: dict, adr_ids: set[str], overlay_paths: set[str]) -> bool:
    """
    校验单个任务的 ADR/CH/Overlay 回链

    返回 True 表示通过，False 表示有错误
    """
    tid = task.get("id")
    story_id = task.get("story_id")
    has_error = False

    print(f"\n== {tid} ==")
    print(f"story_id: {story_id}")

    # 1. 检查 ADR 引用是否存在
    missing_adrs = [a for a in task.get("adr_refs", []) if a not in adr_ids]
    if missing_adrs:
        print(f"  错误: 缺失的 ADR: {missing_adrs}")
        has_error = True
    else:
        print(f"  adr_refs OK ({len(task.get('adr_refs', []))} 个)")

    # 2. 检查 chapter_refs 是否与 ADR 映射一致
    expected_ch: set[str] = set()
    for adr in task.get("adr_refs", []):
        expected_ch.update(ADR_FOR_CH.get(adr, []))
    current_ch = set(task.get("chapter_refs", []))
    missing_ch = expected_ch - current_ch
    extra_ch = current_ch - expected_ch

    if missing_ch:
        print(f"  错误: 缺失的 chapter_refs (根据 ADR 映射): {sorted(missing_ch)}")
        has_error = True
    if extra_ch:
        print(f"  警告: 额外的 chapter_refs (未在 ADR 映射中): {sorted(extra_ch)}")
        # 注意：额外的 chapter 不算错误，只是警告
    if not missing_ch and not extra_ch:
        print(f"  chapter_refs OK ({len(current_ch)} 个，与 ADR 映射一致)")

    # 3. 检查 overlay_refs 文件是否存在
    refs = [p.replace("\\", "/") for p in task.get("overlay_refs", [])]
    if refs:
        missing_overlays = [p for p in refs if p not in overlay_paths]
        if missing_overlays:
            print(f"  错误: 缺失的 overlay 文件: {missing_overlays}")
            has_error = True
        else:
            print(f"  overlay_refs OK ({len(refs)} 个)")
    else:
        print("  overlay_refs: (无)")

    return not has_error


def validate_task_file(
    root: Path,
    task_file: Path,
    file_label: str,
    adr_ids: set[str],
    overlay_paths: set[str]
) -> tuple[int, int]:
    """
    校验单个任务文件

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
    for task in sorted(tasks, key=lambda x: x.get("id", "")):
        if validate_task(task, adr_ids, overlay_paths):
            passed += 1

    return len(tasks), passed


def main() -> None:
    root = Path(__file__).resolve().parents[2]

    # 收集 ADR 和 Overlay 信息
    adr_ids = collect_adr_ids(root)
    overlay_paths = collect_overlay_paths(root)

    print("收集到的 ADR ID 示例:", sorted(adr_ids)[:10], "...")
    print("收集到的 Overlay 文件数:", len(overlay_paths))

    # 校验 tasks_back.json
    tasks_back_file = root / ".taskmaster" / "tasks" / "tasks_back.json"
    total_back, passed_back = validate_task_file(
        root, tasks_back_file, "tasks_back.json", adr_ids, overlay_paths
    )

    # 校验 tasks_gameplay.json
    tasks_gameplay_file = root / ".taskmaster" / "tasks" / "tasks_gameplay.json"
    total_gameplay, passed_gameplay = validate_task_file(
        root, tasks_gameplay_file, "tasks_gameplay.json", adr_ids, overlay_paths
    )

    # 汇总结果
    total_tasks = total_back + total_gameplay
    total_passed = passed_back + passed_gameplay
    total_failed = total_tasks - total_passed

    print(f"\n{'='*60}")
    print("汇总结果")
    print(f"{'='*60}")
    print(f"tasks_back.json:    {passed_back}/{total_back} 通过")
    print(f"tasks_gameplay.json: {passed_gameplay}/{total_gameplay} 通过")
    print(f"总计:               {total_passed}/{total_tasks} 通过")

    if total_failed > 0:
        print(f"\n错误: {total_failed} 个任务未通过校验")
        raise SystemExit(1)
    else:
        print("\n所有任务校验通过!")


if __name__ == "__main__":
    main()
