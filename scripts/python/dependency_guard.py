#!/usr/bin/env python3
# -*- coding: utf-8 -*-

from __future__ import annotations

import argparse
import datetime as _dt
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path
from typing import Any


RE_USING_GODOT = re.compile(r"^\s*using\s+Godot\s*;\s*$", re.MULTILINE)
# Detect actual Godot API usage, not plain-text mentions (e.g., docs/comments "zero Godot dependencies").
RE_GODOT_API = re.compile(r"\b(?:global::)?Godot\.", re.MULTILINE)


@dataclass(frozen=True)
class Violation:
    code: str
    message: str
    target: str


def _today_ymd() -> str:
    return _dt.datetime.now().strftime("%Y-%m-%d")


def _repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


def _load_xml(path: Path) -> ET.Element:
    try:
        return ET.parse(path).getroot()
    except ET.ParseError as ex:
        raise RuntimeError(f"Failed to parse xml: {path}") from ex


def _read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def _project_references(csproj_path: Path) -> list[str]:
    root = _load_xml(csproj_path)
    refs: list[str] = []
    for elem in root.findall(".//ProjectReference"):
        include = elem.attrib.get("Include") or ""
        include = include.strip()
        if not include:
            continue
        refs.append(include.replace("\\", "/"))
    return refs


def _project_sdk(csproj_path: Path) -> str:
    root = _load_xml(csproj_path)
    return (root.attrib.get("Sdk") or "").strip()


def _find_cs_files(root: Path) -> list[Path]:
    return [p for p in root.rglob("*.cs") if p.is_file()]


def _scan_game_core_for_godot(repo_root: Path) -> list[Violation]:
    violations: list[Violation] = []
    core_dir = repo_root / "Game.Core"
    if not core_dir.exists():
        return violations
    for cs in _find_cs_files(core_dir):
        text = _read_text(cs)
        if RE_USING_GODOT.search(text) or RE_GODOT_API.search(text):
            violations.append(
                Violation(
                    code="CORE_USES_GODOT",
                    message="Game.Core must not reference Godot APIs or namespaces.",
                    target=str(cs.relative_to(repo_root)).replace("\\", "/"),
                )
            )
    return violations


def _scan_csproj_direction(repo_root: Path) -> list[Violation]:
    violations: list[Violation] = []

    csproj_core = repo_root / "Game.Core" / "Game.Core.csproj"
    csproj_core_tests = repo_root / "Game.Core.Tests" / "Game.Core.Tests.csproj"
    csproj_godot = repo_root / "GodotGame.csproj"
    csproj_godot_tests = repo_root / "Tests.Godot" / "Tests.Godot.csproj"

    projects = {
        "Game.Core": csproj_core,
        "Game.Core.Tests": csproj_core_tests,
        "GodotGame": csproj_godot,
        "Tests.Godot": csproj_godot_tests,
    }

    for name, path in projects.items():
        if not path.exists():
            violations.append(
                Violation(
                    code="CSPROJ_MISSING",
                    message=f"Expected csproj is missing: {name}",
                    target=str(path.relative_to(repo_root)).replace("\\", "/"),
                )
            )

    if csproj_core.exists():
        sdk = _project_sdk(csproj_core)
        if "Godot.NET.Sdk" in sdk:
            violations.append(
                Violation(
                    code="CORE_USES_GODOT_SDK",
                    message="Game.Core csproj must not use Godot.NET.Sdk.",
                    target=str(csproj_core.relative_to(repo_root)).replace("\\", "/"),
                )
            )
        for include in _project_references(csproj_core):
            if "GodotGame.csproj" in include or "Tests.Godot" in include or "Game.Core.Tests" in include:
                violations.append(
                    Violation(
                        code="CORE_REFERENCE_DIRECTION",
                        message="Game.Core must not reference Godot app or tests projects.",
                        target=f"{csproj_core.relative_to(repo_root).as_posix()} -> {include}",
                    )
                )

    if csproj_core_tests.exists():
        sdk = _project_sdk(csproj_core_tests)
        if "Godot.NET.Sdk" in sdk:
            violations.append(
                Violation(
                    code="CORE_TESTS_USES_GODOT_SDK",
                    message="Game.Core.Tests csproj should not use Godot.NET.Sdk.",
                    target=str(csproj_core_tests.relative_to(repo_root)).replace("\\", "/"),
                )
            )
        for include in _project_references(csproj_core_tests):
            if "GodotGame.csproj" in include or "Tests.Godot" in include:
                violations.append(
                    Violation(
                        code="CORE_TESTS_REFERENCE_DIRECTION",
                        message="Game.Core.Tests must not reference Godot projects.",
                        target=f"{csproj_core_tests.relative_to(repo_root).as_posix()} -> {include}",
                    )
                )

    return violations


def run_guard(repo_root: Path) -> dict[str, Any]:
    violations: list[Violation] = []
    violations.extend(_scan_csproj_direction(repo_root))
    violations.extend(_scan_game_core_for_godot(repo_root))

    result = {
        "ts": _dt.datetime.utcnow().isoformat() + "Z",
        "repo_root": str(repo_root).replace("\\", "/"),
        "rules": [
            "Game.Core must not reference Godot APIs (no Godot namespace usage).",
            "Game.Core must not reference Godot app/test projects.",
            "Game.Core csproj must not use Godot.NET.Sdk.",
        ],
        "violations": [v.__dict__ for v in violations],
        "summary": {
            "violation_count": len(violations),
            "status": "ok" if len(violations) == 0 else "fail",
        },
    }
    return result


def write_outputs(result: dict[str, Any], out_dir: Path) -> tuple[Path, Path]:
    out_dir.mkdir(parents=True, exist_ok=True)
    json_path = out_dir / "dependency-guard.json"
    summary_path = out_dir / "dependency-guard-summary.txt"

    json_path.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    summary_lines = [
        f"status={result['summary']['status']}",
        f"violation_count={result['summary']['violation_count']}",
    ]
    for v in result.get("violations", []):
        summary_lines.append(f"{v.get('code')} target={v.get('target')} msg={v.get('message')}")
    summary_path.write_text("\n".join(summary_lines) + "\n", encoding="utf-8")
    return json_path, summary_path


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="Hard gate: dependency guard for newguild architecture.")
    parser.add_argument(
        "--out-dir",
        default="",
        help="Output directory. Default: logs/ci/<YYYY-MM-DD>/",
    )
    args = parser.parse_args(argv)

    repo_root = _repo_root()
    out_dir = Path(args.out_dir) if args.out_dir else (repo_root / "logs" / "ci" / _today_ymd())

    result = run_guard(repo_root)
    json_path, summary_path = write_outputs(result, out_dir)
    print(f"DEPENDENCY_GUARD status={result['summary']['status']} json={json_path} summary={summary_path}")
    return 0 if result["summary"]["status"] == "ok" else 2


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
