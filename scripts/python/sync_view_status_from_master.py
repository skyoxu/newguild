#!/usr/bin/env python3
"""
Sync task status from master tasks.json into view task files.

Rules:
- Master SSoT: .taskmaster/tasks/tasks.json
- Views: .taskmaster/tasks/tasks_back.json, .taskmaster/tasks/tasks_gameplay.json
- Mapping: master.tasks[].id <-> view[].taskmaster_id

Outputs:
- Updates view files in-place (UTF-8, pretty JSON)
- Writes an audit report under logs/ci/<YYYY-MM-DD>/task-mapping/
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[2]
MASTER_PATH = REPO_ROOT / ".taskmaster" / "tasks" / "tasks.json"
VIEW_PATHS = [
    REPO_ROOT / ".taskmaster" / "tasks" / "tasks_back.json",
    REPO_ROOT / ".taskmaster" / "tasks" / "tasks_gameplay.json",
]


def _read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def _write_json_pretty(path: Path, obj: Any) -> None:
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _today_ci_dir() -> Path:
    return REPO_ROOT / "logs" / "ci" / dt.date.today().isoformat() / "task-mapping"


def _str_id(v: Any) -> str | None:
    if v is None:
        return None
    s = str(v).strip()
    return s if s else None


@dataclass(frozen=True)
class Change:
    view_file: str
    view_id: str | None
    taskmaster_id: str | None
    title: str | None
    old_status: str | None
    new_status: str | None


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true", help="Do not write view files, only report changes.")
    args = ap.parse_args()

    master_obj = _read_json(MASTER_PATH)
    master_tasks = master_obj.get("master", {}).get("tasks", [])
    if not isinstance(master_tasks, list):
        raise TypeError("tasks.json: master.tasks is not a list")

    master_status_by_id: dict[str, str] = {}
    master_title_by_id: dict[str, str] = {}
    for t in master_tasks:
        if not isinstance(t, dict):
            continue
        tid = _str_id(t.get("id"))
        if not tid:
            continue
        status = t.get("status")
        if isinstance(status, str):
            master_status_by_id[tid] = status
        title = t.get("title")
        if isinstance(title, str):
            master_title_by_id[tid] = title

    changes: list[Change] = []
    unmatched_view: list[dict[str, Any]] = []

    for view_path in VIEW_PATHS:
        view_obj = _read_json(view_path)
        if not isinstance(view_obj, list):
            raise TypeError(f"{view_path}: expected a JSON list")

        updated = False
        for vt in view_obj:
            if not isinstance(vt, dict):
                continue
            vmid = _str_id(vt.get("taskmaster_id"))
            if not vmid:
                unmatched_view.append(
                    {
                        "view_file": view_path.name,
                        "view_id": vt.get("id"),
                        "reason": "missing taskmaster_id",
                    }
                )
                continue
            if vmid not in master_status_by_id:
                unmatched_view.append(
                    {
                        "view_file": view_path.name,
                        "view_id": vt.get("id"),
                        "taskmaster_id": vmid,
                        "reason": "taskmaster_id not found in master",
                    }
                )
                continue

            new_status = master_status_by_id[vmid]
            old_status = vt.get("status") if isinstance(vt.get("status"), str) else None
            if old_status != new_status:
                changes.append(
                    Change(
                        view_file=view_path.name,
                        view_id=_str_id(vt.get("id")),
                        taskmaster_id=vmid,
                        title=vt.get("title") if isinstance(vt.get("title"), str) else master_title_by_id.get(vmid),
                        old_status=old_status,
                        new_status=new_status,
                    )
                )
                vt["status"] = new_status
                updated = True

        if updated and not args.dry_run:
            _write_json_pretty(view_path, view_obj)

    out_dir = _today_ci_dir()
    out_dir.mkdir(parents=True, exist_ok=True)
    report = {
        "generated": dt.datetime.now().isoformat(timespec="seconds"),
        "dry_run": bool(args.dry_run),
        "master_path": str(MASTER_PATH),
        "view_paths": [str(p) for p in VIEW_PATHS],
        "changes_count": len(changes),
        "unmatched_view_count": len(unmatched_view),
        "changes": [c.__dict__ for c in changes],
        "unmatched_view": unmatched_view,
        "note": "Status is synced from tasks.json(master) into view files by taskmaster_id mapping.",
    }
    (out_dir / "master-to-view-status-sync.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    (out_dir / "master-to-view-status-sync.txt").write_text(
        "\n".join(
            [
                f"generated: {report['generated']}",
                f"dry_run: {report['dry_run']}",
                f"changes: {report['changes_count']}",
                f"unmatched_view: {report['unmatched_view_count']}",
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    print(f"[REPORT] {out_dir / 'master-to-view-status-sync.json'}")
    print(f"[REPORT] {out_dir / 'master-to-view-status-sync.txt'}")
    print(f"[OK] changes={len(changes)} unmatched={len(unmatched_view)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

