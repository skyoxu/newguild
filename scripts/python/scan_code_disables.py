"""Scan source tree for line-level suppression directives and
write a structural refactor baseline report.

This script is designed to support NG-0034 (structural refactor
and suppression cleanup) and ADR-0005 (quality gates). It produces
an aggregate JSON report under logs/ci/<YYYY-MM-DD>/
quality-gates-structural-refactor.json.

Usage (Windows, from repo root):

    py -3 scripts/python/scan_code_disables.py

Optional arguments:
    --root <path>      Root directory to scan (default: repo root).
    --output <path>    Output JSON path (default: logs/ci/<date>/quality-gates-structural-refactor.json).

The script is intentionally conservative and only looks for a small
set of well-known suppression patterns. It can be extended when new
patterns are introduced.
"""

from __future__ import annotations

import argparse
import json
import sys
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Dict, Iterable, List, Optional


# Directories that should not be scanned for code suppressions.
DEFAULT_EXCLUDED_DIRS = {
    ".git",
    "logs",
    "backup",
    "backups",
    ".godot",
    ".taskmaster",
    "docs",
    "bin",
    "obj",
    "node_modules",
    "dist",
    "build",
    "artifacts",
    ".idea",
    ".vs",
    ".vscode",
}


# Individual files that should not be scanned. This is mainly used
# to exclude this script itself so that pattern definitions do not
# pollute the suppression baseline.
DEFAULT_EXCLUDED_FILES = {
    "scan_code_disables.py",
}


@dataclass
class SuppressionEntry:
    """Represents a single line-level suppression directive."""

    path: str
    line: int
    category: str
    pattern: str
    snippet: str


SUPPRESSION_PATTERNS = {
    # JavaScript / TypeScript / frontend related
    "eslint_disable": ["// eslint-disable", "/* eslint-disable"],
    "tslint_disable": ["// tslint:disable"],
    # C# / .NET
    "csharp_pragma_warning": ["#pragma warning disable"],
    "csharp_suppress_message": ["[System.Diagnostics.CodeAnalysis.SuppressMessage", "[SuppressMessage("],
}


def should_skip_dir(path: Path) -> bool:
    """Return True if directory should be excluded from scanning."""

    return path.name in DEFAULT_EXCLUDED_DIRS


def iter_files(root: Path) -> Iterable[Path]:
    """Yield candidate files under root, skipping known excluded directories.

    The script is intentionally broad and scans most text-like files.
    Binary files are handled by opening with errors="ignore".
    """

    for dirpath, dirnames, filenames in os_walk(root):
        # Filter excluded directories in-place to avoid descending into them.
        dirnames[:] = [d for d in dirnames if d not in DEFAULT_EXCLUDED_DIRS]

        for filename in filenames:
            # Skip obvious binary artifacts by extension.
            lower = filename.lower()
            if lower.endswith(
                (
                    ".png",
                    ".jpg",
                    ".jpeg",
                    ".bmp",
                    ".ico",
                    ".ogg",
                    ".wav",
                    ".mp3",
                    ".ttf",
                    ".otf",
                    ".pck",
                    ".exe",
                    ".dll",
                )
            ):
                continue

            if filename in DEFAULT_EXCLUDED_FILES:
                continue

            yield Path(dirpath) / filename


def os_walk(root: Path):  # type: ignore[override]
    """Wrapper for os.walk to make testing and patching easier."""

    import os

    return os.walk(root)


def classify_line(line: str) -> Optional[SuppressionEntry]:
    """Classify a line and return a SuppressionEntry template if matched.

    The caller is responsible for filling in path and line number.
    """

    stripped = line.strip()
    if not stripped:
        return None

    for category, patterns in SUPPRESSION_PATTERNS.items():
        for pattern in patterns:
            if pattern in stripped:
                return SuppressionEntry(
                    path="",
                    line=0,
                    category=category,
                    pattern=pattern,
                    snippet=stripped[:200],
                )

    return None


def scan_root(root: Path) -> List[SuppressionEntry]:
    """Scan the repository tree and collect suppression entries."""

    entries: List[SuppressionEntry] = []
    root = root.resolve()

    for file_path in iter_files(root):
        rel = file_path.relative_to(root).as_posix()

        try:
            with file_path.open("r", encoding="utf-8", errors="ignore") as f:
                for idx, line in enumerate(f, start=1):
                    template = classify_line(line)
                    if template is None:
                        continue

                    entry = SuppressionEntry(
                        path=rel,
                        line=idx,
                        category=template.category,
                        pattern=template.pattern,
                        snippet=template.snippet,
                    )
                    entries.append(entry)
        except OSError:
            # Non-readable file; ignore but keep scanning.
            continue

    return entries


def aggregate(entries: List[SuppressionEntry]) -> Dict[str, object]:
    """Build an aggregate JSON-serializable structure from entries."""

    summary: Dict[str, Dict[str, int]] = {}
    for entry in entries:
        cat = entry.category
        summary.setdefault(cat, {"count": 0})["count"] += 1

    return {
        "generated_at": datetime.utcnow().isoformat() + "Z",
        "total_suppression_count": len(entries),
        "categories": summary,
        "entries": [
            {
                "path": e.path,
                "line": e.line,
                "category": e.category,
                "pattern": e.pattern,
                "snippet": e.snippet,
            }
            for e in entries
        ],
    }


def default_output_path(root: Path) -> Path:
    """Compute the default output path under logs/ci/<YYYY-MM-DD>."""

    today = datetime.utcnow().strftime("%Y-%m-%d")
    return root / "logs" / "ci" / today / "quality-gates-structural-refactor.json"


def parse_args(argv: Optional[List[str]] = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Scan source tree for line-level suppression directives and "
            "emit a structural refactor baseline report."
        )
    )
    parser.add_argument(
        "--root",
        type=str,
        default=None,
        help="Root directory to scan (default: repository root inferred from script location).",
    )
    parser.add_argument(
        "--output",
        type=str,
        default=None,
        help="Output JSON file path (default: logs/ci/<date>/quality-gates-structural-refactor.json).",
    )
    return parser.parse_args(argv)


def main(argv: Optional[List[str]] = None) -> int:
    args = parse_args(argv)

    script_path = Path(__file__).resolve()
    repo_root = script_path.parents[2]

    if args.root is not None:
        root = Path(args.root).resolve()
    else:
        root = repo_root

    if args.output is not None:
        output_path = Path(args.output).resolve()
    else:
        output_path = default_output_path(root)

    entries = scan_root(root)
    report = aggregate(entries)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    with output_path.open("w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=2)

    # Print a short human-readable summary to stdout.
    print(f"Scanned root: {root}")
    print(f"Total suppression directives: {report['total_suppression_count']}")
    for category, info in report["categories"].items():
        print(f"  {category}: {info['count']}")
    print(f"Report written to: {output_path}")

    return 0


if __name__ == "__main__":  # pragma: no cover - CLI entry point
    raise SystemExit(main(sys.argv[1:]))
