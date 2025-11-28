#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Patch task-master-superclaude-integration.md (paths for Taskmaster files).

This script normalizes legacy references to `tasks/tasks.json` so that they
match the current Taskmaster layout under `.taskmaster/tasks/*.json`.

Rules:
- Concrete file path examples are switched to `.taskmaster/tasks/tasks_back.json`
  as the main NG backbone file.
- Generic shell snippets using `cat tasks/tasks.json | jq ...` are rewritten to
  use `.taskmaster/tasks/tasks_back.json` as well.

Run once via:
  py -3 scripts/python/patch_taskmaster_doc_phase3.py
"""

from __future__ import annotations

from pathlib import Path


def patch_tasks_path(text: str) -> str:
    # Inline code/backtick cases first
    text = text.replace(
        "`tasks/tasks.json`",
        "`.taskmaster/tasks/tasks_back.json`",
    )

    # Plain text comment examples
    text = text.replace(
        "# 打开 tasks/tasks.json，确认任务",
        "# 打开 .taskmaster/tasks/tasks_back.json，确认任务",
    )

    # Shell command example with jq
    text = text.replace(
        "cat tasks/tasks.json | jq",
        "cat .taskmaster/tasks/tasks_back.json | jq",
    )

    return text


def main() -> None:
    doc_path = Path("docs/workflows/task-master-superclaude-integration.md")
    text = doc_path.read_text(encoding="utf-8")
    original = text

    text = patch_tasks_path(text)

    if text == original:
        print("[patch_taskmaster_doc_phase3] no changes made (patterns not found)")
        return

    doc_path.write_text(text, encoding="utf-8")
    print("[patch_taskmaster_doc_phase3] updated tasks/tasks.json path references")


if __name__ == "__main__":  # pragma: no cover - tiny helper
    main()

