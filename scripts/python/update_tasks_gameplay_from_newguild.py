#!/usr/bin/env python3
"""
Update tasks_gameplay.json by appending "next stage" tasks extracted from tasks_newguild.json.

Repository policy notes:
  - tasks.json is the SSoT for completion status.
  - This script only updates .taskmaster/tasks/tasks_gameplay.json as a planning/backlog view.
  - All reports are written under logs/ci/<YYYY-MM-DD>/task-mapping/ (UTF-8).

Usage (Windows):
  py -3 scripts/python/update_tasks_gameplay_from_newguild.py

What it does:
  1) Reads .taskmaster/tasks/tasks.json and summarizes done/pending/in-progress.
  2) Marks tasks_gameplay items with taskmaster_id as done when the SSoT says done.
  3) Selects a "next stage" task set from tasks_newguild.json (default: core modules after T2 playable loop).
  4) Appends them into tasks_gameplay.json using the existing tasks_gameplay schema.
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
from pathlib import Path
from typing import Any


def repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


def today_str() -> str:
    return dt.date.today().strftime("%Y-%m-%d")


def read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, obj: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")


def to_ng_id(n: int | str) -> str:
    try:
        v = int(str(n).strip())
    except Exception:
        v = 0
    return f"NG-{v:04d}"


def map_priority(prio: str | None) -> str:
    p = (prio or "").strip().lower()
    if p == "high":
        return "P0"
    if p == "medium":
        return "P1"
    if p == "low":
        return "P2"
    return "P2"


TASK_GAMEPLAY_KEYS = [
    "id",
    "story_id",
    "title",
    "description",
    "status",
    "priority",
    "layer",
    "depends_on",
    "adr_refs",
    "chapter_refs",
    "overlay_refs",
    "labels",
    "owner",
    "test_refs",
    "acceptance",
    "test_strategy",
    "taskmaster_id",
    "taskmaster_exported",
]


def normalize_gameplay_item(item: dict[str, Any]) -> dict[str, Any]:
    clean = {k: item.get(k) for k in TASK_GAMEPLAY_KEYS}
    # Ensure optional keys exist with stable types.
    clean["depends_on"] = list(clean.get("depends_on") or [])
    clean["adr_refs"] = list(clean.get("adr_refs") or [])
    clean["chapter_refs"] = list(clean.get("chapter_refs") or [])
    clean["overlay_refs"] = list(clean.get("overlay_refs") or [])
    clean["labels"] = list(clean.get("labels") or [])
    clean["test_refs"] = list(clean.get("test_refs") or [])
    clean["acceptance"] = list(clean.get("acceptance") or [])
    clean["test_strategy"] = list(clean.get("test_strategy") or [])
    return clean


def gameplay_fingerprint(item: dict[str, Any]) -> tuple[str, str, str, str]:
    taskmaster_id = "" if item.get("taskmaster_id") is None else str(item.get("taskmaster_id")).strip()
    story_id = str(item.get("story_id") or "").strip()
    title = str(item.get("title") or "").strip()
    description = str(item.get("description") or "").strip()
    return (taskmaster_id, story_id, title, description)


def dedup_gameplay_items(items: list[dict[str, Any]]) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    seen: dict[tuple[str, str, str, str], str] = {}
    kept: list[dict[str, Any]] = []
    removed: list[dict[str, Any]] = []
    for it in items:
        fp = gameplay_fingerprint(it)
        if fp in seen:
            removed.append({"id": it.get("id"), "dupe_of": seen[fp], "title": it.get("title")})
            continue
        seen[fp] = str(it.get("id") or "")
        kept.append(it)
    return kept, removed


def next_gm_id(existing: list[dict[str, Any]]) -> str:
    mx = 0
    for t in existing:
        s = str(t.get("id") or "")
        if not s.startswith("GM-"):
            continue
        try:
            mx = max(mx, int(s.split("-", 1)[1]))
        except Exception:
            continue
    return f"GM-{mx + 1:04d}"


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Append next-stage tasks from tasks_newguild.json into tasks_gameplay.json (planning view), "
        "while syncing completion status from tasks.json (SSoT) and preventing duplicates.",
    )
    parser.add_argument(
        "--prd-path",
        type=str,
        default=str(Path(".taskmaster") / "docs" / "prd.txt"),
        help="Path to PRD text (for report context only). Default: .taskmaster/docs/prd.txt",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Compute and write the report, but do not modify tasks_gameplay.json.",
    )
    args = parser.parse_args()

    root = repo_root()
    tasks_json_path = root / ".taskmaster" / "tasks" / "tasks.json"
    tasks_gameplay_path = root / ".taskmaster" / "tasks" / "tasks_gameplay.json"
    tasks_newguild_path = root / ".taskmaster" / "tasks" / "tasks_newguild.json"
    prd_path = (root / args.prd_path).resolve()

    tasks_json = read_json(tasks_json_path)
    master_tasks = (tasks_json.get("master") or {}).get("tasks") or []
    ssot_by_id = {str(t.get("id")): t for t in master_tasks if t.get("id") is not None}
    ssot_done_ids = {tid for tid, t in ssot_by_id.items() if str(t.get("status")).lower() == "done"}
    ssot_open = [
        {"id": t.get("id"), "status": t.get("status"), "title": t.get("title")}
        for t in master_tasks
        if str(t.get("status")).lower() != "done"
    ]

    gameplay = read_json(tasks_gameplay_path)
    if not isinstance(gameplay, list):
        raise SystemExit(f"Unexpected tasks_gameplay.json shape (expected list): {type(gameplay).__name__}")

    newguild = read_json(tasks_newguild_path)
    if not isinstance(newguild, list):
        raise SystemExit(f"Unexpected tasks_newguild.json shape (expected list): {type(newguild).__name__}")

    prd_head: str | None = None
    prd_error: str | None = None
    try:
        if prd_path.exists():
            prd_head = prd_path.read_text(encoding="utf-8")[:400]
        else:
            prd_error = "not_found"
    except Exception as exc:
        prd_error = str(exc)

    # 1) Update existing gameplay tasks' status using SSoT (tasks.json).
    updated_existing: list[dict[str, Any]] = []
    existing_updates = []
    for_dupe_check: list[dict[str, Any]] = []
    for t in gameplay:
        if not isinstance(t, dict):
            continue
        old_status = t.get("status")
        taskmaster_id = t.get("taskmaster_id")
        if taskmaster_id is not None and str(taskmaster_id) in ssot_done_ids:
            t["status"] = "done"
        updated_existing.append(normalize_gameplay_item(t))
        for_dupe_check.append(normalize_gameplay_item(t))
        if t.get("status") != old_status:
            existing_updates.append({"id": t.get("id"), "from": old_status, "to": t.get("status"), "taskmaster_id": taskmaster_id})

    # 1.5) De-duplicate tasks_gameplay (idempotency + safety).
    updated_existing, removed_dupes = dedup_gameplay_items(updated_existing)

    # 2) Select next-stage tasks from tasks_newguild.json.
    # Heuristic: after T2 minimal loop, focus on 6 modules + combat simulation + AI ecosystem.
    # (IDs below are tasks_newguild numeric ids; dependencies are converted to NG-#### for tasks_gameplay.)
    candidate_ids = [13, 14, 15, 16, 17, 18, 19, 20, 21]
    by_num_id: dict[int, dict[str, Any]] = {}
    for t in newguild:
        if not isinstance(t, dict):
            continue
        sid = t.get("id")
        if sid is None:
            continue
        try:
            nid = int(str(sid).strip())
        except Exception:
            continue
        by_num_id[nid] = t

    selected = []
    missing = []
    for nid in candidate_ids:
        if nid not in by_num_id:
            missing.append(nid)
            continue
        selected.append(by_num_id[nid])

    # 3) Append as tasks_gameplay schema items (GM-#### ids).
    appended = []
    appended_map: list[dict[str, Any]] = []
    skipped_existing = []
    existing_fps = {gameplay_fingerprint(t): str(t.get("id") or "") for t in updated_existing}
    for t in selected:
        fp = (
            "",
            str(t.get("story_id") or "PRD-GUILD-MANAGER").strip(),
            str(t.get("title") or "").strip(),
            str(t.get("description") or "").strip(),
        )
        if fp in existing_fps:
            skipped_existing.append({"tasks_newguild_id": int(str(t.get("id")).strip()), "already_in": existing_fps[fp]})
            continue

        gm_next = next_gm_id(updated_existing + appended)
        nid = int(str(t.get("id")).strip())
        depends = [to_ng_id(x) for x in (t.get("depends_on") or [])]
        item = {
            "id": gm_next,
            "story_id": str(t.get("story_id") or "PRD-GUILD-MANAGER"),
            "title": str(t.get("title") or "").strip(),
            "description": str(t.get("description") or "").strip(),
            "status": "pending",
            "priority": map_priority(t.get("priority")),
            "layer": t.get("layer") or "tbd",
            "depends_on": depends,
            "adr_refs": t.get("adr_refs") or [],
            "chapter_refs": t.get("chapter_refs") or [],
            "overlay_refs": t.get("overlay_refs") or [],
            "labels": t.get("labels") or [],
            "owner": t.get("owner") or "unassigned",
            "test_refs": t.get("test_refs") or [],
            "acceptance": t.get("acceptance") or [],
            "test_strategy": t.get("test_strategy") or [],
            "taskmaster_id": None,
            "taskmaster_exported": False,
        }
        appended.append(normalize_gameplay_item(item))
        appended_map.append({"tasks_newguild_id": nid, "tasks_gameplay_id": gm_next})

    # 4) Write file (drop helper _source to keep schema clean).
    merged = updated_existing + appended
    if not args.dry_run:
        write_json(tasks_gameplay_path, merged)

    # 5) Report
    report_dir = root / "logs" / "ci" / today_str() / "task-mapping"
    report = {
        "generated": dt.datetime.now().isoformat(timespec="seconds"),
        "inputs": {
            "tasks_json": str(tasks_json_path.relative_to(root)).replace("\\", "/"),
            "tasks_gameplay": str(tasks_gameplay_path.relative_to(root)).replace("\\", "/"),
            "tasks_newguild": str(tasks_newguild_path.relative_to(root)).replace("\\", "/"),
            "prd": str(prd_path.relative_to(root)).replace("\\", "/") if str(prd_path).startswith(str(root)) else str(prd_path),
        },
        "tasks_json_summary": {
            "total": len(master_tasks),
            "done": len(ssot_done_ids),
            "not_done": len(ssot_open),
            "not_done_items": ssot_open,
        },
        "tasks_gameplay_update": {
            "existing_count_before": len(gameplay),
            "existing_status_updates": existing_updates,
            "dedup_removed": removed_dupes,
            "appended_count": len(appended),
            "missing_newguild_ids": missing,
            "skipped_already_present": skipped_existing,
            "appended_map": appended_map,
        },
        "prd_context": {"error": prd_error, "head_sample": prd_head},
        "note": "tasks.json is SSoT; tasks_gameplay.json is updated as a planning view for next stage tasks.",
    }
    write_json(report_dir / "update_tasks_gameplay_report.json", report)
    print(f"[REPORT] {report_dir / 'update_tasks_gameplay_report.json'}")
    if args.dry_run:
        print("[OK] Dry run (no file writes).")
    else:
        print(f"[OK] Updated {tasks_gameplay_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
