from __future__ import annotations

import datetime as _dt
import json
from pathlib import Path


def _load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def _write_json(path: Path, data) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main() -> int:
    repo_root = Path(__file__).resolve().parents[2]
    tasks_newguild_path = repo_root / ".taskmaster" / "tasks" / "tasks_newguild.json"
    tasks_ssot_path = repo_root / ".taskmaster" / "tasks" / "tasks.json"

    tasks_newguild = _load_json(tasks_newguild_path)
    tasks_ssot = _load_json(tasks_ssot_path)

    if not isinstance(tasks_newguild, list):
        raise TypeError(f"Expected list in {tasks_newguild_path}, got {type(tasks_newguild).__name__}")
    if not isinstance(tasks_ssot, dict) or "master" not in tasks_ssot:
        raise TypeError(f"Expected dict with master in {tasks_ssot_path}, got {type(tasks_ssot).__name__}")

    ssot_tasks = tasks_ssot.get("master", {}).get("tasks", [])
    if not isinstance(ssot_tasks, list):
        raise TypeError("Expected tasks.json.master.tasks to be a list")

    ssot_done = [t for t in ssot_tasks if str(t.get("status", "")).lower() == "done"]
    ssot_in_progress = [t for t in ssot_tasks if str(t.get("status", "")).lower() == "in-progress"]
    ssot_pending = [t for t in ssot_tasks if str(t.get("status", "")).lower() == "pending"]

    # Conservative coverage map: only mark tasks_newguild entries as done when
    # they clearly correspond to already "done" items in tasks.json (SSoT).
    #
    # NOTE: IDs differ between the two files; this mapping is explicit and audited via logs.
    mark_done_map: dict[int, dict] = {
        1: {"ssot_ids": [2, 6], "reason": "Project environment + C# + UI baseline already landed in the first vertical slice."},
        2: {"ssot_ids": [3], "reason": "Event engine core implemented and verified via existing CI/tests."},
        3: {"ssot_ids": [15, 16], "reason": "AI ecosystem + coordinator already implemented and wired."},
        4: {"ssot_ids": [12, 25], "reason": "SQLite integration + schema migration path already implemented (WAL guarded in adapter)."},
        5: {"ssot_ids": [25, 26], "reason": "Save/Load implementation + UI entry already implemented."},
        7: {"ssot_ids": [24], "reason": "Sentry release health gate + observability autoload already implemented."},
        11: {"ssot_ids": [4], "reason": "Core game loop / time advancement already implemented."},
        13: {"ssot_ids": [15], "reason": "NPC guild / AI ecosystem already implemented."},
        16: {"ssot_ids": [17], "reason": "PVE raid encounter demo/state-machine already implemented and observable."},
        18: {"ssot_ids": [13], "reason": "Member management UI/roster already implemented and smoke-tested."},
    }

    today = _dt.date.today().isoformat()
    audit_dir = repo_root / "logs" / "ci" / today / "tasks-prune"
    audit_dir.mkdir(parents=True, exist_ok=True)

    backup_path = audit_dir / f"tasks_newguild.backup-{today}.json"
    _write_json(backup_path, tasks_newguild)

    changed = []
    for t in tasks_newguild:
        try:
            tid = int(t.get("id"))
        except Exception:
            continue

        if tid not in mark_done_map:
            continue

        before = t.get("status")
        after = "done"
        if str(before).lower() == after:
            continue

        t["status"] = after
        changed.append(
            {
                "tasks_newguild_id": tid,
                "tasks_newguild_title": t.get("title"),
                "status_before": before,
                "status_after": after,
                "covered_by_tasks_json_ids": mark_done_map[tid]["ssot_ids"],
                "reason": mark_done_map[tid]["reason"],
            }
        )

    _write_json(tasks_newguild_path, tasks_newguild)

    report = {
        "ts": _dt.datetime.now(tz=_dt.timezone.utc).isoformat(),
        "paths": {
            "tasks_newguild": str(tasks_newguild_path.as_posix()),
            "tasks_newguild_backup": str(backup_path.as_posix()),
            "tasks_ssot": str(tasks_ssot_path.as_posix()),
        },
        "ssot_summary": {
            "done_count": len(ssot_done),
            "in_progress_count": len(ssot_in_progress),
            "pending_count": len(ssot_pending),
        },
        "changes": changed,
        "notes": [
            "Only a conservative subset was marked done to avoid over-claiming broader backlog items (e.g. DLC, negotiations, legendary recruitment).",
            "Use tasks.json as SSoT; tasks_newguild.json remains a source/backlog and may contain broader requirements than the current stage.",
        ],
    }
    _write_json(audit_dir / "report.json", report)

    md_lines = [
        "# tasks_newguild cleanup report",
        "",
        f"- Date: {today}",
        f"- tasks_newguild backup: `{backup_path.as_posix()}`",
        f"- Changes applied: {len(changed)}",
        "",
        "## Marked as done",
        "",
    ]
    if not changed:
        md_lines.append("- (none)")
    else:
        for c in changed:
            md_lines.append(
                f"- T{c['tasks_newguild_id']}: {c['tasks_newguild_title']} -> done (covered by tasks.json: {c['covered_by_tasks_json_ids']})"
            )
    md_lines.append("")
    (audit_dir / "report.md").write_text("\n".join(md_lines) + "\n", encoding="utf-8")

    print(f"Updated: {tasks_newguild_path}")
    print(f"Backup : {backup_path}")
    print(f"Report : {audit_dir / 'report.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
