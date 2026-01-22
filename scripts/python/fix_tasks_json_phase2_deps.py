from __future__ import annotations

import datetime as dt
import json
from pathlib import Path


def _load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def _write_json(path: Path, data) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main() -> int:
    repo_root = Path(__file__).resolve().parents[2]
    tasks_path = repo_root / ".taskmaster" / "tasks" / "tasks.json"
    doc = _load_json(tasks_path)
    tasks = doc.get("master", {}).get("tasks", [])
    if not isinstance(tasks, list):
        raise TypeError("tasks.json.master.tasks must be a list")

    today = dt.date.today().isoformat()
    now = dt.datetime.now(tz=dt.timezone.utc).isoformat()
    audit_dir = repo_root / "logs" / "ci" / today / "tasks-fix"
    audit_dir.mkdir(parents=True, exist_ok=True)

    backup_path = audit_dir / f"tasks.json.backup-{today}.json"
    _write_json(backup_path, doc)

    # Fix Phase-2 import dependency typo:
    # - T36 (Achievements) mistakenly depended on itself. It should depend on T35 (Reward Ledger).
    changes = []
    for t in tasks:
        if str(t.get("id")) != "36":
            continue
        before = list(t.get("dependencies") or [])
        after = ["35"]
        if before != after:
            t["dependencies"] = after
            t["updatedAt"] = now
            changes.append({"id": "36", "title": t.get("title"), "before": before, "after": after})

    doc["master"]["tasks"] = tasks
    _write_json(tasks_path, doc)

    report = {
        "ts": now,
        "tasks_path": str(tasks_path.as_posix()),
        "backup_path": str(backup_path.as_posix()),
        "changes": changes,
    }
    _write_json(audit_dir / "report.json", report)

    print(f"Updated: {tasks_path}")
    print(f"Backup : {backup_path}")
    print(f"Report : {audit_dir / 'report.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

