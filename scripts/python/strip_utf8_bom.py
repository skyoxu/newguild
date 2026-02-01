#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Strip UTF-8 BOM from text files.

Motivation:
Some tools (including git diff/PR text renderers) display BOM as mojibake
like "锘縟" when interpreted with a non-UTF8 default code page.

This script rewrites files in-place (UTF-8, no BOM) and writes an evidence
report under logs/ci/<YYYY-MM-DD>/encoding/.
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[2]
UTF8_BOM = b"\xef\xbb\xbf"


def _today() -> str:
    return dt.date.today().strftime("%Y-%m-%d")


def _write_json(path: Path, obj: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("paths", nargs="+", help="Files to rewrite if they start with UTF-8 BOM.")
    args = ap.parse_args()

    changed: list[str] = []
    skipped: list[str] = []

    for raw in args.paths:
        p = (REPO_ROOT / raw).resolve() if not Path(raw).is_absolute() else Path(raw)
        if not p.exists():
            skipped.append(f"{raw} (missing)")
            continue
        b = p.read_bytes()
        if not b.startswith(UTF8_BOM):
            skipped.append(raw)
            continue
        p.write_bytes(b[len(UTF8_BOM) :])
        changed.append(raw)

    out_dir = REPO_ROOT / "logs" / "ci" / _today() / "encoding"
    _write_json(
        out_dir / "strip-utf8-bom.json",
        {"date": _today(), "changed": changed, "skipped": skipped},
    )

    print(f"changed={len(changed)} skipped={len(skipped)} out={out_dir/'strip-utf8-bom.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

