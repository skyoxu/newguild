#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Sync selected docs/migration files from C:\\buildgame\\godotgame to this repo,
overwriting mojibake-affected files with the same relative path.

All reads/writes use UTF-8 encoding.
"""

from __future__ import annotations

from pathlib import Path


NEWGUILD_ROOT = Path(r"C:\buildgame\newguild")
GODOTGAME_ROOT = Path(r"C:\buildgame\godotgame")

TARGET_FILES = [
    "docs/migration/MIGRATION_INDEX.md",
    "docs/migration/Phase-11-Scene-Integration-Tests-REVISED.md",
    "docs/migration/Phase-11-Scene-Integration-Tests.md",
    "docs/migration/Phase-14-Godot-Security-Backlog.md",
    "docs/migration/Phase-14-Godot-Security-Baseline.md",
    "docs/migration/Phase-3-Project-Structure.md",
    "docs/migration/VERIFICATION_REPORT_Phase11-12.md",
    "docs/migration/VERIFICATION_SUMMARY.txt",
]


def main() -> None:
    for rel in TARGET_FILES:
        src = GODOTGAME_ROOT / rel
        dst = NEWGUILD_ROOT / rel

        if not src.exists():
            print(f"[SKIP] Source not found: {src}")
            continue
        if not dst.exists():
            print(f"[WARN] Dest not found (will create): {dst}")
            dst.parent.mkdir(parents=True, exist_ok=True)

        content = src.read_text(encoding="utf-8", errors="strict")
        dst.write_text(content, encoding="utf-8")
        print(f"[SYNC] {src} -> {dst}")


if __name__ == "__main__":
    main()

