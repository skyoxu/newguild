#!/usr/bin/env python3
"""
Sync overlay_refs from tasks_back.json into tasks.json (SSoT) via taskmaster_id mapping.

Mapping:
  tasks.json.master.tasks[].id  <->  tasks_back[].taskmaster_id

Behavior:
  - For each tasks_back entry with taskmaster_id != null:
      append its overlay_refs into the matching tasks.json task.overlay_refs.
  - Preserve order and de-duplicate.
  - Write a small report under logs/ci/<YYYY-MM-DD>/.

Notes:
  - This script only mutates tasks.json.
  - overlay_refs are treated as a list of workspace-relative paths.
"""

from __future__ import annotations

import json
from dataclasses import dataclass
from datetime import date
from pathlib import Path
from typing import Any, Dict, List, Tuple


ROOT = Path(__file__).resolve().parents[2]
TASKS_JSON = ROOT / ".taskmaster/tasks/tasks.json"
TASKS_BACK = ROOT / ".taskmaster/tasks/tasks_back.json"


@dataclass(frozen=True)
class Change:
    master_id: str
    added: List[str]


def _read_json(path: Path) -> Tuple[Any, str]:
    raw = path.read_text(encoding="utf-8")
    eol = "\r\n" if "\r\n" in raw else "\n"
    return json.loads(raw), eol


def _write_json(path: Path, obj: Any, eol: str) -> None:
    text = json.dumps(obj, ensure_ascii=False, indent=2)
    if not text.endswith("\n"):
        text += "\n"
    text = text.replace("\n", eol)
    path.write_text(text, encoding="utf-8")


def _ensure_list(value: Any) -> List[str]:
    if value is None:
        return []
    if isinstance(value, str):
        return [value]
    if isinstance(value, list):
        return [x for x in value if isinstance(x, str)]
    return []


def _merge_unique(existing: List[str], incoming: List[str]) -> Tuple[List[str], List[str]]:
    seen = set()
    out: List[str] = []
    added: List[str] = []

    for x in existing:
        if x in seen:
            continue
        seen.add(x)
        out.append(x)

    for x in incoming:
        if x in seen:
            continue
        seen.add(x)
        out.append(x)
        added.append(x)

    return out, added


def main() -> int:
    tasks_json, tasks_json_eol = _read_json(TASKS_JSON)
    tasks_back, _ = _read_json(TASKS_BACK)

    master_tasks = (tasks_json or {}).get("master", {}).get("tasks", [])
    if not isinstance(master_tasks, list):
        raise SystemExit("tasks.json: master.tasks must be a list")
    if not isinstance(tasks_back, list):
        raise SystemExit("tasks_back.json must be a list")

    by_master_id: Dict[str, List[str]] = {}
    for t in tasks_back:
        if not isinstance(t, dict):
            continue
        taskmaster_id = t.get("taskmaster_id")
        if taskmaster_id is None:
            continue
        master_id = str(taskmaster_id)
        overlay_refs = _ensure_list(t.get("overlay_refs"))
        if not overlay_refs:
            continue
        by_master_id.setdefault(master_id, []).extend(overlay_refs)

    changes: List[Change] = []
    for t in master_tasks:
        if not isinstance(t, dict):
            continue
        master_id = str(t.get("id"))
        incoming = by_master_id.get(master_id)
        if not incoming:
            continue
        existing = _ensure_list(t.get("overlay_refs"))
        merged, added = _merge_unique(existing, incoming)
        if added:
            t["overlay_refs"] = merged
            changes.append(Change(master_id=master_id, added=added))

    if changes:
        _write_json(TASKS_JSON, tasks_json, tasks_json_eol)

    out_dir = ROOT / "logs/ci" / date.today().isoformat()
    out_dir.mkdir(parents=True, exist_ok=True)
    report_json = out_dir / "sync-master-overlay-refs.json"
    report_txt = out_dir / "sync-master-overlay-refs.txt"

    report = {
        "ok": True,
        "tasks_json": str(TASKS_JSON.relative_to(ROOT)),
        "tasks_back": str(TASKS_BACK.relative_to(ROOT)),
        "changed_tasks": [
            {"id": c.master_id, "added_count": len(c.added), "added": c.added} for c in changes
        ],
        "changed_count": len(changes),
    }

    report_json.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    report_txt.write_text(
        "\n".join(
            [
                f"changed_count: {len(changes)}",
                *[f"- {c.master_id}: +{len(c.added)}" for c in changes],
                "",
                f"[REPORT] {report_json}",
            ]
        )
        + "\n",
        encoding="utf-8",
    )

    print(f"[REPORT] {report_json}")
    print(f"[REPORT] {report_txt}")
    print(f"[OK] Updated {len(changes)} master tasks in tasks.json")
    return 0


if __name__ == "__main__":  # pragma: no cover
    raise SystemExit(main())

