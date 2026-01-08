#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Verify Taskmaster view ↔ master task mapping (Windows-friendly).

This script checks whether tasks in `.taskmaster/tasks/tasks.json` (SSoT)
have corresponding entries in view task files:
  - `.taskmaster/tasks/tasks_back.json` (NG-*)
  - `.taskmaster/tasks/tasks_gameplay.json` (GM-*)

Notes:
- Output is ASCII-only to avoid Windows console encoding problems.
- This script is defensive: it does not modify any repo files.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def main() -> int:
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

    print("=" * 60)
    print("Task mapping verification report")
    print("=" * 60)

    tasks_json_path = Path(".taskmaster/tasks/tasks.json")
    if not tasks_json_path.is_file():
        print("[FAIL] missing .taskmaster/tasks/tasks.json")
        return 1

    tasks_data = load_json(tasks_json_path)
    master_tasks = tasks_data.get("master", {}).get("tasks", [])
    if not isinstance(master_tasks, list):
        print("[FAIL] invalid tasks.json format: master.tasks is not a list")
        return 1

    view_files = [
        (Path(".taskmaster/tasks/tasks_back.json"), "NG"),
        (Path(".taskmaster/tasks/tasks_gameplay.json"), "GM"),
    ]

    view_by_master_id: dict[str, dict] = {}
    for path, prefix in view_files:
        if not path.is_file():
            continue
        tasks = load_json(path)
        if not isinstance(tasks, list):
            continue
        for task in tasks:
            tm = task.get("taskmaster_id")
            if tm is None:
                continue
            key = str(tm)
            view_by_master_id[key] = {
                "id": task.get("id"),
                "file": str(path).replace("\\", "/"),
                "prefix": prefix,
                "has_adr_refs": bool(task.get("adr_refs")),
                "has_test_refs": bool(task.get("test_refs")),
                "has_acceptance": bool(task.get("acceptance")),
                "has_story_id": bool(task.get("story_id")),
            }

    print(f"\nChecking master tasks: {len(master_tasks)} total\n")

    complete = 0
    partial = 0
    missing = 0

    for task in master_tasks:
        tm_id = str(task.get("id", ""))
        title = str(task.get("title", ""))
        title_short = title[:60] + "..." if len(title) > 60 else title
        print(f"Task #{tm_id}: {title_short}")

        orig = view_by_master_id.get(tm_id)
        if not orig:
            print("  [FAIL] missing mapping in view tasks")
            missing += 1
            print()
            continue

        print(f"  [OK] mapped_to: {orig['id']} ({orig['file']})")
        print(f"       adr_refs: {'OK' if orig['has_adr_refs'] else 'MISSING'}")
        print(f"       test_refs: {'OK' if orig['has_test_refs'] else 'MISSING'}")
        print(f"       acceptance: {'OK' if orig['has_acceptance'] else 'MISSING'}")
        print(f"       story_id: {'OK' if orig['has_story_id'] else 'MISSING'}")

        if orig["has_adr_refs"] and orig["has_acceptance"] and orig["has_story_id"]:
            print("       status: COMPLETE")
            complete += 1
        else:
            print("       status: PARTIAL")
            partial += 1
        print()

    print("=" * 60)
    print("Summary")
    print("=" * 60)
    print(f"COMPLETE: {complete}")
    print(f"PARTIAL:  {partial}")
    print(f"MISSING:  {missing}")
    print(f"TOTAL:    {complete + partial + missing}")

    if missing == 0:
        print("\n[OK] all master tasks have a view mapping.")
        return 0
    print(f"\n[WARN] {missing} master task(s) missing a view mapping.")
    return 1


if __name__ == "__main__":
    raise SystemExit(main())

