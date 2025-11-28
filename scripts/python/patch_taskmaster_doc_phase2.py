#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Patch task-master-superclaude-integration.md for Python/.taskmaster usage.

This script performs a small set of safe, UTF-8 based replacements so that
the workflow document matches the current newguild toolchain:

- Replace the obsolete coverage_gate.py example with run_dotnet.py using
  COVERAGE_LINES_MIN/COVERAGE_BRANCHES_MIN.
- Fix validate_task_links.py → task_links_validate.py to match the actual
  script name under scripts/python/.

Run once via:
  py -3 scripts/python/patch_taskmaster_doc_phase2.py
"""

from __future__ import annotations

from pathlib import Path


def patch_coverage_gate(text: str) -> str:
    old = "py -3 scripts/python/coverage_gate.py --threshold-lines 90 --threshold-branches 85"
    if old not in text:
        return text

    replacement = """$env:COVERAGE_LINES_MIN = \"90\"\n$env:COVERAGE_BRANCHES_MIN = \"85\"\npy -3 scripts/python/run_dotnet.py --solution Game.sln --configuration Debug"""
    return text.replace(old, replacement)


def patch_validate_task_links(text: str) -> str:
    # Unix-style example
    text = text.replace(
        "py -3 scripts/python/validate_task_links.py",
        "py -3 scripts/python/task_links_validate.py",
    )
    # Windows-style example
    text = text.replace(
        "py -3 scripts\\python\\validate_task_links.py",
        "py -3 scripts\\python\\task_links_validate.py",
    )
    return text


def main() -> None:
    doc_path = Path("docs/workflows/task-master-superclaude-integration.md")
    text = doc_path.read_text(encoding="utf-8")

    original = text
    text = patch_coverage_gate(text)
    text = patch_validate_task_links(text)

    if text == original:
        print("[patch_taskmaster_doc_phase2] no changes made (patterns not found)")
        return

    doc_path.write_text(text, encoding="utf-8")
    print("[patch_taskmaster_doc_phase2] document updated successfully")


if __name__ == "__main__":  # pragma: no cover - tiny helper
    main()

