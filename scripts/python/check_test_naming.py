"""Test Naming Convention Validator.

This validator enforces naming conventions for:
1) xUnit tests in ``Game.Core.Tests``
2) GdUnit tests in ``Tests.Godot/tests``

Rules:
- C# xUnit methods:
  - PascalCase
  - PascalCase_With_Underscores (for readable scenario-style names)
  - Optional strict behavior style (Given_When_Then or Should_)
- GdUnit methods:
  - test_snake_case (method name starts with ``test_``)

Additional capabilities:
- Legacy allowlist support (file/method granularity)
- Changed-only mode for CI/PR incremental enforcement

The script supports legacy CI invocations like:
    py -3 scripts/python/check_test_naming.py --task-id 44 --style strict

Unknown CLI arguments are ignored for backward compatibility.
"""

import argparse
import fnmatch
import re
import subprocess
import sys
from pathlib import Path
from typing import Dict, List, Sequence, Tuple

Violation = Tuple[int, str, str]
AllowlistEntry = Tuple[str, str]


class GitCommandError(RuntimeError):
    """Raised when a git command required by changed-only mode fails."""


def is_pascal_case(name: str) -> bool:
    """
    Check if a method name follows PascalCase convention.

    PascalCase rules:
    - Starts with uppercase letter
    - No underscores
    - Can contain digits

    Args:
        name: Method name to check

    Returns:
        True if name is PascalCase, False otherwise
    """
    # PascalCase pattern: starts with uppercase, no underscores
    pattern = r'^[A-Z][a-zA-Z0-9]*$'
    return bool(re.match(pattern, name))


def is_pascal_case_with_underscores(name: str) -> bool:
    """
    Check if a method name follows the PascalCase_With_Underscores convention.

    Examples:
      - Save_load_delete_and_index_flow_works_with_compression  (NOT allowed: starts with lowercase)
      - Save_Load_Delete_And_Index_Flow_WorksWithCompression    (allowed)
      - Advance_WithinBounds_ReturnsCorrectPosition             (allowed)

    Rules:
      - Each segment is PascalCase (no underscores within segments)
      - Segments are separated by a single underscore
    """
    pattern = r'^[A-Z][a-zA-Z0-9]*(?:_[A-Z][a-zA-Z0-9]*)+$'
    return bool(re.match(pattern, name))


def is_allowed_test_method_name(name: str) -> bool:
    """
    Approved patterns:
      A) PascalCase (covers GivenWhenThen style)
      B) PascalCase_With_Underscores (Method_Scenario_ExpectedResult)
    """
    return is_pascal_case(name) or is_pascal_case_with_underscores(name)


def is_given_when_then_name(name: str) -> bool:
    """Return True if method name follows Given_When_Then style."""
    pattern = r'^Given[A-Za-z0-9]+_When[A-Za-z0-9]+_Then[A-Za-z0-9]+$'
    return bool(re.match(pattern, name))


def is_should_style_name(name: str) -> bool:
    """Return True if method name follows ShouldX_WhenY style."""
    pattern = r'^Should[A-Za-z0-9]+_When[A-Za-z0-9]+(?:_(?:Then|And)[A-Za-z0-9]+)*$'
    return bool(re.match(pattern, name))


def normalize_style_mode(style: str | None) -> str:
    """Normalize style argument while preserving backward compatibility.

    - legacy: existing permissive mode (PascalCase or PascalCase_With_Underscores)
    - gwt_should: strict behavior mode (Given_When_Then or Should_)
    """
    raw = (style or 'strict').strip().lower().replace('-', '_')
    if raw in {'strict'}:
        return 'strict_auto'
    if raw in {'legacy', 'pascal', 'compat'}:
        return 'legacy'
    if raw in {'should_when', 'shouldx_wheny', 'strict_should'}:
        return 'should_when'
    if raw in {'gwt_should', 'behavior', 'scenario', 'strict_behavior'}:
        return 'gwt_should'
    return 'legacy'


def is_allowed_test_method_name_by_style(name: str, style_mode: str) -> bool:
    """Validate test method name according to selected style mode."""
    if style_mode == 'should_when':
        return is_should_style_name(name)
    if style_mode == 'gwt_should':
        return is_given_when_then_name(name) or is_should_style_name(name)
    return is_allowed_test_method_name(name)


def parse_test_refs_from_details(details: str) -> List[str]:
    """Extract test refs from details text fallback when testRefs is absent."""
    refs: List[str] = []
    for line in details.splitlines():
        if not line.lower().startswith('test refs:'):
            continue
        payload = line.split(':', 1)[1]
        refs.extend([item.strip().replace('\\', '/') for item in payload.split(';') if item.strip()])
    return refs


