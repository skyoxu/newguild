#!/usr/bin/env python3
"""Validate security-audit.jsonl format and field integrity

Validates JSONL audit logs according to ADR-0019 Security Baseline:
- Format: One JSON object per line (no arrays)
- Required fields: {ts, action, reason, target, caller}
- ISO 8601 timestamp format
- ADR-0004 action naming: domain.entity.verb
- Optional sensitive data detection
- Configurable strictness and reporting
"""

import argparse
import json
import re
import sys
from datetime import datetime
from pathlib import Path
from typing import List, Dict, Any, Optional


# Validation patterns
ACTION_PATTERN = re.compile(r'^[a-z][a-z0-9_]*\.[a-z][a-z0-9_]*\.[a-z][a-z0-9_]*$')
ISO8601_PATTERN = re.compile(
    r'^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$'
)

# Sensitive data patterns (detect unmasked secrets)
# Note: Excludes already sanitized forms (***、[REDACTED])
SENSITIVE_PATTERNS = [
    (r'password', re.compile(
        r'(?<![*\[])(password["\s:=]+)(?!(\*+|\[REDACTED\]))([^\s,}"\']+)',
        re.IGNORECASE
    )),
    (r'token', re.compile(
        r'(?<![*\[])(token["\s:=]+)(?!(\*+|\[REDACTED\]))([^\s,}"\']+)',
        re.IGNORECASE
    )),
    (r'api[-_]?key', re.compile(
        r'(?<![*\[])(api[-_]?key["\s:=]+)(?!(\*+|\[REDACTED\]))([^\s,}"\']+)',
        re.IGNORECASE
    )),
    (r'secret', re.compile(
        r'(?<![*\[])(secret["\s:=]+)(?!(\*+|\[REDACTED\]))([^\s,}"\']+)',
        re.IGNORECASE
    )),
    (r'credential', re.compile(
        r'(?<![*\[])(credential["\s:=]+)(?!(\*+|\[REDACTED\]))([^\s,}"\']+)',
        re.IGNORECASE
    )),
]


class ValidationError:
    """Structured validation error with severity"""

    def __init__(self, line_num: int, field: str, message: str, severity: str = 'error'):
        self.line_num = line_num
        self.field = field
        self.message = message
        self.severity = severity  # 'error' or 'warning'

    def __repr__(self):
        color = '\033[91m' if self.severity == 'error' else '\033[93m'
        reset = '\033[0m'
        severity_label = 'ERROR' if self.severity == 'error' else 'WARN'
        return f"{color}Line {self.line_num} [{severity_label}] {self.field}: {self.message}{reset}"


