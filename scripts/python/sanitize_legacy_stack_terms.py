#!/usr/bin/env python3
"""
Neutralize legacy stack terms in docs to reduce misleading context.

This tool is designed for the repository's "doc stack convergence" workflow.
It reads and writes strictly as UTF-8.

Safety rules:
  - Only text-like files are processed.
  - Markdown code fences are preserved (not modified) to avoid breaking examples.
  - A report is always written under logs/ci/<YYYY-MM-DD>/legacy-term-sanitize/.

Usage (Windows):
  py -3 scripts/python/sanitize_legacy_stack_terms.py --root docs
  py -3 scripts/python/sanitize_legacy_stack_terms.py --root docs --write
"""

from __future__ import annotations

import argparse
import datetime as dt
import io
import json
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, List


TEXT_EXTS = {
    ".md",
    ".txt",
    ".yml",
    ".yaml",
    ".json",
    ".xml",
    ".ini",
    ".cfg",
    ".toml",
}

DEFAULT_MAP_PATH = Path("docs/workflows/legacy-stack-terms-map.json")


@dataclass(frozen=True)
class ReplacementSpec:
    name: str
    pattern: str
    replacement: str
    flags: str


def _compile(spec: ReplacementSpec) -> re.Pattern[str]:
    flags = 0
    if "i" in spec.flags.lower():
        flags |= re.IGNORECASE
    return re.compile(spec.pattern, flags)


def _is_text_file(path: Path) -> bool:
    if not path.is_file():
        return False
    if path.name.startswith("ZZZ-encoding-fixture-"):
        return False
    return path.suffix.lower() in TEXT_EXTS


def _iter_files(root: Path) -> Iterable[Path]:
    for p in root.rglob("*"):
        if _is_text_file(p):
            yield p


def _load_replacements(map_path: Path) -> List[ReplacementSpec]:
    data = json.loads(map_path.read_text(encoding="utf-8"))
    reps = data.get("replacements") or []
    out: List[ReplacementSpec] = []
    for idx, item in enumerate(reps):
        name = str(item.get("name") or item.get("pattern") or f"pattern_{idx}")
        pattern = str(item.get("pattern") or "")
        replacement = str(item.get("replacement") or "")
        flags = str(item.get("flags") or "")
        if not pattern or replacement is None:
            continue
        out.append(ReplacementSpec(name=name, pattern=pattern, replacement=replacement, flags=flags))
    return out


def _split_md_fences(text: str) -> list[tuple[str, str]]:
    """
    Split markdown into segments: ('text'|'code', content).
    Code segments include the surrounding ``` fences.
    """
    parts: list[tuple[str, str]] = []
    fence_re = re.compile(r"```.*?```", re.DOTALL)
    last = 0
    for m in fence_re.finditer(text):
        if m.start() > last:
            parts.append(("text", text[last : m.start()]))
        parts.append(("code", m.group(0)))
        last = m.end()
    if last < len(text):
        parts.append(("text", text[last:]))
    return parts


def _apply_replacements(text: str, compiled: list[tuple[ReplacementSpec, re.Pattern[str]]]) -> tuple[str, list[dict]]:
    changes: list[dict] = []
    out = text
    for spec, cre in compiled:
        out, n = cre.subn(spec.replacement, out)
        if n:
            changes.append({"name": spec.name, "count": int(n)})
    return out, changes


def _ensure_dir(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default="docs", help="Root directory to sanitize (default: docs)")
    ap.add_argument("--write", action="store_true", help="Write changes back to disk")
    ap.add_argument(
        "--map",
        dest="map_path",
        default=str(DEFAULT_MAP_PATH),
        help="Replacement map JSON (pattern -> replacement)",
    )
    args = ap.parse_args()

    root = Path(args.root)
    map_path = Path(args.map_path)
    if not root.exists():
        raise SystemExit(f"[legacy_sanitize] root not found: {root}")
    if not map_path.exists():
        raise SystemExit(f"[legacy_sanitize] map file not found: {map_path}")

    specs = _load_replacements(map_path)
    compiled = [(s, _compile(s)) for s in specs]

    date = dt.date.today().strftime("%Y-%m-%d")
    out_dir = Path("logs") / "ci" / date / "legacy-term-sanitize"
    _ensure_dir(out_dir)
    changes_path = out_dir / "changes.json"
    summary_path = out_dir / "summary.json"

    changes: list[dict] = []
    files_seen = 0
    files_changed = 0
    total_replacements = 0
    errors: list[dict] = []

    for path in _iter_files(root):
        rel = path.as_posix()
        try:
            original = path.read_text(encoding="utf-8", errors="strict")
        except Exception as e:
            errors.append({"file": rel, "error": f"{type(e).__name__}: {e}"})
            continue

        files_seen += 1
        payload = original
        file_changes: list[dict] = []

        if path.suffix.lower() == ".md":
            parts = _split_md_fences(payload)
            rebuilt: list[str] = []
            for kind, content in parts:
                if kind == "code":
                    rebuilt.append(content)
                    continue
                new_text, ch = _apply_replacements(content, compiled)
                file_changes.extend(ch)
                rebuilt.append(new_text)
            new_payload = "".join(rebuilt)
        else:
            new_payload, file_changes = _apply_replacements(payload, compiled)

        if not file_changes:
            continue

        files_changed += 1
        rep_count = sum(c["count"] for c in file_changes)
        total_replacements += rep_count
        changes.append({"file": rel, "replacements": file_changes, "total": int(rep_count)})

        if args.write:
            path.write_text(new_payload, encoding="utf-8", newline="\n")

    summary = {
        "ts": dt.datetime.now().isoformat(),
        "root": root.as_posix(),
        "map": map_path.as_posix(),
        "write": bool(args.write),
        "files_scanned": int(files_seen),
        "files_changed": int(files_changed),
        "total_replacements": int(total_replacements),
        "errors": errors,
    }

    with io.open(changes_path, "w", encoding="utf-8") as f:
        json.dump(changes, f, ensure_ascii=False, indent=2)
    with io.open(summary_path, "w", encoding="utf-8") as f:
        json.dump(summary, f, ensure_ascii=False, indent=2)

    print(
        f"[legacy_sanitize] files_scanned={files_seen} files_changed={files_changed} "
        f"replacements={total_replacements} write={args.write} out={out_dir.as_posix()}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

