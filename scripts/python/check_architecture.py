#!/usr/bin/env python3
"""
Architecture Compliance CI Check Script

Validates three-layer architecture compliance per ADR-0018:
1. Game.Core/ MUST NOT reference Godot namespace
2. Interfaces MUST be in Game.Core/Ports/ directory
3. Adapters layer MUST be complete (all ports implemented)

Exit codes:
  0 - All checks passed
  1 - Architecture violations detected
"""

import os
import re
import sys
from pathlib import Path
from typing import List, Tuple

# Configuration
CORE_DIR = "Game.Core"
GODOT_DIR = "Game.Godot"
PORTS_DIR = os.path.join(CORE_DIR, "Ports")
ADAPTERS_DIR = os.path.join(GODOT_DIR, "Adapters")

# Forbidden patterns in Core layer
FORBIDDEN_IN_CORE = [
    r"using\s+Godot;",
    r"using\s+Godot\.",
    r":\s*Node\b",
    r":\s*Node2D\b",
    r":\s*Node3D\b",
    r":\s*Control\b",
    r"GD\.Print",
    r"FileAccess\.",
]


def find_cs_files(directory: str) -> List[Path]:
    """Find all .cs files in directory recursively."""
    path = Path(directory)
    if not path.exists():
        return []
    return list(path.rglob("*.cs"))


def check_core_godot_references() -> Tuple[bool, List[str]]:
    """
    Check 1: Verify Game.Core/ does NOT reference Godot namespace.
    Core layer must be pure C# with no Godot dependencies.
    """
    violations = []
    core_files = find_cs_files(CORE_DIR)

    for file in core_files:
        with open(file, 'r', encoding='utf-8') as f:
            content = f.read()
            for line_num, line in enumerate(content.split('\n'), 1):
                for pattern in FORBIDDEN_IN_CORE:
                    if re.search(pattern, line):
                        violations.append(
                            f"{file}:{line_num}: Forbidden Godot reference: {line.strip()}"
                        )

    passed = len(violations) == 0
    return passed, violations


def check_interfaces_in_ports() -> Tuple[bool, List[str]]:
    """
    Check 2: Verify all interfaces are in Game.Core/Ports/ directory.
    Per ADR-0018, Ports/ is the canonical location for all interface definitions.
    """
    violations = []
    core_files = find_cs_files(CORE_DIR)

    for file in core_files:
        # Skip if file is in Ports/ directory
        if str(PORTS_DIR) in str(file):
            continue

        with open(file, 'r', encoding='utf-8') as f:
            content = f.read()
            # Check for interface definitions (simple regex, may have false positives)
            if re.search(r'\binterface\s+I[A-Z]\w+', content):
                violations.append(
                    f"{file}: Interface definition found outside Ports/ directory"
                )

    passed = len(violations) == 0
    return passed, violations


def check_adapters_completeness() -> Tuple[bool, List[str]]:
    """
    Check 3: Verify Adapters layer exists and implements all ports.
    Ensures every interface in Ports/ has a corresponding implementation in Adapters/.
    """
    violations = []

    # Find all interfaces in Ports/
    ports_files = find_cs_files(PORTS_DIR)
    interfaces = []

    for file in ports_files:
        with open(file, 'r', encoding='utf-8') as f:
            content = f.read()
            # Extract interface names (e.g., "interface IResourceLoader")
            matches = re.findall(r'\binterface\s+(I[A-Z]\w+)', content)
            interfaces.extend(matches)

    # Find all adapter implementations
    adapter_files = find_cs_files(ADAPTERS_DIR)
    implementations = set()

    for file in adapter_files:
        with open(file, 'r', encoding='utf-8') as f:
            content = f.read()
            # Extract implemented interfaces (e.g., ": Node, IResourceLoader")
            matches = re.findall(r':\s*(?:[\w.]+,\s*)*(I[A-Z]\w+)', content)
            implementations.update(matches)

    # Check for missing implementations
    for interface in set(interfaces):
        if interface not in implementations:
            violations.append(
                f"Missing adapter implementation for {interface}"
            )

    # Check if Adapters/ directory exists
    if not Path(ADAPTERS_DIR).exists():
        violations.append(
            f"CRITICAL: Adapters directory not found at {ADAPTERS_DIR}"
        )

    passed = len(violations) == 0
    return passed, violations


def main() -> int:
    """Run all architecture compliance checks."""
    print("=" * 70)
    print("Architecture Compliance CI Check (ADR-0018)")
    print("=" * 70)
    print()

    all_passed = True
    all_violations = []

    # Check 1: Core layer purity
    print("[1/3] Checking Core layer for Godot references...")
    passed, violations = check_core_godot_references()
    if passed:
        print("  [OK] PASSED: Core layer is pure C# (no Godot dependencies)")
    else:
        print(f"  [FAIL] FAILED: Found {len(violations)} Godot references in Core layer")
        all_violations.extend(violations)
        all_passed = False
    print()

    # Check 2: Interface locations
    print("[2/3] Checking interface locations...")
    passed, violations = check_interfaces_in_ports()
    if passed:
        print("  [OK] PASSED: All interfaces in Ports/ directory")
    else:
        print(f"  [FAIL] FAILED: Found {len(violations)} interfaces outside Ports/")
        all_violations.extend(violations)
        all_passed = False
    print()

    # Check 3: Adapter completeness
    print("[3/3] Checking Adapters layer completeness...")
    passed, violations = check_adapters_completeness()
    if passed:
        print("  [OK] PASSED: All ports have adapter implementations")
    else:
        print(f"  [FAIL] FAILED: Found {len(violations)} missing implementations")
        all_violations.extend(violations)
        all_passed = False
    print()

    # Summary
    print("=" * 70)
    if all_passed:
        print("[OK] All architecture compliance checks PASSED")
        print("=" * 70)
        return 0
    else:
        print("[FAIL] Architecture compliance checks FAILED")
        print("=" * 70)
        print()
        print("Violations:")
        for violation in all_violations:
            print(f"  - {violation}")
        print()
        print("Please fix violations before merging.")
        return 1


if __name__ == "__main__":
    sys.exit(main())
