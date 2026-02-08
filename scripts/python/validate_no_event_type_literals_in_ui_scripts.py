#!/usr/bin/env python3
"""
Hard gate: prevent event-type string literals from drifting in Godot UI scripts.

Rule:
- Under Game.Godot/Scripts/**/*.cs, literals starting with
  `core.`, `ui.menu.`, or `screen.` are forbidden unless they are declared as
  `const string` in the same line.

Rationale:
- Event types must come from contract constants in Game.Core/Contracts/**
  (ADR-0004 aligned), so renames are safe and deterministic.

Output:
- logs/ci/<YYYY-MM-DD>/ui-event-type-literals/report.json

Exit code:
- 0: no violations
- 1: violations found
"""

from __future__ import annotations

import datetime as dt
import io
import json
import re
from pathlib import Path
from typing import Dict, List


ROOT = Path(__file__).resolve().parents[2]
TARGET_DIR = ROOT / "Game.Godot" / "Scripts"
EVENT_PREFIXES = ("core.", "ui.menu.", "screen.")
STRING_LITERAL_RE = re.compile(r'"(?:\\.|[^"\\])*"')


def iter_cs_files() -> List[Path]:
    if not TARGET_DIR.exists():
        return []
    return sorted(TARGET_DIR.rglob("*.cs"))


def is_allowed_line(line: str) -> bool:
    return "const string" in line


def scan_file(path: Path) -> List[Dict[str, object]]:
    violations: List[Dict[str, object]] = []
    text = path.read_text(encoding="utf-8", errors="ignore")
    lines = text.splitlines()
    for lineno, line in enumerate(lines, start=1):
        stripped = line.strip()
        if not stripped or stripped.startswith("//"):
            continue
        if is_allowed_line(line):
            continue

        for match in STRING_LITERAL_RE.finditer(line):
            literal = match.group(0)[1:-1]
            if literal.startswith(EVENT_PREFIXES):
                violations.append(
                    {
                        "file": str(path.relative_to(ROOT)).replace("\\", "/"),
                        "line": lineno,
                        "column": match.start() + 1,
                        "literal": literal,
                        "rule": "use contract constants instead of event-type string literals",
                    }
                )
    return violations


def write_report(report: Dict[str, object]) -> Path:
    date = dt.date.today().strftime("%Y-%m-%d")
    out_dir = ROOT / "logs" / "ci" / date / "ui-event-type-literals"
    out_dir.mkdir(parents=True, exist_ok=True)
    out_path = out_dir / "report.json"
    with io.open(out_path, "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=2)
    return out_path


def main() -> int:
    files = iter_cs_files()
    violations: List[Dict[str, object]] = []
    for file_path in files:
        violations.extend(scan_file(file_path))

    report: Dict[str, object] = {
        "ok": len(violations) == 0,
        "root": str(ROOT),
        "target_dir": str(TARGET_DIR),
        "files_scanned": len(files),
        "violations_count": len(violations),
        "violations": violations,
        "allowed": ["const string declarations in the same line"],
        "prefixes": list(EVENT_PREFIXES),
    }

    out_path = write_report(report)
    print(json.dumps({"ok": report["ok"], "out": str(out_path), "violations": len(violations)}, ensure_ascii=False))
    return 0 if report["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())