def collect_task_test_ref_paths(project_root: Path, task_id: str) -> set[str]:
    """Collect task-scoped test refs from .taskmaster/tasks/tasks.json."""
    tasks_file = project_root / '.taskmaster' / 'tasks' / 'tasks.json'
    if not tasks_file.exists():
        return set()

    try:
        import json

        payload = json.loads(tasks_file.read_text(encoding='utf-8'))
        tasks = payload.get('master', {}).get('tasks', [])
        task = next((item for item in tasks if str(item.get('id')) == str(task_id)), None)
        if not isinstance(task, dict):
            return set()

        refs: List[str] = []
        test_refs = task.get('testRefs')
        if isinstance(test_refs, list):
            refs.extend([str(item).strip().replace('\\', '/') for item in test_refs if str(item).strip()])

        if not refs and isinstance(task.get('details'), str):
            refs.extend(parse_test_refs_from_details(task['details']))

        return {ref for ref in refs if ref}
    except Exception as exception:  # pragma: no cover - defensive
        print(f"[FAIL] failed to load task test refs: {exception}", file=sys.stderr)
        return set()


def run_git(project_root: Path, args: Sequence[str]) -> List[str]:
    """Run git command and return normalized path list.

    Raises GitCommandError when git is unavailable or command fails.
    """
    command = ['git', *args]
    try:
        output = subprocess.check_output(
            command,
            cwd=project_root,
            text=True,
            encoding='utf-8',
            stderr=subprocess.STDOUT,
        )
    except subprocess.CalledProcessError as exception:  # pragma: no cover - defensive
        stderr = (exception.output or '').strip()
        if len(stderr) > 400:
            stderr = stderr[:400] + '...'
        raise GitCommandError(
            f"git command failed: {' '.join(command)} (exit={exception.returncode})"
            + (f" output={stderr}" if stderr else '')
        ) from exception
    except FileNotFoundError as exception:  # pragma: no cover - defensive
        raise GitCommandError('git executable not found in PATH') from exception
    except Exception as exception:  # pragma: no cover - defensive
        raise GitCommandError(f'git command failed unexpectedly: {exception}') from exception

    return [line.strip().replace('\\', '/') for line in output.splitlines() if line.strip()]


def collect_changed_repo_paths(project_root: Path, base_ref: str | None) -> List[str]:
    """Collect changed paths for changed-only mode."""
    paths: set[str] = set()

    if base_ref:
        paths.update(run_git(project_root, ['diff', '--name-only', '--diff-filter=ACMRTUXB', f'{base_ref}...HEAD']))
    else:
        paths.update(run_git(project_root, ['diff', '--name-only', '--diff-filter=ACMRTUXB', '--cached']))
        paths.update(run_git(project_root, ['diff', '--name-only', '--diff-filter=ACMRTUXB']))
        paths.update(run_git(project_root, ['ls-files', '--others', '--exclude-standard']))

    return sorted(paths)


def collect_changed_test_paths(
    project_root: Path,
    csharp_test_dir: Path,
    gdunit_test_dir: Path,
    base_ref: str | None,
) -> set[str]:
    """Return changed test file paths (repo-relative POSIX style)."""
    changed_paths = collect_changed_repo_paths(project_root, base_ref)
    selected: set[str] = set()

    csharp_root = csharp_test_dir.resolve()
    gdunit_root = gdunit_test_dir.resolve()

    for rel in changed_paths:
        absolute = (project_root / rel).resolve()
        if not absolute.exists() or not absolute.is_file():
            continue

        if absolute.suffix.lower() == '.cs' and absolute.name.endswith('Tests.cs') and absolute.is_relative_to(csharp_root):
            selected.add(rel)
            continue

        if absolute.suffix.lower() == '.gd' and absolute.is_relative_to(gdunit_root):
            selected.add(rel)

    return selected


