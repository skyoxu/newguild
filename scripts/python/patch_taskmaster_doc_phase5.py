#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Final tweaks for tasks.json wording in workflow doc.

This script fixes a few remaining places where `tasks.json` still appears
as if it were a concrete file, and rewrites them to refer to
`.taskmaster/tasks/*.json` instead. It keeps the schema explanation
paragraph (which already clarifies that tasks.json is only an example).

Run once via:
  py -3 scripts/python/patch_taskmaster_doc_phase5.py
"""

from __future__ import annotations

from pathlib import Path


def patch_remaining(text: str) -> str:
    # Overlay sync line
    text = text.replace(
        "overlay 璺緞鍙樻洿锛岄渶鍚屾鏇存柊 tasks.json",
        "overlay 璺緞鍙樻洿锛岄渶鍚屾鏇存柊 `.taskmaster/tasks/*.json`",
    )

    # Blockers bullet
    text = text.replace(
        "鍦?tasks.json 娣诲姞 `blockers`",
        "鍦?`.taskmaster/tasks/*.json` 涓搴斾换鍔″姞 `blockers`",
    )

    # Python snippet comment "写入 tasks.json"
    text = text.replace(
        "# 鍐欏叆 tasks.json",
        "# 鍐欏叆 .taskmaster/tasks/*.json",
    )

    # 手动编辑 tasks.json 或使用 jq
    text = text.replace(
        "(鎵嬪姩缂栬緫 tasks.json 鎴栦娇鐢?jq)",
        "(鎵嬪姩缂栬緫 .taskmaster/tasks/*.json 鎴栦娇鐢?jq)",
    )

    return text


def main() -> None:
    doc_path = Path("docs/workflows/task-master-superclaude-integration.md")
    text = doc_path.read_text(encoding="utf-8")
    original = text

    text = patch_remaining(text)

    if text == original:
        print("[patch_taskmaster_doc_phase5] no changes made (patterns not found)")
        return

    doc_path.write_text(text, encoding="utf-8")
    print("[patch_taskmaster_doc_phase5] updated remaining tasks.json mentions")


if __name__ == "__main__":  # pragma: no cover - tiny helper
    main()

