#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Rename old project name 'godotgame' to 'newguild' under docs/migration,
using UTF-8 and a constrained, case-sensitive heuristic.

Semantic constraints:
- 仅处理 docs/migration 下的文档文件（.md/.txt/.json/.yml/.yaml）；
- 仅替换小写 'godotgame' 子串，不触及 'GodotGame.csproj' 等类型名/文件名；
- 典型场景包括本地路径 (C:\\buildgame\\godotgame)、Git 远程地址以及文字描述。
"""

from __future__ import annotations

from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
MIGRATION_DIR = ROOT / "docs" / "migration"
TEXT_SUFFIXES = {".md", ".txt", ".json", ".yml", ".yaml"}


def is_text_file(path: Path) -> bool:
    return path.suffix.lower() in TEXT_SUFFIXES


def main() -> None:
    if not MIGRATION_DIR.exists():
        raise SystemExit(f"Migration directory not found: {MIGRATION_DIR}")

    total_files = 0
    total_replacements = 0

    for path in MIGRATION_DIR.rglob("*"):
        if not path.is_file():
            continue
        if not is_text_file(path):
            continue

        text = path.read_text(encoding="utf-8", errors="strict")
        if "godotgame" not in text:
            continue

        count = text.count("godotgame")
        new_text = text.replace("godotgame", "newguild")
        if new_text != text:
            path.write_text(new_text, encoding="utf-8")
            rel = path.relative_to(ROOT)
            print(f"[REWRITE] {rel}  (replacements: {count})")
            total_files += 1
            total_replacements += count

    if total_files == 0:
        print("No 'godotgame' references found under docs/migration (nothing changed).")
    else:
        print(f"Updated {total_files} files, {total_replacements} replacements in total.")


if __name__ == "__main__":
    main()

