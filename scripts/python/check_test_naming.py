"""Test Naming Convention Validator.

This validator enforces naming conventions for:
1) xUnit tests in ``Game.Core.Tests``
2) GdUnit tests in ``Tests.Godot/tests``

Rules:
- C# xUnit methods:
  - PascalCase
  - PascalCase_With_Underscores (for readable scenario-style names)
- GdUnit methods:
  - test_snake_case (method name starts with ``test_``)

The script supports legacy CI invocations like:
    py -3 scripts/python/check_test_naming.py --task-id 44 --style strict

Unknown CLI arguments are ignored for backward compatibility.
"""

import argparse
import re
import sys
from pathlib import Path
from typing import Dict, List, Sequence, Tuple

Violation = Tuple[int, str, str]


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


def scan_csharp_test_files(test_dir: Path) -> Dict[Path, List[Violation]]:
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
        test_methods = extract_test_methods(test_file)
        file_violations: List[Violation] = []

        for line_num, method_name in test_methods:
            if not is_allowed_test_method_name(method_name):
                file_violations.append(
                    (
                        line_num,
                        method_name,
                        "not approved; expected PascalCase or PascalCase_With_Underscores",
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


def scan_gdunit_test_files(test_dir: Path) -> Dict[Path, List[Violation]]:
    """Scan GdUnit files and validate test function naming."""
    violations: Dict[Path, List[Violation]] = {}

    gd_files = sorted(test_dir.rglob('*.gd'))
    for gd_file in gd_files:
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


def parse_args(argv: Sequence[str]) -> argparse.Namespace:
    """Parse CLI arguments while keeping backward compatibility."""
    parser = argparse.ArgumentParser(description='Validate test naming conventions.')
    parser.add_argument('--task-id', default=None, help='Optional task id for CI compatibility.')
    parser.add_argument('--style', default='strict', help='Naming style mode (kept for compatibility).')
    args, _unknown = parser.parse_known_args(argv)
    return args


def main():
    """Main entry point for the script."""
    _args = parse_args(sys.argv[1:])

    # Determine project root (script is in scripts/python/)
    script_dir = Path(__file__).parent
    project_root = script_dir.parent.parent
    csharp_test_dir = project_root / 'Game.Core.Tests'
    gdunit_test_dir = project_root / 'Tests.Godot' / 'tests'

    if not csharp_test_dir.exists():
        print(f"Error: C# test directory not found: {csharp_test_dir}", file=sys.stderr)
        return 1

    print('Scanning test naming conventions...')
    print(f'C# test directory: {csharp_test_dir}')
    print(f'GdUnit test directory: {gdunit_test_dir}')
    print()

    csharp_violations = scan_csharp_test_files(csharp_test_dir)
    gdunit_violations: Dict[Path, List[Violation]] = {}

    if gdunit_test_dir.exists():
        gdunit_violations = scan_gdunit_test_files(gdunit_test_dir)

    violations = merge_violations(csharp_violations, gdunit_violations)

    if not violations:
        print('[OK] All test methods follow approved naming conventions')
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
    print('  - C# PascalCase: GivenNoState_WhenSaveGame_ThenThrowsInvalidOperationException')
    print('  - C# PascalCase_With_Underscores: SaveGame_WhenStateMissing_ShouldThrowInvalidOperationException')
    print('  - GdUnit test_snake_case: test_save_load_roundtrip_persists_state')

    return 1


if __name__ == '__main__':
    sys.exit(main())
