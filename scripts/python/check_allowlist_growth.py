"""Guard against allowlist growth.

Fail the check when allowlist entries increase compared to a base ref.
This enforces a shrink-only policy for legacy allowlists.
"""

from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description='Check allowlist growth against a base ref.')
    parser.add_argument('--base-ref', required=True, help='Git base ref, e.g. origin/main or commit SHA.')
    parser.add_argument(
        '--file',
        action='append',
        dest='files',
        default=[],
        help='Allowlist file path (repeatable).',
    )
    parser.add_argument(
        '--allow-bootstrap',
        action='store_true',
        help='Allow first-introduced allowlist files (missing in base) without failing.',
    )
    return parser.parse_args()


def count_entries_from_text(content: str) -> int:
    return sum(1 for line in content.splitlines() if line.strip() and not line.lstrip().startswith('#'))


def read_current(path: Path) -> tuple[int, str]:
    if not path.exists():
        return 0, 'missing'
    return count_entries_from_text(path.read_text(encoding='utf-8', errors='replace')), 'ok'


def read_base(path: str, base_ref: str) -> tuple[int, str]:
    command = ['git', 'show', f'{base_ref}:{path.replace("\\", "/")}']
    process = subprocess.run(
        command,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding='utf-8',
        errors='replace',
    )
    if process.returncode != 0:
        return 0, 'missing_in_base'
    return count_entries_from_text(process.stdout), 'ok'


def main() -> int:
    args = parse_args()
    targets = args.files or [
        'scripts/python/check_test_naming.allowlist.txt',
        'scripts/python/check_test_naming.strict.allowlist.txt',
    ]

    print(f'ALLOWLIST_GROWTH base_ref={args.base_ref}')
    violations: list[str] = []

    for rel in targets:
        current_count, current_state = read_current(Path(rel))
        base_count, base_state = read_base(rel, args.base_ref)

        print(
            f' - {rel}: base={base_count} ({base_state}) current={current_count} ({current_state}) '
            f'delta={current_count - base_count}'
        )

        if base_state == 'missing_in_base':
            if args.allow_bootstrap:
                continue

            if current_count > 0:
                violations.append(
                    f'{rel}: file missing in base, bootstrap requires explicit --allow-bootstrap '
                    f'(current entries={current_count})'
                )
            continue

        if current_count > base_count:
            violations.append(
                f'{rel}: allowlist grew from {base_count} to {current_count} (+{current_count - base_count})'
            )

    if violations:
        print('ALLOWLIST_GROWTH status=fail')
        for violation in violations:
            print(f'  * {violation}')
        return 1

    print('ALLOWLIST_GROWTH status=ok')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
