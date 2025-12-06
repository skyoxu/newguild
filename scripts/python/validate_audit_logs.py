#!/usr/bin/env python3
"""验证 security-audit.jsonl 格式与字段完整性"""

import json
import sys
from pathlib import Path
from datetime import datetime
from typing import List, Dict, Any, Tuple

def validate_jsonl_format(log_file: Path) -> Tuple[bool, List[str]]:
    """验证 JSONL 格式"""
    errors = []
    line_num = 0

    if not log_file.exists():
        return False, [f"审计日志文件不存在: {log_file}"]

    try:
        with open(log_file, 'r', encoding='utf-8') as f:
            for line in f:
                line_num += 1
                line = line.strip()

                if not line:  # 跳过空行
                    continue

                try:
                    entry = json.loads(line)
                except json.JSONDecodeError as e:
                    errors.append(f"行 {line_num}: JSON 解析失败 - {e}")
                    continue

                # 验证必需字段
                required_fields = {'ts', 'action', 'reason', 'target', 'caller'}
                missing_fields = required_fields - set(entry.keys())
                if missing_fields:
                    errors.append(f"行 {line_num}: 缺少必需字段 {missing_fields}")

                # 验证 ts 字段格式
                if 'ts' in entry:
                    try:
                        datetime.fromisoformat(entry['ts'].replace('Z', '+00:00'))
                    except (ValueError, AttributeError) as e:
                        errors.append(f"行 {line_num}: ts 字段格式无效 '{entry.get('ts')}' - {e}")

                # 验证字段类型
                for field in required_fields:
                    if field in entry and not isinstance(entry[field], str):
                        errors.append(f"行 {line_num}: 字段 '{field}' 应为字符串类型，实际为 {type(entry[field])}")

    except Exception as e:
        return False, [f"读取文件失败: {e}"]

    if line_num == 0:
        return False, ["审计日志文件为空"]

    return len(errors) == 0, errors

def find_audit_logs(base_dir: Path = Path('.')) -> List[Path]:
    """查找所有 security-audit.jsonl 文件"""
    return list(base_dir.glob('logs/ci/**/security-audit.jsonl'))

def main():
    base_dir = Path(__file__).parent.parent.parent  # 项目根目录

    print("正在查找安全审计日志...")
    audit_logs = find_audit_logs(base_dir)

    if not audit_logs:
        print("未找到安全审计日志文件（logs/ci/**/security-audit.jsonl）")
        return 0

    print(f"找到 {len(audit_logs)} 个审计日志文件\n")

    total_valid = 0
    total_invalid = 0

    for log_file in sorted(audit_logs):
        rel_path = log_file.relative_to(base_dir)
        print(f"验证: {rel_path}")

        is_valid, errors = validate_jsonl_format(log_file)

        if is_valid:
            # 统计条目数
            with open(log_file, 'r', encoding='utf-8') as f:
                entry_count = sum(1 for line in f if line.strip())
            print(f"  [OK] Format valid ({entry_count} entries)")
            total_valid += 1
        else:
            print(f"  [FAIL] Format errors:")
            for error in errors[:5]:  # 最多显示 5 个错误
                print(f"    - {error}")
            if len(errors) > 5:
                print(f"    ... {len(errors) - 5} more errors")
            total_invalid += 1
        print()

    print(f"\n验证结果: {total_valid} 个文件通过，{total_invalid} 个文件失败")

    return 0 if total_invalid == 0 else 1

if __name__ == "__main__":
    sys.exit(main())