def extract_test_methods(file_path: Path) -> List[Tuple[int, str]]:
    """
    Extract test method names and their line numbers from a C# test file.

    Args:
        file_path: Path to the test file

    Returns:
        List of tuples (line_number, method_name)
    """
    test_methods = []

    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()

        # Look for [Fact] or [Theory] attributes followed by method definition
        for i, line in enumerate(lines, start=1):
            line = line.strip()

            # Check if this line has [Fact] or [Theory] attribute
            if line.startswith('[Fact]') or line.startswith('[Theory]'):
                # Next non-empty line should be the method definition
                for j in range(i, min(i + 5, len(lines) + 1)):  # Check next few lines
                    next_line = lines[j - 1].strip()
                    if not next_line or next_line.startswith('//') or next_line.startswith('['):
                        continue

                    # Extract method name from method signature
                    # Pattern: public void MethodName() or public async Task MethodName()
                    method_match = re.search(r'\b(?:public|private|internal)\s+(?:async\s+)?(?:void|Task(?:<[^>]+>)?)\s+(\w+)\s*\(', next_line)
                    if method_match:
                        method_name = method_match.group(1)
                        test_methods.append((j, method_name))
                        break

    except Exception as e:
        print(f"Error reading {file_path}: {e}", file=sys.stderr)

    return test_methods


def scan_csharp_test_files(
    test_dir: Path,
    project_root: Path,
    style_mode: str,
    target_rel_paths: set[str] | None = None,
) -> Dict[Path, List[Violation]]:
    """
    Scan all test files and find naming violations.

    Args:
        test_dir: Root directory containing test files

    Returns:
        Dictionary mapping file paths to list of violations (line_number, method_name)
    """
    violations: Dict[Path, List[Violation]] = {}

    # Find all *Tests.cs files
    test_files = list(test_dir.rglob('*Tests.cs'))

    for test_file in sorted(test_files):
        rel_path = test_file.relative_to(project_root).as_posix()
        if target_rel_paths is not None and rel_path not in target_rel_paths:
            continue

        test_methods = extract_test_methods(test_file)
        file_violations: List[Violation] = []

        for line_num, method_name in test_methods:
            if not is_allowed_test_method_name_by_style(method_name, style_mode):
                file_violations.append(
                    (
                        line_num,
                        method_name,
                        (
                            "not approved; expected ShouldX_WhenY style"
                            if style_mode == 'should_when'
                            else (
                                "not approved; expected Given_When_Then or ShouldX_WhenY style"
                                if style_mode == 'gwt_should'
                                else "not approved; expected PascalCase or PascalCase_With_Underscores"
                            )
                        ),
                    )
                )

        if file_violations:
            violations[test_file] = file_violations

    return violations


def extract_gdscript_functions(file_path: Path) -> List[Tuple[int, str]]:
    """Extract GDScript function names with line numbers."""
    functions: List[Tuple[int, str]] = []

    try:
        lines = file_path.read_text(encoding='utf-8').splitlines()
    except Exception as exception:  # pragma: no cover - defensive
        print(f"Error reading {file_path}: {exception}", file=sys.stderr)
        return functions

    func_pattern = re.compile(r'^\s*func\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(')
    for index, raw_line in enumerate(lines, start=1):
        match = func_pattern.match(raw_line)
        if match:
            functions.append((index, match.group(1)))

    return functions


def is_allowed_gdunit_test_name(name: str) -> bool:
    """Return True when GdUnit test function name follows test_snake_case."""
    return bool(re.match(r'^test_[a-z0-9_]+$', name))


def scan_gdunit_test_files(
    test_dir: Path,
    project_root: Path,
    target_rel_paths: set[str] | None = None,
) -> Dict[Path, List[Violation]]:
    """Scan GdUnit files and validate test function naming."""
    violations: Dict[Path, List[Violation]] = {}

    gd_files = sorted(test_dir.rglob('*.gd'))
    for gd_file in gd_files:
        rel_path = gd_file.relative_to(project_root).as_posix()
        if target_rel_paths is not None and rel_path not in target_rel_paths:
            continue

        functions = extract_gdscript_functions(gd_file)
        file_violations: List[Violation] = []

        for line_num, function_name in functions:
            # Only validate test functions here; helpers/lifecycle hooks are allowed.
            if not function_name.lower().startswith('test'):
                continue

            if not is_allowed_gdunit_test_name(function_name):
                file_violations.append(
                    (
                        line_num,
                        function_name,
                        "not approved for GdUnit; expected test_snake_case (test_*)",
                    )
                )

        if file_violations:
            violations[gd_file] = file_violations

    return violations


def merge_violations(*groups: Dict[Path, List[Violation]]) -> Dict[Path, List[Violation]]:
    """Merge multiple violation dictionaries into one."""
    merged: Dict[Path, List[Violation]] = {}
    for group in groups:
        for file_path, issues in group.items():
            merged.setdefault(file_path, []).extend(issues)
    return merged


