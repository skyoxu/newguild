#!/usr/bin/env python3
"""测试脚本：验证任务加载器能否正确加载所有任务的完整信息"""

import json
import sys

def test_task(task_id):
    """测试单个任务的加载"""
    import subprocess

    result = subprocess.run(
        ["py", "-3", "scripts/python/load_enhanced_task.py", str(task_id), "--json"],
        capture_output=True,
        text=True,
        encoding='utf-8'
    )

    if result.returncode != 0:
        print(f"❌ 任务 {task_id}: 加载失败")
        print(f"   错误: {result.stderr}")
        return False

    try:
        data = json.loads(result.stdout)
    except json.JSONDecodeError as e:
        print(f"❌ 任务 {task_id}: JSON 解析失败")
        print(f"   错误: {e}")
        return False

    # 验证关键字段
    print(f"\n{'='*60}")
    print(f"✅ 任务 #{data['taskmaster_id']}")
    print(f"{'='*60}")
    print(f"原始 ID: {data.get('original_id', 'N/A')}")
    print(f"Story ID: {data.get('story_id', 'N/A')}")
    print(f"状态: {data['status']}")
    print(f"优先级: {data['priority']}")
    print(f"负责人: {data.get('owner', 'N/A')}")
    print(f"层级: {data.get('layer', 'N/A')}")
    print(f"\n架构引用:")
    print(f"  - ADR: {len(data.get('adr_refs', []))} 个")
    print(f"  - 章节: {len(data.get('chapter_refs', []))} 个")
    print(f"  - Overlay: {len(data.get('overlay_refs', []))} 个")
    print(f"\n测试相关:")
    print(f"  - 测试策略: {len(data.get('test_strategy', []))} 条")
    print(f"  - 测试文件: {len(data.get('test_refs', []))} 个")
    print(f"  - 验收标准: {len(data.get('acceptance', []))} 条")
    print(f"\n数据来源:")
    print(f"  - 原始文件: {data['_source']['original_file']}")
    print(f"  - 映射成功: {data['_source']['has_original']}")

    # 检查是否有完整元数据
    has_metadata = (
        data.get('original_id') and
        data.get('story_id') and
        len(data.get('adr_refs', [])) > 0 and
        len(data.get('acceptance', [])) > 0 and
        data['_source']['has_original']
    )

    if has_metadata:
        print(f"\n✅ 完整元数据加载成功")
    else:
        print(f"\n⚠️ 部分元数据缺失")

    return has_metadata

def main():
    """测试多个任务"""
    print("测试任务加载器 - 验证完整信息加载")
    print("="*60)

    task_ids = [1, 2, 3, 4, 5]
    results = {}

    for task_id in task_ids:
        try:
            success = test_task(task_id)
            results[task_id] = success
        except Exception as e:
            print(f"\n❌ 任务 {task_id}: 未知错误")
            print(f"   {e}")
            results[task_id] = False

    # 汇总结果
    print(f"\n{'='*60}")
    print("测试汇总")
    print(f"{'='*60}")

    total = len(results)
    success = sum(results.values())

    print(f"总计: {total} 个任务")
    print(f"成功: {success} 个")
    print(f"失败: {total - success} 个")
    print(f"成功率: {success/total*100:.1f}%")

    if success == total:
        print("\n✅ 所有任务都成功加载了完整信息！")
        return 0
    else:
        print("\n⚠️ 部分任务未能加载完整信息")
        return 1

if __name__ == "__main__":
    sys.exit(main())
