#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Scan documentation-like files for likely UTF-8->GBK style mojibake.

Heuristic only: we look for rarely-used characters/sequences that常见于乱码场景，
例如 “锛”“鈥”“鍦烘” 等。结果用于人工复核，不做自动修改。
"""

from __future__ import annotations

from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]

# 重点扫描目录：文档、任务定义、日志说明等
TARGET_DIRS = [
    ROOT / "docs",
    ROOT / ".taskmaster",
    ROOT / "logs",
]

# 简单乱码特征（可按需扩展）
MOJIBAKE_PATTERNS = [
    "锛",      # 常见 UTF-8→GBK 乱码
    "鈥",      # 对应 ‘…’ 等被错误解码
    "鍦烘",    # “场景”等词常见乱码前缀
    "闆嗘",    # “集成”等词常见乱码前缀
    "鑰冨",    # 一些罕见部件组合
]

# 只看文本后缀
TEXT_SUFFIXES = {
    ".md",
    ".txt",
    ".json",
    ".yml",
    ".yaml",
}


def is_text_file(path: Path) -> bool:
    return path.suffix.lower() in TEXT_SUFFIXES


def main() -> None:
    suspect_files: list[tuple[str, list[str]]] = []

    for base in TARGET_DIRS:
        if not base.exists():
            continue
        for path in base.rglob("*"):
            if not path.is_file():
                continue
            if not is_text_file(path):
                continue
            try:
                text = path.read_text(encoding="utf-8", errors="ignore")
            except OSError:
                continue

            hits = [p for p in MOJIBAKE_PATTERNS if p in text]
            if hits:
                # 记录相对路径与命中的特征
                rel = path.relative_to(ROOT)
                suspect_files.append((str(rel), hits))

    if not suspect_files:
        print("No obvious mojibake patterns detected in docs/.taskmaster/logs.")
        return

    print("Suspect files with possible mojibake (heuristic):")
    for rel, hits in sorted(suspect_files):
        uniq_hits = ", ".join(sorted(set(hits)))
        print(f"  - {rel}  (patterns: {uniq_hits})")


if __name__ == "__main__":
    main()