def load_allowlist(file_path: Path) -> List[AllowlistEntry]:
    """Load allowlist entries from UTF-8 text file.

    Format:
      - "relative/path/to/file.cs::MethodName"
      - "relative/path/**/*.cs::Method*"
      - "relative/path/to/file.cs" (equivalent to "::*")
    """
    if not file_path.exists():
        return []

    entries: List[AllowlistEntry] = []
    try:
        for raw_line in file_path.read_text(encoding='utf-8').splitlines():
            line = raw_line.strip()
            if not line or line.startswith('#'):
                continue

            if '::' in line:
                path_pattern, method_pattern = line.split('::', 1)
            else:
                path_pattern, method_pattern = line, '*'

            path_pattern = path_pattern.strip().replace('\\', '/')
            method_pattern = method_pattern.strip() or '*'
            if path_pattern:
                entries.append((path_pattern, method_pattern))
    except Exception as exception:  # pragma: no cover - defensive
        print(f"Error loading allowlist {file_path}: {exception}", file=sys.stderr)
        return []

    return entries


def is_allowlisted(relative_path: str, method_name: str, entries: Sequence[AllowlistEntry]) -> bool:
    """Return True if file/method pair matches any allowlist entry."""
    for path_pattern, method_pattern in entries:
        if fnmatch.fnmatch(relative_path, path_pattern) and fnmatch.fnmatch(method_name, method_pattern):
            return True
    return False


def apply_allowlist(
    violations: Dict[Path, List[Violation]],
    project_root: Path,
    entries: Sequence[AllowlistEntry],
) -> Tuple[Dict[Path, List[Violation]], int]:
    """Filter violations by allowlist entries and return remaining violations with skipped count."""
    if not entries:
        return violations, 0

    filtered: Dict[Path, List[Violation]] = {}
    skipped = 0

    for file_path, items in violations.items():
        rel_path = file_path.relative_to(project_root).as_posix()
        keep: List[Violation] = []
        for line_num, method_name, reason in items:
            if is_allowlisted(rel_path, method_name, entries):
                skipped += 1
                continue
            keep.append((line_num, method_name, reason))

        if keep:
            filtered[file_path] = keep

    return filtered, skipped


def parse_args(argv: Sequence[str]) -> argparse.Namespace:
    """Parse CLI arguments while keeping backward compatibility."""
    parser = argparse.ArgumentParser(description='Validate test naming conventions.')
    parser.add_argument('--task-id', default=None, help='Optional task id for CI compatibility.')
    parser.add_argument(
        '--style',
        default='strict',
        help=(
            'Naming style mode: strict(auto task/changed ShouldX_WhenY), '
            'legacy(PascalCase), gwt_should(Given_When_Then or ShouldX_WhenY).'
        ),
    )
    parser.add_argument('--allowlist', default=None, help='Optional allowlist path for legacy tests.')
    parser.add_argument('--changed-only', action='store_true', help='Validate only changed test files.')
    parser.add_argument('--base-ref', default=None, help='Base git ref for changed-only mode (e.g. origin/main).')
    args, _unknown = parser.parse_known_args(argv)
    return args


