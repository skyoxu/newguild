#!/usr/bin/env python3
"""
测试污染检测工具 - Windows 适配版
根据 root-cause-tracing skill 的 find-polluter.sh 改写

用法:
    py -3 scripts/python/find_polluter.py <检查路径> <测试模式>

示例:
    # 检测哪个测试创建了 .git 目录
    py -3 scripts/python/find_polluter.py .git "tests/**/*.cs"

    # 检测 GdUnit4 测试污染
    py -3 scripts/python/find_polluter.py user://save.db "tests/**/*.gd"
"""

import sys
import os
import subprocess
import glob
from pathlib import Path

def find_test_files(pattern: str) -> list[str]:
    """根据 glob 模式查找测试文件"""
    # 支持 ** 递归匹配
    files = []
    for path in Path('.').glob(pattern):
        if path.is_file():
            files.append(str(path))
    return sorted(files)

def pollution_exists(check_path: str) -> bool:
    """检查污染路径是否存在"""
    return os.path.exists(check_path)

def run_test(test_file: str, is_gdunit: bool = False) -> bool:
    """运行单个测试文件,返回是否成功"""
    try:
        if is_gdunit:
            # GdUnit4 测试 (需要 Godot headless)
            cmd = [
                "py", "-3", "scripts/python/godot_tests.py",
                "--headless",
                "--file", test_file
            ]
        else:
            # xUnit 测试
            cmd = ["dotnet", "test", "--filter", f"FullyQualifiedName~{Path(test_file).stem}"]

        subprocess.run(cmd, capture_output=True, check=False, timeout=30)
        return True
    except Exception as e:
        print(f"⚠️  测试执行失败: {e}")
        return False

def main():
    if len(sys.argv) != 3:
        print("用法: py -3 scripts/python/find_polluter.py <检查路径> <测试模式>")
        print("示例: py -3 scripts/python/find_polluter.py .git \"tests/**/*.cs\"")
        sys.exit(1)

    pollution_check = sys.argv[1]
    test_pattern = sys.argv[2]

    print(f"🔍 查找创建以下路径的测试: {pollution_check}")
    print(f"测试模式: {test_pattern}")
    print()

    # 查找测试文件
    test_files = find_test_files(test_pattern)
    total = len(test_files)

    if total == 0:
        print(f"❌ 未找到匹配模式的测试文件: {test_pattern}")
        sys.exit(1)

    print(f"找到 {total} 个测试文件")
    print()

    # 检测是否为 GdUnit4 测试
    is_gdunit = test_pattern.endswith('.gd')

    # 逐个运行测试
    for count, test_file in enumerate(test_files, 1):
        # 如果污染已存在,跳过
        if pollution_exists(pollution_check):
            print(f"⚠️  污染在测试 {count}/{total} 之前已存在")
            print(f"   跳过: {test_file}")
            continue

        print(f"[{count}/{total}] 测试: {test_file}")

        # 运行测试
        run_test(test_file, is_gdunit)

        # 检查污染是否出现
        if pollution_exists(pollution_check):
            print()
            print("🎯 找到污染源!")
            print(f"   测试: {test_file}")
            print(f"   创建: {pollution_check}")
            print()
            print("污染详情:")

            # 显示污染路径详情
            if os.path.isdir(pollution_check):
                print(f"  目录: {pollution_check}")
                for item in os.listdir(pollution_check):
                    print(f"    - {item}")
            else:
                size = os.path.getsize(pollution_check)
                print(f"  文件: {pollution_check} ({size} bytes)")

            print()
            print("调查建议:")
            if is_gdunit:
                print(f"  py -3 scripts/python/godot_tests.py --headless --file {test_file}")
            else:
                print(f"  dotnet test --filter \"FullyQualifiedName~{Path(test_file).stem}\"")
            print(f"  查看测试代码: {test_file}")
            sys.exit(1)

    print()
    print("✅ 未找到污染源 - 所有测试都是干净的!")
    sys.exit(0)

if __name__ == "__main__":
    main()
