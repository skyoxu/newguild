#!/usr/bin/env python3
"""
Remove emoji/dingbat symbols from documentation files.

Repository rule: emojis are not allowed. This script helps enforce that rule
for docs, without requiring manual edits.

It operates in UTF-8 and writes an audit report under logs/ci/<YYYY-MM-DD>/.

Usage (Windows):
  py -3 scripts/python/sanitize_docs_no_emoji.py --root docs
  py -3 scripts/python/sanitize_docs_no_emoji.py --root docs --extra README.md AGENTS.md CLAUDE.md --write
"""

from __future__ import annotations

import argparse
import datetime as dt
import io
import json
from pathlib import Path
from typing import Iterable, List


TEXT_EXTS = {".md", ".txt", ".yml", ".yaml", ".json"}

# Replace a few common symbols with ASCII tags.
REPLACE = {
    0x2705: "[PASS]",  # white heavy check mark
    0x274C: "[FAIL]",  # cross mark
    0x26A0: "[WARN]",  # warning sign
    0x2713: "[OK]",  # check mark
}

# Drop ranges for emoji-ish code points.
RANGES = [
    (0x1F000, 0x1FAFF),
    (0x2600, 0x27BF),
]

DROP = {0xFE0F, 0x20E3}  # variation selector-16, combining keycap


def _is_emojiish(cp: int) -> bool:
    if cp in DROP:
        return True
    if cp in REPLACE:
        return True
    return any(a <= cp <= b for a, b in RANGES)


def _sanitize(text: str) -> tuple[str, int]:
    out: List[str] = []
    removed = 0
    for ch in text:
        cp = ord(ch)
        if cp in DROP:
            removed += 1
            continue
        if cp in REPLACE:
            out.append(REPLACE[cp])
            removed += 1
            continue
        if any(a <= cp <= b for a, b in RANGES):
            removed += 1
            continue
        out.append(ch)
    return "".join(out), removed


def _iter_files(root: Path) -> Iterable[Path]:
    for p in root.rglob("*"):
        if p.is_file() and p.suffix.lower() in TEXT_EXTS and not p.name.startswith("ZZZ-encoding-fixture-"):
            yield p


def _ensure_dir(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default="docs", help="Root directory to sanitize (default: docs)")
    ap.add_argument("--extra", nargs="*", default=[], help="Extra file paths to include")
    ap.add_argument("--write", action="store_true", help="Write changes back to disk")
    args = ap.parse_args()

    date = dt.date.today().strftime("%Y-%m-%d")
    out_dir = Path("logs") / "ci" / date
    _ensure_dir(out_dir)
    report_path = out_dir / "emoji-sanitize.json"

    root = Path(args.root)
    targets = list(_iter_files(root))
    for extra in args.extra:
        p = Path(extra)
        if p.is_file():
            targets.append(p)

    results: list[dict] = []
    total_removed = 0
    changed_files = 0
    for p in sorted(set(targets), key=lambda x: x.as_posix()):
        rel = p.as_posix()
        try:
            original = p.read_text(encoding="utf-8", errors="strict")
        except Exception as e:
            results.append({"file": rel, "error": f"{type(e).__name__}: {e}", "removed": 0, "changed": False})
            continue
        sanitized, removed = _sanitize(original)
        total_removed += removed
        changed = sanitized != original
        if changed:
            changed_files += 1
        results.append({"file": rel, "error": None, "removed": int(removed), "changed": bool(changed)})
        if changed and args.write:
            p.write_text(sanitized, encoding="utf-8", newline="\n")

    summary = {
        "ts": dt.datetime.now().isoformat(),
        "root": root.as_posix(),
        "extra": [str(x) for x in args.extra],
        "write": bool(args.write),
        "files_scanned": len(results),
        "files_changed": int(changed_files),
        "total_removed": int(total_removed),
        "results": results,
    }
    with io.open(report_path, "w", encoding="utf-8") as f:
        json.dump(summary, f, ensure_ascii=False, indent=2)

    print(f"[emoji_sanitize] files={len(results)} changed={changed_files} removed={total_removed} out={report_path.as_posix()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

