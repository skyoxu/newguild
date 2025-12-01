#!/usr/bin/env python3
"""验证任务映射完整性"""

import json
from pathlib import Path

def main():
    print("=" * 60)
    print("任务映射验证报告")
    print("=" * 60)

    # 读取 tasks.json
    tasks_json_path = Path(".taskmaster/tasks/tasks.json")
    with open(tasks_json_path, 'r', encoding='utf-8') as f:
        tasks_data = json.load(f)

    # 读取原始文件
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

    # 检查 master tag 的任务
    master_tasks = tasks_data.get("master", {}).get("tasks", [])

    print(f"\n检查 'master' Tag 中的 {len(master_tasks)} 个任务:\n")

    success_count = 0
    partial_count = 0
    missing_count = 0

    for task in master_tasks[:10]:  # 检查前10个任务
        tm_id = task["id"]
        title = task["title"][:40] + "..." if len(task["title"]) > 40 else task["title"]

        print(f"任务 #{tm_id}: {title}")

        if tm_id in original_tasks:
            orig = original_tasks[tm_id]
            print(f"  ✅ 映射成功: {orig['id']} ({orig['file']})")
            print(f"     ADR引用: {'✅' if orig['has_adr_refs'] else '❌'}")
            print(f"     测试文件: {'✅' if orig['has_test_refs'] else '❌'}")
            print(f"     验收标准: {'✅' if orig['has_acceptance'] else '❌'}")
            print(f"     Story ID: {'✅' if orig['has_story_id'] else '❌'}")

            if all([orig['has_adr_refs'], orig['has_acceptance'], orig['has_story_id']]):
                print(f"     状态: ✅ 完整元数据")
                success_count += 1
            else:
                print(f"     状态: ⚠️ 部分元数据缺失")
                partial_count += 1
        else:
            print(f"  ❌ 未找到原始任务映射")
            missing_count += 1

        print()

    # 汇总统计
    print("=" * 60)
    print("统计汇总")
    print("=" * 60)
    print(f"完整元数据: {success_count} 个 ✅")
    print(f"部分元数据: {partial_count} 个 ⚠️")
    print(f"缺失映射: {missing_count} 个 ❌")
    print(f"总计: {success_count + partial_count + missing_count} 个")

    if success_count == len(master_tasks[:10]):
        print("\n✅ 所有检查的任务都成功加载了完整元数据！")
    else:
        print(f"\n⚠️ {partial_count + missing_count} 个任务存在元数据问题")

if __name__ == "__main__":
    main()
