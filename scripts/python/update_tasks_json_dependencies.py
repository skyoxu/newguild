from __future__ import annotations

import argparse
import datetime as dt
import json
from pathlib import Path


def _load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def _write_json(path: Path, data) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _ensure_list(values) -> list[str]:
    if values is None:
        return []
    if isinstance(values, list):
        return [str(v) for v in values]
    return [str(values)]


def main() -> int:
    parser = argparse.ArgumentParser(description="Update dependencies for a task in .taskmaster/tasks/tasks.json (SSoT).")
    parser.add_argument("--task-id", type=int, required=True)
    parser.add_argument("--add", action="append", default=[], help="Dependency task id to add (repeatable).")
    parser.add_argument("--remove", action="append", default=[], help="Dependency task id to remove (repeatable).")
    args = parser.parse_args()

    repo_root = Path(__file__).resolve().parents[2]
    tasks_path = repo_root / ".taskmaster" / "tasks" / "tasks.json"
    doc = _load_json(tasks_path)
    tasks = doc.get("master", {}).get("tasks", [])
    if not isinstance(tasks, list):
        raise TypeError("tasks.json.master.tasks must be a list")

    today = dt.date.today().isoformat()
    now = dt.datetime.now(tz=dt.timezone.utc).isoformat()
    audit_dir = repo_root / "logs" / "ci" / today / "tasks-deps"
    audit_dir.mkdir(parents=True, exist_ok=True)

    backup_path = audit_dir / f"tasks.json.backup-{today}.json"
    _write_json(backup_path, doc)

    target_id = str(args.task_id)
    add = [str(x) for x in args.add]
    remove = {str(x) for x in args.remove}

    change = None
    for t in tasks:
        if str(t.get("id")) != target_id:
            continue
        before = _ensure_list(t.get("dependencies"))
        after = [d for d in before if d not in remove]
        for d in add:
            if d not in after:
                after.append(d)
        t["dependencies"] = after
        t["updatedAt"] = now
        change = {"id": target_id, "title": t.get("title"), "before": before, "after": after}
        break

    if not change:
        raise KeyError(f"Task id {target_id} not found in tasks.json")

    doc["master"]["tasks"] = tasks
    _write_json(tasks_path, doc)

    report = {
        "ts": now,
        "task_id": int(target_id),
        "paths": {"tasks_json": str(tasks_path.as_posix()), "backup": str(backup_path.as_posix())},
        "change": change,
    }
    _write_json(audit_dir / "report.json", report)

    print(f"Updated: {tasks_path}")
    print(f"Backup : {backup_path}")
    print(f"Report : {audit_dir / 'report.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

