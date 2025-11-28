#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Patch conceptual tasks.json references in workflow doc.

This script keeps the idea of "tasks.json" as a *schema example* but
clarifies that the actual storage in newguild lives under
`.taskmaster/tasks/*.json`. It adjusts narrative sentences so new readers
won't assume a physical tasks.json file exists at the repo root.

Run once via:
  py -3 scripts/python/patch_taskmaster_doc_phase4.py
"""

from __future__ import annotations

from pathlib import Path


def patch_conceptual_mentions(text: str) -> str:
    # 手动编辑 tasks.json → 手动编辑 .taskmaster 下的任务文件
    text = text.replace(
        "手动编辑 tasks.json 添加 blockers 字段",
        "手动编辑 `.taskmaster/tasks/*.json` 中对应任务，添加 `blockers` 字段",
    )
    text = text.replace(
        "# 手动编辑 tasks.json 添加 blockers 字段",
        "# 手动编辑 .taskmaster/tasks/*.json 中对应任务，添加 blockers 字段",
    )
    text = text.replace(
        "# 手动编辑 tasks.json 添加原因",
        "# 手动编辑 .taskmaster/tasks/*.json 中对应任务，添加原因",
    )

    # 读取 tasks.json[...] → 读取 .taskmaster/tasks/*.json 中的任务记录
    text = text.replace(
        "1. 读取 tasks.json[<task-id>].overlay 字段",
        "1. 读取 `.taskmaster/tasks/*.json` 中对应 task 的 overlay 字段",
    )
    text = text.replace(
        "# 1. 读取 tasks.json 找到任务 1.1",
        "# 1. 读取 `.taskmaster/tasks/*.json` 找到任务 1.1",
    )
    text = text.replace(
        "1. 读取 tasks.json[<task-id>].overlay 字段",
        "1. 读取 `.taskmaster/tasks/*.json` 中对应 task 的 overlay 字段",
    )

    # 批量更新 / 自动匹配说明
    text = text.replace(
        "- 自动匹配 tasks.json 中的任务与对应的 ACCEPTANCE_CHECKLIST.md",
        "- 自动匹配 `.taskmaster/tasks/*.json` 中的任务与对应的 ACCEPTANCE_CHECKLIST.md",
    )
    text = text.replace(
        "# 批量更新 tasks.json 的 overlay 字段",
        "# 批量更新 .taskmaster/tasks/*.json 中各任务的 overlay 字段",
    )

    # Task ID 与 tasks.json 一致 → 与 Taskmaster JSON 一致
    text = text.replace(
        "Task ID 与 tasks.json 一致",
        "Task ID 与 `.taskmaster/tasks/*.json` 中记录一致",
    )
    text = text.replace(
        "使用 `#1.1` 格式（与 tasks.json 一致）",
        "使用 `#1.1` 格式（与 `.taskmaster/tasks/*.json` 中的示例一致）",
    )

    # 从 tasks.json 聚合/提取度量数据 → 从 Taskmaster JSON 聚合/提取
    text = text.replace(
        "自动聚合 tasks.json 中的 completed 任务：",
        "自动聚合 `.taskmaster/tasks/*.json` 中 status=completed 的任务：",
    )
    text = text.replace(
        "从 tasks.json 提取度量数据：",
        "从 `.taskmaster/tasks/*.json` 提取度量数据：",
    )

    # 备注：模板中保留的 "<!-- 从 tasks.json 复制 description -->"，仅用于说明
    # 这里不强行替换，避免破坏示例注释结构。

    # 更新 tasks.json 的 prUrl 字段 → 更新 Taskmaster JSON 中的 prUrl 字段
    text = text.replace(
        "更新 tasks.json 的 `prUrl` 字段",
        "更新 `.taskmaster/tasks/*.json` 中对应任务的 `prUrl` 字段",
    )

    return text


def main() -> None:
    doc_path = Path("docs/workflows/task-master-superclaude-integration.md")
    text = doc_path.read_text(encoding="utf-8")
    original = text

    text = patch_conceptual_mentions(text)

    if text == original:
        print("[patch_taskmaster_doc_phase4] no changes made (patterns not found)")
        return

    doc_path.write_text(text, encoding="utf-8")
    print("[patch_taskmaster_doc_phase4] updated conceptual tasks.json mentions")


if __name__ == "__main__":  # pragma: no cover - tiny helper
    main()

