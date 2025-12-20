#!/usr/bin/env python3
"""Report Cobertura coverage for security-critical core files.

Writes a short UTF-8 report under logs/ci/<YYYY-MM-DD>/coverage-analysis/.
This script is read-only and does not modify build outputs.
"""

from __future__ import annotations

import datetime as _dt
import xml.etree.ElementTree as ET
from pathlib import Path


def _utc_date() -> str:
    return _dt.datetime.now(_dt.UTC).strftime("%Y-%m-%d")


def _find_latest_cobertura() -> Path:
    candidates = list(Path("Game.Core.Tests/TestResults").rglob("coverage.cobertura.xml"))
    if not candidates:
        raise SystemExit("No cobertura files found under Game.Core.Tests/TestResults/**/coverage.cobertura.xml")
    return max(candidates, key=lambda p: p.stat().st_mtime)


def _norm_path(p: str) -> str:
    return p.replace("\\\\", "/").replace("\\", "/")


def main() -> int:
    cobertura = _find_latest_cobertura()
    tree = ET.parse(cobertura)
    root = tree.getroot()

    # Cobertura filenames are typically project-relative (e.g. "Services/Foo.cs").
    targets = {
        "Services/DatabaseErrorHandling.cs",
        "Services/SensitiveDetailsPolicy.cs",
        "Game.Core/Services/DatabaseErrorHandling.cs",
        "Game.Core/Services/SensitiveDetailsPolicy.cs",
    }

    rows: list[str] = [f"cobertura={cobertura}"]
    found = 0
    for cls in root.findall(".//class"):
        filename = _norm_path(cls.get("filename") or "")
        if not any(filename.endswith(t) for t in targets):
            continue

        found += 1
        line_rate = float(cls.get("line-rate") or 0.0) * 100.0
        branch_rate = float(cls.get("branch-rate") or 0.0) * 100.0
        rows.append(f"{filename} line_rate={line_rate:.2f}% branch_rate={branch_rate:.2f}%")

    if found == 0:
        rows.append("No matching class entries found in cobertura report.")

    out_dir = Path("logs/ci") / _utc_date() / "coverage-analysis"
    out_dir.mkdir(parents=True, exist_ok=True)
    out_path = out_dir / "database-error-handling-coverage.txt"
    out_path.write_text("\n".join(rows) + "\n", encoding="utf-8")

    print(f"[REPORT] {out_path}")
    print("\n".join(rows))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
