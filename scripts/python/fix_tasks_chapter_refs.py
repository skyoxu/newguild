#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Auto-fix chapter_refs for Taskmaster tasks based on ADR_FOR_CH.

This script adjusts chapter_refs in:
  - .taskmaster/tasks/tasks_back.json
  - .taskmaster/tasks/tasks_gameplay.json

Rules:
  - For each task, compute expected chapters from adr_refs using ADR_FOR_CH.
  - If expected set is non-empty:
      * Add missing chapters.
      * Remove extra chapters that are within the known CH set derived
        from ADR_FOR_CH.
  - Tasks whose adr_refs 只包含未在映射表中的 ADR 将保持原有 chapter_refs，不作修改。

All JSON I/O uses UTF-8 and preserves other fields.
"""

from __future__ import annotations

import json
from pathlib import Path


ADR_FOR_CH: dict[str, list[str]] = {
    "ADR-0002": ["CH02"],
    "ADR-0019": ["CH02"],
    "ADR-0003": ["CH03"],
    "ADR-0004": ["CH04"],
    "ADR-0006": ["CH05"],
    "ADR-0007": ["CH05", "CH06"],
    "ADR-0005": ["CH07"],
    "ADR-0011": ["CH07", "CH10"],
    "ADR-0008": ["CH10"],
    "ADR-0015": ["CH09"],
    "ADR-0018": ["CH01", "CH06", "CH07"],
    "ADR-0020": ["CH06", "CH07"],
    "ADR-0023": ["CH05"],
}

KNOWN_CH: set[str] = set(ch for lst in ADR_FOR_CH.values() for ch in lst)


def fix_tasks(path: Path) -> None:
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, list):
        print(f"[fix_tasks_chapter_refs] {path} 顶层不是数组, 跳过")
        return

    changed = False
    for task in data:
        adr_refs = task.get("adr_refs", [])
        expected: set[str] = set()
        for adr in adr_refs:
            expected.update(ADR_FOR_CH.get(adr, []))

        if not expected:
            # 没有可映射的 ADR, 保留原有 chapter_refs
            continue

        current = set(task.get("chapter_refs", []))
        missing = expected - current
        extra = current - expected

        if missing or extra:
            # 只移除属于 KNOWN_CH 的多余章节, 防止误删人工标记的其他章节
            to_remove = {ch for ch in extra if ch in KNOWN_CH}
            new_set = (current | missing) - to_remove
            task["chapter_refs"] = sorted(new_set)
            changed = True

    if changed:
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
        print(f"[fix_tasks_chapter_refs] 已更新 {path}")
    else:
        print(f"[fix_tasks_chapter_refs] {path} 无需修改")


def main() -> None:
    root = Path(__file__).resolve().parents[2]
    back = root / ".taskmaster" / "tasks" / "tasks_back.json"
    gameplay = root / ".taskmaster" / "tasks" / "tasks_gameplay.json"

    fix_tasks(back)
    fix_tasks(gameplay)


if __name__ == "__main__":  # pragma: no cover
    main()

