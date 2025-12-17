#!/usr/bin/env python3
"""
Scan documentation files for legacy stack terms that can mislead readers/LLMs.

This tool is intended for "doc stack convergence" work (Base + Migration docs).
It produces a machine-readable report under logs/ci/<YYYY-MM-DD>/doc-stack-scan/.

Default behavior:
  - Scans text-like files under --root (default: docs)
  - Uses a replacement map file (default: docs/workflows/legacy-stack-terms-map.json)
    to obtain the list of patterns to detect
  - Excludes code fences in Markdown to avoid false positives in legacy snippets

Usage (Windows):
  py -3 scripts/python/scan_doc_stack_terms.py --root docs
  py -3 scripts/python/scan_doc_stack_terms.py --root docs --fail-on-hits
"""

from __future__ import annotations

import argparse
import datetime as dt
import io
import json
import os
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, List, Sequence


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

MD_FENCE_RE = re.compile(r"```.*?```", re.DOTALL)


@dataclass(frozen=True)
class PatternSpec:
    name: str
    pattern: str
    flags: str


def _compile(spec: PatternSpec) -> re.Pattern[str]:
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


def _load_patterns(map_path: Path) -> List[PatternSpec]:
    data = json.loads(map_path.read_text(encoding="utf-8"))
    reps = data.get("replacements") or []
    out: List[PatternSpec] = []
    for idx, item in enumerate(reps):
        name = str(item.get("name") or item.get("pattern") or f"pattern_{idx}")
        pattern = str(item.get("pattern") or "")
        flags = str(item.get("flags") or "")
        if not pattern:
            continue
        out.append(PatternSpec(name=name, pattern=pattern, flags=flags))
    return out


def _strip_markdown_code_fences(text: str) -> str:
    return MD_FENCE_RE.sub("", text)


def _find_hits(text: str, compiled: Sequence[tuple[PatternSpec, re.Pattern[str]]]) -> list[dict]:
    hits: list[dict] = []
    for spec, cre in compiled:
        ms = list(cre.finditer(text))
        if not ms:
            continue
        # Keep a small, stable preview for triage.
        previews: list[str] = []
        for m in ms[:3]:
            start = max(0, m.start() - 30)
            end = min(len(text), m.end() + 30)
            snippet = text[start:end].replace("\r", "").replace("\n", " ")
            previews.append(snippet)
        hits.append(
            {
                "name": spec.name,
                "pattern": spec.pattern,
                "flags": spec.flags,
                "count": len(ms),
                "previews": previews,
            }
        )
    return hits


def _ensure_dir(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default="docs", help="Root directory to scan (default: docs)")
    ap.add_argument(
        "--map",
        dest="map_path",
        default=str(DEFAULT_MAP_PATH),
        help="Replacement map JSON used as the scan keyword source",
    )
    ap.add_argument("--fail-on-hits", action="store_true", help="Exit non-zero if any hits are found")
    args = ap.parse_args()

    root = Path(args.root)
    map_path = Path(args.map_path)
    if not root.exists():
        raise SystemExit(f"[doc_stack_scan] root not found: {root}")
    if not map_path.exists():
        raise SystemExit(f"[doc_stack_scan] map file not found: {map_path}")

    specs = _load_patterns(map_path)
    compiled = [(s, _compile(s)) for s in specs]

    date = dt.date.today().strftime("%Y-%m-%d")
    out_dir = Path("logs") / "ci" / date / "doc-stack-scan"
    _ensure_dir(out_dir)
    scan_path = out_dir / "scan.json"
    summary_path = out_dir / "summary.json"

    results: list[dict] = []
    total_hits = 0
    files_scanned = 0

    for path in _iter_files(root):
        rel = path.as_posix()
        try:
            raw = path.read_bytes()
            text = raw.decode("utf-8", errors="strict")
        except Exception as e:
            # Encoding failures are handled by check_encoding.py; still record for traceability.
            results.append({"file": rel, "error": f"{type(e).__name__}: {e}", "hits": []})
            continue

        files_scanned += 1
        payload = text
        if path.suffix.lower() == ".md":
            payload = _strip_markdown_code_fences(payload)

        hits = _find_hits(payload, compiled)
        if hits:
            total_hits += sum(h["count"] for h in hits)
        results.append({"file": rel, "error": None, "hits": hits})

    files_with_hits = [r for r in results if r.get("hits")]
    top_files = sorted(
        (
            {
                "file": r["file"],
                "hit_count": sum(h["count"] for h in r["hits"]),
                "hit_names": [h["name"] for h in r["hits"]],
            }
            for r in files_with_hits
        ),
        key=lambda x: (-x["hit_count"], x["file"]),
    )
    top_terms: dict[str, int] = {}
    for r in files_with_hits:
        for h in r["hits"]:
            top_terms[h["name"]] = top_terms.get(h["name"], 0) + int(h["count"])

    summary = {
        "ts": dt.datetime.now().isoformat(),
        "root": root.as_posix(),
        "map": map_path.as_posix(),
        "files_scanned": files_scanned,
        "files_with_hits": len(files_with_hits),
        "hits": int(total_hits),
        "top_files": top_files[:25],
        "top_terms": sorted(({"name": k, "count": v} for k, v in top_terms.items()), key=lambda x: -x["count"])[
            :25
        ],
    }

    with io.open(scan_path, "w", encoding="utf-8") as f:
        json.dump(results, f, ensure_ascii=False, indent=2)
    with io.open(summary_path, "w", encoding="utf-8") as f:
        json.dump(summary, f, ensure_ascii=False, indent=2)

    print(f"[doc_stack_scan] files_scanned={files_scanned} hits={summary['hits']} out={out_dir.as_posix()}")
    if args.fail_on_hits and summary["hits"] > 0:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

