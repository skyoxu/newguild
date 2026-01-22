from __future__ import annotations

import argparse
import datetime as dt
import json
from pathlib import Path


def _load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def _write_json(path: Path, data) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Update .taskmaster/tasks/tasks.json task statuses (SSoT).")
    parser.add_argument("--task-id", action="append", type=int, required=True, help="Task ID to update (repeatable).")
    parser.add_argument("--status", required=True, help="New status (e.g., done, pending, in-progress).")
    args = parser.parse_args()

    repo_root = Path(__file__).resolve().parents[2]
    tasks_path = repo_root / ".taskmaster" / "tasks" / "tasks.json"
    tasks_doc = _load_json(tasks_path)
    if not isinstance(tasks_doc, dict) or "master" not in tasks_doc:
        raise TypeError(f"Unexpected tasks.json shape at {tasks_path}")
    tasks = tasks_doc.get("master", {}).get("tasks", [])
    if not isinstance(tasks, list):
        raise TypeError("tasks.json.master.tasks must be a list")

    today = dt.date.today().isoformat()
    audit_dir = repo_root / "logs" / "ci" / today / "tasks-status"
    audit_dir.mkdir(parents=True, exist_ok=True)

    backup_path = audit_dir / f"tasks.backup-{today}.json"
    _write_json(backup_path, tasks_doc)

    wanted_ids = {int(x) for x in args.task_id}
    new_status = str(args.status)
    updated_at = dt.datetime.now(tz=dt.timezone.utc).isoformat()

    changes = []
    found = set()
    for t in tasks:
        try:
            tid = int(t.get("id"))
        except Exception:
            continue
        if tid not in wanted_ids:
            continue
        before = t.get("status")
        if before == new_status:
            found.add(tid)
            continue
        t["status"] = new_status
        t["updatedAt"] = updated_at
        changes.append(
            {
                "id": tid,
                "title": t.get("title"),
                "status_before": before,
                "status_after": new_status,
            }
        )
        found.add(tid)

    missing = sorted(wanted_ids - found)
    report = {
        "ts": updated_at,
        "tasks_path": str(tasks_path.as_posix()),
        "backup_path": str(backup_path.as_posix()),
        "requested": {"ids": sorted(wanted_ids), "status": new_status},
        "changes": changes,
        "missing_ids": missing,
    }
    _write_json(audit_dir / "report.json", report)

    if missing:
        # Still write changes, but make it obvious in output.
        print(f"[WARN] Missing task ids in tasks.json: {missing}")

    _write_json(tasks_path, tasks_doc)
    print(f"Updated: {tasks_path}")
    print(f"Backup : {backup_path}")
    print(f"Report : {audit_dir / 'report.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
