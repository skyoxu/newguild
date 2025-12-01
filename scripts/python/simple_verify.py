#!/usr/bin/env python3
"""Simple task mapping verification - ASCII output only"""

import json
from pathlib import Path

def main():
    print("=" * 60)
    print("Task Mapping Verification Report (ASCII)")
    print("=" * 60)

    # Read tasks.json
    tasks_json_path = Path(".taskmaster/tasks/tasks.json")
    with open(tasks_json_path, 'r', encoding='utf-8') as f:
        tasks_data = json.load(f)

    # Read original files
    original_files = [
        (".taskmaster/tasks/tasks_back.json", "NG"),
        (".taskmaster/tasks/tasks_gameplay.json", "GM")
    ]

    original_tasks = {}
    for file_path, prefix in original_files:
        path = Path(file_path)
        if path.exists():
            with open(path, 'r', encoding='utf-8') as f:
                tasks = json.load(f)
                for task in tasks:
                    if task.get("taskmaster_id"):
                        original_tasks[task["taskmaster_id"]] = {
                            "id": task["id"],
                            "file": file_path,
                            "prefix": prefix,
                            "has_adr_refs": len(task.get("adr_refs", [])) > 0,
                            "has_test_refs": len(task.get("test_refs", [])) > 0,
                            "has_acceptance": len(task.get("acceptance", [])) > 0,
                            "has_story_id": bool(task.get("story_id"))
                        }

    # Check master tag tasks
    master_tasks = tasks_data.get("master", {}).get("tasks", [])

    print(f"\nChecking {len(master_tasks)} tasks in 'master' tag:\n")

    success_count = 0
    partial_count = 0
    missing_count = 0

    for task in master_tasks:
        tm_id = task["id"]
        title = task["title"][:40] + "..." if len(task["title"]) > 40 else task["title"]

        print(f"Task #{tm_id}: {title}")

        if tm_id in original_tasks:
            orig = original_tasks[tm_id]
            print(f"  [OK] Mapped: {orig['id']} ({orig['file']})")
            print(f"     ADR refs: {'YES' if orig['has_adr_refs'] else 'NO'}")
            print(f"     Test files: {'YES' if orig['has_test_refs'] else 'NO'}")
            print(f"     Acceptance: {'YES' if orig['has_acceptance'] else 'NO'}")
            print(f"     Story ID: {'YES' if orig['has_story_id'] else 'NO'}")

            if all([orig['has_adr_refs'], orig['has_acceptance'], orig['has_story_id']]):
                print(f"     Status: COMPLETE metadata")
                success_count += 1
            else:
                print(f"     Status: PARTIAL metadata")
                partial_count += 1
        else:
            print(f"  [MISS] No original task mapping found")
            missing_count += 1

        print()

    # Summary
    print("=" * 60)
    print("Summary Statistics")
    print("=" * 60)
    print(f"Complete metadata: {success_count} tasks")
    print(f"Partial metadata: {partial_count} tasks")
    print(f"Missing mapping: {missing_count} tasks")
    print(f"Total: {success_count + partial_count + missing_count} tasks")

    if success_count == len(master_tasks):
        print("\n[SUCCESS] All tasks loaded complete metadata!")
    else:
        print(f"\n[WARNING] {partial_count + missing_count} tasks have metadata issues")

if __name__ == "__main__":
    main()
