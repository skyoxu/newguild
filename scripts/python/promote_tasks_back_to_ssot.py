from __future__ import annotations

import datetime as dt
import json
from pathlib import Path


def _load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def _write_json(path: Path, data) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _next_task_id(tasks: list[dict]) -> int:
    ids: list[int] = []
    for t in tasks:
        try:
            ids.append(int(t.get("id")))
        except Exception:
            continue
    return (max(ids) if ids else 0) + 1


def main() -> int:
    repo_root = Path(__file__).resolve().parents[2]
    ssot_path = repo_root / ".taskmaster" / "tasks" / "tasks.json"
    back_path = repo_root / ".taskmaster" / "tasks" / "tasks_back.json"

    ssot = _load_json(ssot_path)
    back = _load_json(back_path)

    if not isinstance(ssot, dict) or "master" not in ssot:
        raise TypeError(f"Unexpected tasks.json shape at {ssot_path}")
    if not isinstance(back, list):
        raise TypeError(f"Unexpected tasks_back.json shape at {back_path}")

    tasks: list[dict] = ssot.get("master", {}).get("tasks", [])
    if not isinstance(tasks, list):
        raise TypeError("tasks.json.master.tasks must be a list")

    today = dt.date.today().isoformat()
    now = dt.datetime.now(tz=dt.timezone.utc).isoformat()
    audit_dir = repo_root / "logs" / "ci" / today / "tasks-back-promote"
    audit_dir.mkdir(parents=True, exist_ok=True)

    backup_tasks = audit_dir / f"tasks.json.backup-{today}.json"
    backup_back = audit_dir / f"tasks_back.json.backup-{today}.json"
    _write_json(backup_tasks, ssot)
    _write_json(backup_back, back)

    # Select only back tasks that materially reduce Phase-2 delivery risk.
    # Keep this list short to avoid scope explosion.
    promote_ids = [
        "NG-0023",  # event naming migration: eliminate legacy event types in code/tests
        "NG-0038",  # dependency guard: prevent Core/Godot coupling drift during Phase-2 expansion
    ]

    back_by_id = {str(e.get("id")): e for e in back}
    for pid in promote_ids:
        if pid not in back_by_id:
            raise KeyError(f"tasks_back missing entry id={pid}")

    # Idempotency: if tasks_back entry already has a numeric taskmaster_id, do not re-add.
    already_mapped = [pid for pid in promote_ids if str(back_by_id[pid].get("taskmaster_id", "")).isdigit()]

    # Idempotency: if tasks.json already has a Source-Back marker, do not re-add.
    existing_markers = set()
    for t in tasks:
        det = str(t.get("details", ""))
        for line in det.splitlines():
            if line.startswith("Source-Back:"):
                existing_markers.add(line.strip())

    def marker(back_id: str) -> str:
        return f"Source-Back: {back_id}"

    next_id = _next_task_id(tasks)
    appended = []
    mapped = []

    # Build new tasks.json entries from tasks_back content (English-only code/comments rule is respected:
    # tasks metadata is Chinese; code/tests are separate).
    for pid in promote_ids:
        b = back_by_id[pid]
        m = marker(pid)
        if m in existing_markers:
            appended.append({"back_id": pid, "skipped": True, "reason": "already-present-marker"})
            continue

        if str(b.get("taskmaster_id", "")).isdigit():
            appended.append({"back_id": pid, "skipped": True, "reason": "already-mapped", "taskmaster_id": int(b["taskmaster_id"])})
            continue

        # Minimal rewrite for SSOT format; keep ADR/CH/Overlay consistent.
        title = b.get("title", "")
        description = b.get("description", "")
        priority = b.get("priority", "P2")
        # Convert back view priority P1/P2/P3 to ssot priority high/medium/low.
        pr_map = {"P1": "high", "P2": "medium", "P3": "low"}
        ssot_prio = pr_map.get(str(priority).upper(), "medium")

        # Complexity: conservative defaults.
        complexity = 6 if pid in ("NG-0038",) else 4

        # Dependencies:
        # - NG-0023 should run before Phase-2 content-driven events/UI; depend on existing done core loop/event engine.
        # - NG-0038 depends on nothing but uses existing repo structure; safe to run early.
        if pid == "NG-0023":
            deps = ["3", "4"]  # Event engine + game loop already done
        elif pid == "NG-0038":
            deps = ["2"]  # three-layer skeleton already landed
        else:
            deps = []

        ssot_task = {
            "id": str(next_id),
            "title": title,
            "description": description,
            "details": "\n".join(
                [
                    f"Story: {b.get('story_id') or 'PHASE2-BACK-PROMOTED'}",
                    f"ADR Refs: {'; '.join(list(b.get('adr_refs') or []))}",
                    f"Chapters: {'; '.join(list(b.get('chapter_refs') or []))}",
                    f"Overlays: {'; '.join(list(b.get('overlay_refs') or []))}",
                    marker(pid),
                    "Rewrite-Intent: Promote backlog task into SSoT to be implemented in the current phase.",
                ]
            ),
            "testStrategy": "TDD (red->green->refactor). Keep Game.Core free of Godot API. Write artifacts under logs/**.",
            "adrRefs": list(b.get("adr_refs") or []),
            "archRefs": list(b.get("chapter_refs") or []),
            "overlay": (list(b.get("overlay_refs") or [])[:1] or ["docs/architecture/overlays/PRD-Guild-Manager/08/_index.md"])[0],
            "priority": ssot_prio,
            "complexity": complexity,
            "dependencies": deps,
            "status": "pending",
            "subtasks": [],
            "recommendedSubtasks": 0,
            "updatedAt": now,
        }

        tasks.append(ssot_task)
        # Back view now maps to the new SSOT id.
        b["taskmaster_id"] = int(ssot_task["id"])
        b["taskmaster_exported"] = False
        mapped.append({"back_id": pid, "taskmaster_id": int(ssot_task["id"]), "title": title})
        appended.append({"back_id": pid, "new_task_id": int(ssot_task["id"]), "title": title})
        next_id += 1

    ssot["master"]["tasks"] = tasks
    _write_json(ssot_path, ssot)
    _write_json(back_path, back)

    report = {
        "ts": now,
        "paths": {
            "tasks_json": str(ssot_path.as_posix()),
            "tasks_back": str(back_path.as_posix()),
            "tasks_json_backup": str(backup_tasks.as_posix()),
            "tasks_back_backup": str(backup_back.as_posix()),
        },
        "selected_back_ids": promote_ids,
        "already_mapped": already_mapped,
        "results": appended,
        "back_mappings": mapped,
        "notes": [
            "Only selected backlog items were promoted to avoid scope explosion.",
            "Back view task IDs (NG-xxxx) were preserved; mapping is done via taskmaster_id only.",
        ],
    }
    _write_json(audit_dir / "report.json", report)
    (audit_dir / "report.md").write_text(
        "\n".join(
            [
                "# tasks_back -> tasks.json promotion report",
                "",
                f"- Date: {today}",
                f"- Promoted: {sum(1 for r in appended if not r.get('skipped'))}",
                f"- Skipped: {sum(1 for r in appended if r.get('skipped'))}",
                "",
                "## Mappings",
                "",
            ]
            + [f"- {m['back_id']} -> tasks.json T{m['taskmaster_id']}: {m['title']}" for m in mapped]
            + [""]
        )
        + "\n",
        encoding="utf-8",
    )

    print(f"Updated: {ssot_path}")
    print(f"Updated: {back_path}")
    print(f"Report : {audit_dir / 'report.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

