#!/usr/bin/env python3
"""
Ensure T3 Save/Load tasks exist in view files and are promoted to master tasks.json.

This script is intentionally idempotent:
- If the tasks already exist, it only performs safe normalization (schema fields + mapping consistency)
  and will not rewrite titles/acceptance text.
- If they are missing, it adds minimal placeholders (still valid JSON, UTF-8).

Files:
- Master SSoT: `.taskmaster/tasks/tasks.json`
- Views: `.taskmaster/tasks/tasks_back.json`, `.taskmaster/tasks/tasks_gameplay.json`

Mapping:
- master.tasks[].id  <->  view[].taskmaster_id

Outputs:
- Writes an audit report under `logs/ci/<YYYY-MM-DD>/task-mapping/`

All reads/writes are UTF-8.
"""

from __future__ import annotations

import datetime as dt
import json
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[2]

MASTER_PATH = REPO_ROOT / ".taskmaster" / "tasks" / "tasks.json"
BACK_PATH = REPO_ROOT / ".taskmaster" / "tasks" / "tasks_back.json"
PLAY_PATH = REPO_ROOT / ".taskmaster" / "tasks" / "tasks_gameplay.json"


NEW_BACK_ID = "NG-0046"
NEW_PLAY_ID = "GM-0207"
MASTER_CORE_ID = "25"
MASTER_UI_ID = "26"

CORE_CONTRACT_REFS = [
    "core.save.requested",
    "core.save.completed",
    "core.save.failed",
    "core.load.requested",
    "core.load.completed",
    "core.load.failed",
    "core.save.format.migration.applied",
]

UI_CONTRACT_REFS = [
    "core.save.requested",
    "core.save.completed",
    "core.save.failed",
    "core.load.requested",
    "core.load.completed",
    "core.load.failed",
]


def _read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def _write_json_pretty(path: Path, obj: Any) -> None:
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _today_dir() -> Path:
    day = dt.date.today().isoformat()
    out_dir = REPO_ROOT / "logs" / "ci" / day / "task-mapping"
    out_dir.mkdir(parents=True, exist_ok=True)
    return out_dir


def _str_id(v: Any) -> str:
    return str(v).strip()


def _ensure_contractrefs(view_tasks: list[dict[str, Any]], changes: list[dict[str, Any]], view_name: str) -> None:
    for t in view_tasks:
        if not isinstance(t, dict):
            continue
        if "contractRefs" not in t:
            t["contractRefs"] = []
            changes.append({"view": view_name, "id": t.get("id"), "change": "add_contractRefs_default_empty"})


def _find_task(tasks: list[dict[str, Any]], task_id: str) -> dict[str, Any] | None:
    for t in tasks:
        if isinstance(t, dict) and str(t.get("id")) == task_id:
            return t
    return None


def _normalize_master_deps(task: dict[str, Any], remove: set[str], ensure: set[str], changes: list[dict[str, Any]]) -> None:
    before = [_str_id(x) for x in (task.get("dependencies") or [])]
    after = [d for d in before if d not in remove]
    for d in sorted(ensure):
        if d not in after:
            after.append(d)
    if after != before:
        task["dependencies"] = after
        changes.append({"master_id": task.get("id"), "field": "dependencies", "before": before, "after": after})


