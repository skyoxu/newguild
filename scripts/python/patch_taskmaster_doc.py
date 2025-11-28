#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""One-off patch helper for task-master-superclaude-integration.md.

This script uses UTF-8 explicitly to read/write the workflow document and
corrects legacy references that still point to tasks.json as the place to
add blockers. In newguild, Taskmaster data lives under .taskmaster/tasks/*.json.

Run once via:
  py -3 scripts/python/patch_taskmaster_doc.py
"""

from __future__ import annotations

from pathlib import Path


def main() -> None:
    doc_path = Path("docs/workflows/task-master-superclaude-integration.md")
    text = doc_path.read_text(encoding="utf-8")

    old = "在 tasks.json 添加 `blockers` 字段，说明具体问题和文件行号"
    new = (
        "在 `.taskmaster/tasks/*.json` 中对应任务添加 `blockers` 字段，"
        "说明具体问题和文件行号"
    )

    if old not in text:
        # No-op if the pattern is already updated or missing.
        print("[patch_taskmaster_doc] pattern not found, no changes made")
        return

    updated = text.replace(old, new)
    doc_path.write_text(updated, encoding="utf-8")
    print("[patch_taskmaster_doc] updated blockers description to use .taskmaster/tasks/*.json")


if __name__ == "__main__":  # pragma: no cover - tiny helper
    main()