class AuditLogValidator:
    """JSONL audit log validator with comprehensive checks"""

    REQUIRED_FIELDS = {'ts', 'action', 'reason', 'target', 'caller'}
    MAX_REASON_LENGTH = 500
    MAX_TARGET_LENGTH = 1000

    def __init__(self, strict_mode: bool = False, check_sensitive: bool = False):
        self.strict_mode = strict_mode
        self.check_sensitive = check_sensitive
        self.errors: List[ValidationError] = []
        self.warnings: List[ValidationError] = []
        self.total_lines = 0
        self.valid_entries = 0

    def validate_file(self, file_path: Path) -> bool:
        """Validate a single JSONL file, return True if valid"""
        if not file_path.exists():
            self.errors.append(ValidationError(0, 'file', f'File not found: {file_path}', 'error'))
            return False

        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                for line_num, line in enumerate(f, start=1):
                    self.total_lines = line_num
                    line = line.strip()

                    if not line:  # Skip empty lines
                        continue

                    if self._validate_line(line, line_num):
                        self.valid_entries += 1

        except Exception as e:
            self.errors.append(ValidationError(0, 'file', f'Failed to read file: {e}', 'error'))
            return False

        # Check if file was completely empty
        if self.total_lines == 0:
            self.errors.append(ValidationError(0, 'file', 'Audit log file is empty', 'error'))
            return False

        # In strict mode, warnings count as failures
        if self.strict_mode:
            return len(self.errors) == 0 and len(self.warnings) == 0
        else:
            return len(self.errors) == 0

    def _validate_line(self, line: str, line_num: int) -> bool:
        """Validate a single line, return True if valid"""
        # Parse JSON
        try:
            entry = json.loads(line)
        except json.JSONDecodeError as e:
            self.errors.append(ValidationError(line_num, 'format', f'Invalid JSON: {e}', 'error'))
            return False

        # Must be an object, not array
        if not isinstance(entry, dict):
            self.errors.append(ValidationError(
                line_num, 'format', f'Expected JSON object, got {type(entry).__name__}', 'error'
            ))
            return False

        # Check required fields
        missing_fields = self.REQUIRED_FIELDS - set(entry.keys())
        if missing_fields:
            self.errors.append(ValidationError(
                line_num, 'fields', f'Missing required fields: {missing_fields}', 'error'
            ))
            return False

        # Validate individual fields
        is_valid = True
        is_valid &= self._validate_timestamp(entry.get('ts'), line_num)
        is_valid &= self._validate_action(entry.get('action'), line_num)
        is_valid &= self._validate_reason(entry.get('reason'), line_num)
        is_valid &= self._validate_target(entry.get('target'), line_num)
        is_valid &= self._validate_caller(entry.get('caller'), line_num)

        # Optional: Check for sensitive data leakage
        if self.check_sensitive:
            self._check_sensitive_data(line, line_num)

        return is_valid

    def _validate_timestamp(self, ts: Any, line_num: int) -> bool:
        """Validate ISO 8601 timestamp format"""
        if not isinstance(ts, str):
            self.errors.append(ValidationError(
                line_num, 'ts', f'Expected string, got {type(ts).__name__}', 'error'
            ))
            return False

        if not ISO8601_PATTERN.match(ts):
            self.errors.append(ValidationError(
                line_num, 'ts', f'Invalid ISO 8601 format: {ts}', 'error'
            ))
            return False

        # Parse and check for future timestamps (warning only)
        try:
            parsed = datetime.fromisoformat(ts.replace('Z', '+00:00'))
            if parsed > datetime.now(parsed.tzinfo):
                self.warnings.append(ValidationError(
                    line_num, 'ts', f'Future timestamp detected: {ts}', 'warning'
                ))
        except ValueError as e:
            self.errors.append(ValidationError(
                line_num, 'ts', f'Failed to parse timestamp: {e}', 'error'
            ))
            return False

        return True

    def _validate_action(self, action: Any, line_num: int) -> bool:
        """Validate ADR-0004 action naming: domain.entity.verb"""
        if not isinstance(action, str):
            self.errors.append(ValidationError(
                line_num, 'action', f'Expected string, got {type(action).__name__}', 'error'
            ))
            return False

        if not ACTION_PATTERN.match(action):
            self.errors.append(ValidationError(
                line_num, 'action',
                f'Invalid action naming (expected domain.entity.verb): {action}', 'error'
            ))
            return False

        return True

    def _validate_reason(self, reason: Any, line_num: int) -> bool:
        """Validate reason field (non-empty string, max length)"""
        if not isinstance(reason, str):
            self.errors.append(ValidationError(
                line_num, 'reason', f'Expected string, got {type(reason).__name__}', 'error'
            ))
            return False

        if not reason.strip():
            self.errors.append(ValidationError(
                line_num, 'reason', 'Reason cannot be empty', 'error'
            ))
            return False

        if len(reason) > self.MAX_REASON_LENGTH:
            self.errors.append(ValidationError(
                line_num, 'reason',
                f'Reason too long ({len(reason)} > {self.MAX_REASON_LENGTH} chars)', 'error'
            ))
            return False

        return True

    def _validate_target(self, target: Any, line_num: int) -> bool:
        """Validate target field (non-empty string, max length)"""
        if not isinstance(target, str):
            self.errors.append(ValidationError(
                line_num, 'target', f'Expected string, got {type(target).__name__}', 'error'
            ))
            return False

        if not target.strip():
            self.errors.append(ValidationError(
                line_num, 'target', 'Target cannot be empty', 'error'
            ))
            return False

        if len(target) > self.MAX_TARGET_LENGTH:
            self.errors.append(ValidationError(
                line_num, 'target',
                f'Target too long ({len(target)} > {self.MAX_TARGET_LENGTH} chars)', 'error'
            ))
            return False

        return True

    def _validate_caller(self, caller: Any, line_num: int) -> bool:
        """Validate caller field (non-empty string)"""
        if not isinstance(caller, str):
            self.errors.append(ValidationError(
                line_num, 'caller', f'Expected string, got {type(caller).__name__}', 'error'
            ))
            return False

        if not caller.strip():
            self.errors.append(ValidationError(
                line_num, 'caller', 'Caller cannot be empty', 'error'
            ))
            return False

        return True

    def _check_sensitive_data(self, line: str, line_num: int):
        """Check for unmasked sensitive data patterns"""
        for pattern_name, pattern_regex in SENSITIVE_PATTERNS:
            matches = pattern_regex.findall(line)
            if matches:
                self.warnings.append(ValidationError(
                    line_num, 'sensitive',
                    f'Potential {pattern_name} leakage detected (not sanitized)', 'warning'
                ))

    def get_report(self) -> Dict[str, Any]:
        """Generate JSON-serializable validation report"""
        return {
            'valid': len(self.errors) == 0 and (not self.strict_mode or len(self.warnings) == 0),
            'total_lines': self.total_lines,
            'valid_entries': self.valid_entries,
            'error_count': len(self.errors),
            'warning_count': len(self.warnings),
            'errors': [
                {
                    'line': err.line_num,
                    'field': err.field,
                    'message': err.message,
                    'severity': err.severity
                }
                for err in (self.errors + self.warnings)
            ],
            'strict_mode': self.strict_mode,
            'check_sensitive': self.check_sensitive
        }

    def print_report(self):
        """Print colored console report"""
        print(f"\n{'='*70}")
        print(f"Validation Report")
        print(f"{'='*70}")
        print(f"Total lines: {self.total_lines}")
        print(f"Valid entries: {self.valid_entries}")
        print(f"Errors: \033[91m{len(self.errors)}\033[0m")
        print(f"Warnings: \033[93m{len(self.warnings)}\033[0m")
        print(f"Strict mode: {'ON' if self.strict_mode else 'OFF'}")
        print(f"Sensitive check: {'ON' if self.check_sensitive else 'OFF'}")
        print(f"{'='*70}\n")

        if self.errors:
            print("\033[91mErrors:\033[0m")
            for err in self.errors:
                print(f"  {err}")
            print()

        if self.warnings:
            print("\033[93mWarnings:\033[0m")
            for warn in self.warnings:
                print(f"  {warn}")
            print()