def main():
    """Main entry point for the script."""
    args = parse_args(sys.argv[1:])

    # Determine project root (script is in scripts/python/)
    script_dir = Path(__file__).parent
    project_root = script_dir.parent.parent
    csharp_test_dir = project_root / 'Game.Core.Tests'
    gdunit_test_dir = project_root / 'Tests.Godot' / 'tests'
    style_mode = normalize_style_mode(args.style)

    if style_mode == 'strict_auto':
        if args.task_id or args.changed_only:
            style_mode = 'should_when'
        else:
            style_mode = 'legacy'

    if args.allowlist:
        allowlist_path = Path(args.allowlist)
    else:
        allowlist_path = (
            script_dir / 'check_test_naming.strict.allowlist.txt'
            if style_mode in {'gwt_should', 'should_when'}
            else script_dir / 'check_test_naming.allowlist.txt'
        )

    allowlist_entries = load_allowlist(allowlist_path)

    target_rel_paths: set[str] | None = None
    if args.changed_only:
        try:
            target_rel_paths = collect_changed_test_paths(
                project_root=project_root,
                csharp_test_dir=csharp_test_dir,
                gdunit_test_dir=gdunit_test_dir,
                base_ref=args.base_ref,
            )
        except GitCommandError as exception:
            print(f"[FAIL] changed-only git source failed: {exception}", file=sys.stderr)
            print('[FAIL] changed-only gate is fail-closed to avoid false green.', file=sys.stderr)
            return 2

    if style_mode == 'should_when' and args.task_id:
        task_ref_paths = collect_task_test_ref_paths(project_root, str(args.task_id))
        if not task_ref_paths:
            print(
                f"[FAIL] strict task naming requires non-empty testRefs for task-id={args.task_id}",
                file=sys.stderr,
            )
            print('[FAIL] add task.testRefs or pass --changed-only for incremental enforcement.', file=sys.stderr)
            return 2
        if target_rel_paths is None:
            target_rel_paths = task_ref_paths
        else:
            target_rel_paths = target_rel_paths.intersection(task_ref_paths)

    if not csharp_test_dir.exists():
        print(f"Error: C# test directory not found: {csharp_test_dir}", file=sys.stderr)
        return 1

    print('Scanning test naming conventions...')
    print(f'C# test directory: {csharp_test_dir}')
    print(f'GdUnit test directory: {gdunit_test_dir}')
    print(f'[INFO] Style mode: {style_mode}')
    if args.changed_only:
        base_text = args.base_ref if args.base_ref else 'worktree(index+unstaged+untracked)'
        print(f'[INFO] Changed-only mode enabled (base: {base_text})')
        print(f'[INFO] Selected changed test files: {len(target_rel_paths)}')
        if len(target_rel_paths) == 0:
            print('[INFO] No changed test files selected by changed-only filter.')
    if style_mode == 'should_when' and args.task_id:
        print(f'[INFO] Task-scoped strict naming enabled (task-id: {args.task_id})')
        print(f'[INFO] Selected task test refs: {len(target_rel_paths) if target_rel_paths is not None else 0}')
    print()

    csharp_violations = scan_csharp_test_files(
        csharp_test_dir,
        project_root=project_root,
        style_mode=style_mode,
        target_rel_paths=target_rel_paths,
    )
    gdunit_violations: Dict[Path, List[Violation]] = {}

    if gdunit_test_dir.exists():
        gdunit_violations = scan_gdunit_test_files(
            gdunit_test_dir,
            project_root=project_root,
            target_rel_paths=target_rel_paths,
        )

    violations = merge_violations(csharp_violations, gdunit_violations)
    violations, skipped_count = apply_allowlist(violations, project_root, allowlist_entries)

    if allowlist_entries:
        print(f'[INFO] Loaded naming allowlist entries: {len(allowlist_entries)} ({allowlist_path})')
        print(f'[INFO] Skipped violations by allowlist: {skipped_count}')
        print()

    if not violations:
        print('[OK] All test methods follow approved naming conventions')
        if style_mode == 'should_when':
            print('[OK] C# tests: ShouldX_WhenY style')
        elif style_mode == 'gwt_should':
            print('[OK] C# tests: Given_When_Then or ShouldX_WhenY style')
        else:
            print('[OK] C# tests: PascalCase or PascalCase_With_Underscores')
        print('[OK] GdUnit tests: test_snake_case (test_*)')
        print("[OK] No violations found")
        return 0

    # Report violations
    print("[FAIL] Test naming violations found:")
    print()

    total_violations = 0
    for file_path, file_violations in sorted(violations.items()):
        rel_path = file_path.relative_to(project_root)
        print(f"{rel_path}:")
        for line_num, method_name, reason in file_violations:
            print(f"  Line {line_num}: {method_name} ({reason})")
            total_violations += 1
        print()

    print(f"Total violations: {total_violations}")
    print()
    print('Fix these violations by renaming methods to an approved pattern:')
    if style_mode == 'should_when':
        print('  - C# ShouldX_WhenY: ShouldSaveGame_WhenStateMissing')
        print('  - C# Optional extension: ShouldSaveGame_WhenStateMissing_ThenThrowInvalidOperationException')
    elif style_mode == 'gwt_should':
        print('  - C# Given_When_Then: GivenNoState_WhenSaveGame_ThenThrowsInvalidOperationException')
        print('  - C# ShouldX_WhenY: ShouldSaveGame_WhenStateMissing')
    else:
        print('  - C# PascalCase: GivenNoState_WhenSaveGame_ThenThrowsInvalidOperationException')
        print('  - C# PascalCase_With_Underscores: SaveGame_WhenStateMissing_ShouldThrowInvalidOperationException')
    print('  - GdUnit test_snake_case: test_save_load_roundtrip_persists_state')
    if allowlist_entries:
        print(f'  - Legacy skip via allowlist: {allowlist_path}')

    return 1


if __name__ == '__main__':
    sys.exit(main())