def main() -> int:
    report_changes: list[dict[str, Any]] = []
    report_warnings: list[str] = []

    master_obj = _read_json(MASTER_PATH)
    master_tasks: list[dict[str, Any]] = master_obj["master"]["tasks"]

    back_tasks: list[dict[str, Any]] = _read_json(BACK_PATH)
    play_tasks: list[dict[str, Any]] = _read_json(PLAY_PATH)

    # Normalize view schemas (contractRefs should exist for both views).
    _ensure_contractrefs(back_tasks, report_changes, "tasks_back.json")
    _ensure_contractrefs(play_tasks, report_changes, "tasks_gameplay.json")

    # Ensure view tasks exist; if missing, add minimal placeholders.
    back_task = _find_task(back_tasks, NEW_BACK_ID)
    if back_task is None:
        back_task = {
            "id": NEW_BACK_ID,
            "taskmaster_id": int(MASTER_CORE_ID),
            "story_id": "PRD-GUILD-MANAGER-T3-SAVELOAD-SSOT",
            "title": "T3 Save/Load + Schema Migration (SSoT view)",
            "description": "Acceptance/contract view for Save/Load + schema migration.",
            "status": "pending",
            "priority": "P0",
            "layer": "core",
            "depends_on": [],
            "adr_refs": [],
            "chapter_refs": [],
            "overlay_refs": [],
            "labels": ["save", "load", "migration", "crosscutting"],
            "owner": "architecture",
            "test_refs": [],
            "acceptance": [],
            "test_strategy": [],
            "contractRefs": CORE_CONTRACT_REFS,
            "taskmaster_exported": False,
        }
        back_tasks.append(back_task)
        report_changes.append({"view": "tasks_back.json", "id": NEW_BACK_ID, "change": "added_placeholder_task"})
    else:
        if back_task.get("taskmaster_id") != int(MASTER_CORE_ID):
            report_changes.append(
                {
                    "view": "tasks_back.json",
                    "id": NEW_BACK_ID,
                    "change": "fix_taskmaster_id",
                    "before": back_task.get("taskmaster_id"),
                    "after": int(MASTER_CORE_ID),
                }
            )
            back_task["taskmaster_id"] = int(MASTER_CORE_ID)
        if not back_task.get("contractRefs"):
            back_task["contractRefs"] = CORE_CONTRACT_REFS
            report_changes.append({"view": "tasks_back.json", "id": NEW_BACK_ID, "change": "fill_contractRefs"})

    play_task = _find_task(play_tasks, NEW_PLAY_ID)
    if play_task is None:
        play_task = {
            "id": NEW_PLAY_ID,
            "taskmaster_id": int(MASTER_UI_ID),
            "story_id": "PRD-GUILD-MANAGER-T3-SAVELOAD-UI",
            "title": "T3 Save/Load UI Entry (view)",
            "description": "Gameplay view: player-accessible entry point for Save/Load.",
            "status": "pending",
            "priority": "P1",
            "layer": "adapter",
            "depends_on": [NEW_BACK_ID],
            "adr_refs": [],
            "chapter_refs": [],
            "overlay_refs": [],
            "labels": ["ui", "save", "load", "playable"],
            "owner": "architecture",
            "test_refs": [],
            "acceptance": [],
            "test_strategy": [],
            "contractRefs": UI_CONTRACT_REFS,
            "taskmaster_exported": False,
        }
        play_tasks.append(play_task)
        report_changes.append({"view": "tasks_gameplay.json", "id": NEW_PLAY_ID, "change": "added_placeholder_task"})
    else:
        if play_task.get("taskmaster_id") != int(MASTER_UI_ID):
            report_changes.append(
                {
                    "view": "tasks_gameplay.json",
                    "id": NEW_PLAY_ID,
                    "change": "fix_taskmaster_id",
                    "before": play_task.get("taskmaster_id"),
                    "after": int(MASTER_UI_ID),
                }
            )
            play_task["taskmaster_id"] = int(MASTER_UI_ID)
        if not play_task.get("contractRefs"):
            play_task["contractRefs"] = UI_CONTRACT_REFS
            report_changes.append({"view": "tasks_gameplay.json", "id": NEW_PLAY_ID, "change": "fill_contractRefs"})

    # Ensure master tasks exist; if missing, warn (do not create here to avoid unintended semantics rewrites).
    master_by_id = {str(t.get("id")): t for t in master_tasks if isinstance(t, dict) and t.get("id") is not None}

    t25 = master_by_id.get(MASTER_CORE_ID)
    if t25 is None:
        report_warnings.append("Master Task 25 not found (expected Save/Load core task).")
    else:
        _normalize_master_deps(t25, remove={"13", "14"}, ensure={"10", "11", "12", "20"}, changes=report_changes)

    t26 = master_by_id.get(MASTER_UI_ID)
    if t26 is None:
        report_warnings.append("Master Task 26 not found (expected Save/Load UI entry task).")
    else:
        _normalize_master_deps(t26, remove=set(), ensure={MASTER_CORE_ID}, changes=report_changes)

    # Save/Load should precede AI ecosystem expansion: Task 15 depends on 25 if present.
    t15 = master_by_id.get("15")
    if t15 is not None and t25 is not None:
        _normalize_master_deps(t15, remove=set(), ensure={MASTER_CORE_ID}, changes=report_changes)

    # Write files only if needed.
    if report_changes:
        _write_json_pretty(MASTER_PATH, master_obj)
        _write_json_pretty(BACK_PATH, back_tasks)
        _write_json_pretty(PLAY_PATH, play_tasks)

    out_dir = _today_dir()
    out_json = out_dir / "t3-save-load-task-ensure.json"
    out_txt = out_dir / "t3-save-load-task-ensure.txt"
    payload = {
        "generated": dt.datetime.now().replace(microsecond=0).isoformat(),
        "changes_count": len(report_changes),
        "warnings_count": len(report_warnings),
        "changes": report_changes,
        "warnings": report_warnings,
    }
    _write_json_pretty(out_json, payload)
    out_txt.write_text(
        "\n".join(
            [
                f"generated: {payload['generated']}",
                f"changes: {payload['changes_count']}",
                f"warnings: {payload['warnings_count']}",
                *[f"warning: {w}" for w in report_warnings],
                "",
            ]
        ),
        encoding="utf-8",
    )

    print(f"[REPORT] {out_json}")
    print(f"[REPORT] {out_txt}")
    if report_warnings:
        print("[WARN] Completed with warnings.")
    else:
        print("[OK] Completed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