def find_audit_logs(pattern: str, base_dir: Path = Path('.')) -> List[Path]:
    """Find audit log files matching pattern (supports wildcards)"""
    if '*' in pattern or '?' in pattern:
        # Wildcard pattern
        return list(base_dir.glob(pattern))
    else:
        # Single file path
        file_path = Path(pattern) if Path(pattern).is_absolute() else base_dir / pattern
        return [file_path] if file_path.exists() else []


def main():
    try:
        sys.stdout.reconfigure(encoding='utf-8')
        sys.stderr.reconfigure(encoding='utf-8')
    except Exception:
        pass

    parser = argparse.ArgumentParser(
        description='Validate security audit JSONL logs',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  %(prog)s --log-path logs/ci/2025-12-13/security-audit.jsonl
  %(prog)s --log-path "logs/ci/**/security-audit.jsonl" --strict
  %(prog)s --log-path logs/ci/latest.jsonl --report validation-report.json
  %(prog)s --log-path logs/ci/latest.jsonl --check-sensitive --strict
        """
    )

    parser.add_argument(
        '--log-path',
        required=True,
        help='Path to audit log file (supports wildcards like logs/ci/**/security-audit.jsonl)'
    )
    parser.add_argument(
        '--strict',
        action='store_true',
        help='Treat warnings as errors (fail validation on warnings)'
    )
    parser.add_argument(
        '--report',
        metavar='FILE',
        help='Output JSON validation report to file'
    )
    parser.add_argument(
        '--check-sensitive',
        action='store_true',
        help='Enable sensitive data detection (passwords, tokens, etc.)'
    )

    args = parser.parse_args()

    # Find log files
    base_dir = Path(__file__).parent.parent.parent  # Project root
    log_files = find_audit_logs(args.log_path, base_dir)

    if not log_files:
        print(f"Error: No audit log files found matching pattern: {args.log_path}")
        return 2

    print(f"Found {len(log_files)} audit log file(s) to validate\n")

    # Validate all files
    all_valid = True
    all_reports = []

    for log_file in sorted(log_files):
        rel_path = log_file.relative_to(base_dir) if log_file.is_relative_to(base_dir) else log_file
        print(f"Validating: {rel_path}")

        validator = AuditLogValidator(strict_mode=args.strict, check_sensitive=args.check_sensitive)
        is_valid = validator.validate_file(log_file)

        if is_valid:
            print(f"  PASS ({validator.valid_entries} entries)")
        else:
            print("  FAIL")
            all_valid = False

        validator.print_report()
        all_reports.append({
            'file': str(rel_path),
            'report': validator.get_report()
        })

    # Save JSON report if requested
    if args.report:
        report_path = Path(args.report)
        report_path.parent.mkdir(parents=True, exist_ok=True)

        with open(report_path, 'w', encoding='utf-8') as f:
            json.dump({
                'files': all_reports,
                'summary': {
                    'total_files': len(log_files),
                    'valid_files': sum(1 for r in all_reports if r['report']['valid']),
                    'failed_files': sum(1 for r in all_reports if not r['report']['valid']),
                    'total_errors': sum(r['report']['error_count'] for r in all_reports),
                    'total_warnings': sum(r['report']['warning_count'] for r in all_reports),
                }
            }, f, indent=2, ensure_ascii=False)

        print(f"\nValidation report saved to: {report_path}")

    # Final summary
    print(f"\n{'='*70}")
    if all_valid:
        print("ALL FILES PASSED VALIDATION")
    else:
        print("VALIDATION FAILED")
    print(f"{'='*70}\n")

    return 0 if all_valid else 1


if __name__ == "__main__":
    sys.exit(main())
