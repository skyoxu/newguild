#!/usr/bin/env python3
"""
Clean common workspace caches for this repo (Windows-friendly).

This is intended to help reproduce CI behavior locally by removing generated
directories like .godot/mono, bin/obj, and Python caches.

Notes:
- This script only deletes within the repository root.
- By default it does NOT delete logs/ to avoid destroying forensic artifacts.

Usage (Windows):
  py -3 scripts/python/clean_workspace_caches.py
  py -3 scripts/python/clean_workspace_caches.py --dry-run
  py -3 scripts/python/clean_workspace_caches.py --include-logs
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import shutil
import sys
from pathlib import Path
from typing import Iterable


def repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


def safe_rel(root: Path, path: Path) -> str:
    try:
        return str(path.resolve().relative_to(root.resolve())).replace("\\", "/")
    except Exception:
        return str(path).replace("\\", "/")


def iter_cache_paths(root: Path, *, include_logs: bool) -> Iterable[Path]:
    # Deterministic, explicit roots first.
    candidates = [
        root / ".godot",
        root / "Game.Core" / "bin",
        root / "Game.Core" / "obj",
        root / "Game.Core.Tests" / "bin",
        root / "Game.Core.Tests" / "obj",
        root / "Game.Godot.Tests" / "bin",
        root / "Game.Godot.Tests" / "obj",
        root / ".pytest_cache",
    ]
    for p in candidates:
        yield p

    # Root-level tmp dirs created by tools.
    for p in root.iterdir():
        if not p.is_dir():
            continue
        name = p.name.lower()
        if name == "tmp" or name.startswith("tmp"):
            yield p

    # Recursive Python caches (keep scope to repo).
    for p in root.rglob("__pycache__"):
        if p.is_dir():
            yield p

    if include_logs:
        # Godot userdir redirection cache and local run artifacts.
        yield root / "logs" / "_godot_userdir"
        yield root / "logs" / "e2e"
        yield root / "logs" / "unit"


def delete_path(path: Path) -> tuple[bool, str | None]:
    try:
        if not path.exists():
            return True, None
        if path.is_file() or path.is_symlink():
            path.unlink()
            return True, None
        shutil.rmtree(path)
        return True, None
    except Exception as ex:
        return False, str(ex)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--include-logs", action="store_true")
    args = ap.parse_args()

    root = repo_root()
    date = dt.date.today().strftime("%Y-%m-%d")
    out_dir = root / "logs" / "ci" / date / "cache-clean"
    out_dir.mkdir(parents=True, exist_ok=True)

    actions = []
    seen = set()
    for p in iter_cache_paths(root, include_logs=args.include_logs):
        try:
            rp = p.resolve()
        except Exception:
            rp = p
        if rp in seen:
            continue
        seen.add(rp)

        rel = safe_rel(root, p)
        # Safety: never delete outside repo.
        if ".." in rel.split("/"):
            actions.append({"path": rel, "status": "skip", "reason": "outside_repo"})
            continue

        if args.dry_run:
            actions.append({"path": rel, "status": "dry_run"})
            continue

        ok, err = delete_path(p)
        actions.append({"path": rel, "status": "deleted" if ok else "failed", "error": err})

    summary = {
        "generated": dt.datetime.now().isoformat(),
        "root": str(root),
        "dry_run": bool(args.dry_run),
        "include_logs": bool(args.include_logs),
        "deleted": [a["path"] for a in actions if a["status"] == "deleted"],
        "failed": [a for a in actions if a["status"] == "failed"],
        "skipped": [a for a in actions if a["status"] == "skip"],
    }

    (out_dir / "session-details.json").write_text(
        json.dumps(actions, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    (out_dir / "session-summary.json").write_text(
        json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    (out_dir / "session-summary.txt").write_text(
        "\n".join(
            [
                f"CACHE_CLEAN dry_run={summary['dry_run']} include_logs={summary['include_logs']}",
                f"deleted={len(summary['deleted'])} failed={len(summary['failed'])} skipped={len(summary['skipped'])}",
            ]
        )
        + "\n",
        encoding="utf-8",
        newline="\n",
    )

    print(f"[REPORT] {out_dir / 'session-summary.json'}")
    print(f"[REPORT] {out_dir / 'session-summary.txt'}")
    if summary["failed"]:
        print("[WARN] Some paths could not be deleted. See session-details.json for errors.")
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

