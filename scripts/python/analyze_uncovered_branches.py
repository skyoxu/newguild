#!/usr/bin/env python3
"""分析未覆盖分支的详细信息"""

import xml.etree.ElementTree as ET
import sys
from pathlib import Path
from collections import defaultdict

def analyze_coverage(coverage_file: Path):
    """分析覆盖率报告并输出未覆盖分支详情"""

    tree = ET.parse(coverage_file)
    root = tree.getroot()

    # 获取总体覆盖率
    summary = root.find('.//Summary')
    if summary is not None:
        seq_cov = float(summary.get('sequenceCoverage', '0'))
        branch_cov = float(summary.get('branchCoverage', '0'))
        print(f'Overall Coverage:')
        print(f'  Line: {seq_cov:.2f}%')
        print(f'  Branch: {branch_cov:.2f}%')
        print()

    # 收集未覆盖分支
    uncovered_details = defaultdict(lambda: defaultdict(list))

    for module in root.findall('.//Module'):
        # 构建文件ID映射
        files = {}
        for file_elem in module.findall('.//File'):
            files[file_elem.get('uid')] = file_elem.get('fullPath', '')

        # 检查每个方法的分支点
        for cls in module.findall('.//Class'):
            for method in cls.findall('.//Method'):
                # 获取文件引用
                file_ref = method.find('.//FileRef')
                if file_ref is None:
                    continue

                file_path = files.get(file_ref.get('uid'), '')
                if not file_path or 'Tests' in file_path:
                    continue

                file_name = Path(file_path).name
                method_name = method.get('name')

                # 检查分支点
                branch_points = method.findall('.//BranchPoint')
                uncovered_count = 0

                for bp in branch_points:
                    vc = int(bp.get('vc', '0'))
                    if vc == 0:
                        uncovered_count += 1
                        line_num = bp.get('sl', 'unknown')
                        offset = bp.get('offset', '')
                        path_type = bp.get('path', '')

                        uncovered_details[file_name][method_name].append({
                            'line': line_num,
                            'offset': offset,
                            'path': path_type
                        })

    # 输出未覆盖分支详情
    if uncovered_details:
        print('Uncovered Branches Detail:')
        print('=' * 80)

        # 按文件排序
        for file_name in sorted(uncovered_details.keys()):
            methods = uncovered_details[file_name]
            total_branches = sum(len(branches) for branches in methods.values())

            print(f'\n{file_name} ({total_branches} uncovered branches):')

            # 按方法排序
            for method_name in sorted(methods.keys()):
                branches = methods[method_name]
                print(f'  {method_name}():')
                for branch in branches:
                    print(f'    Line {branch["line"]}: offset={branch["offset"]}, path={branch["path"]}')
    else:
        print('All branches covered!')

    return uncovered_details

def main():
    if len(sys.argv) > 1:
        coverage_file = Path(sys.argv[1])
    else:
        # 查找最新的覆盖率报告
        test_results = Path('Game.Core.Tests/TestResults')
        if not test_results.exists():
            print('[FAIL] No test results found. Run: dotnet test --collect:"XPlat Code Coverage"')
            return 1

        coverage_files = list(test_results.glob('*/coverage.opencover.xml'))
        if not coverage_files:
            print('[FAIL] No coverage.opencover.xml found')
            return 1

        # 使用最新的报告
        coverage_file = max(coverage_files, key=lambda p: p.stat().st_mtime)
        print(f'Using coverage report: {coverage_file}')
        print()

    analyze_coverage(coverage_file)
    return 0

if __name__ == '__main__':
    sys.exit(main())
