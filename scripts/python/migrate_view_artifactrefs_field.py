#!/usr/bin/env python3
"""
Ensure `artifactRefs` field exists in view task files.

Why:
- We keep `contractRefs` focused on *domain contracts* (Game.Core/Contracts/** + Overlay 08 contracts pages).
- We add `artifactRefs` to reference gate artifacts (scripts, log schemas, JSONL formats, CI outputs),
  so CI/observability/performance tasks don't have to overload `contractRefs`.

Scope:
- `.taskmaster/tasks/tasks_back.json`
- `.taskmaster/tasks/tasks_gameplay.json`

Behavior (idempotent):
- If a task is missing `artifactRefs`, add `artifactRefs: []` (inserted right after `contractRefs` when present).
- Optionally fill known `artifactRefs` for a small set of gate tasks (taskmaster_id 20..24) in tasks_back.json.

Artifacts (forensics):
- `logs/ci/<YYYY-MM-DD>/task-mapping/view-artifactRefs-migration.json`
- `logs/ci/<YYYY-MM-DD>/task-mapping/view-artifactRefs-migration.txt`
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
from pathlib import Path
from typing import Any


VIEW_FILES = [
    ".taskmaster/tasks/tasks_back.json",
    ".taskmaster/tasks/tasks_gameplay.json",
]


FILL_BY_TASKMASTER_ID: dict[int, list[str]] = {
    # 20..24 are CI/observability/performance tasks in tasks_back.json (gate artifacts).
    20: [
        "scripts/python/perf_smoke_db.py",
        "scripts/python/quality_gates.py",
        ".github/workflows/windows-quality-gate.yml",
        "logs/perf/<YYYY-MM-DD>/db/db-perf-summary.json",
        "logs/perf/<YYYY-MM-DD>/db/run.json",
        "logs/perf/<YYYY-MM-DD>/db/gdunit.log",
    ],
    21: [
        "docs/architecture/base/03-observability-sentry-logging-v2.md",
        "docs/adr/ADR-0003-observability-release-health.md",
    ],
    22: [
        "scripts/python/task_links_validate.py",
        "scripts/python/check_tasks_all_refs.py",
        "scripts/python/check_tasks_back_references.py",
        "scripts/python/validate_task_overlays.py",
        "scripts/sc/acceptance_check.py",
        "logs/ci/<YYYY-MM-DD>/sc-acceptance-check/task-links-validate.log",
        "logs/ci/<YYYY-MM-DD>/sc-acceptance-check/validate-task-overlays.log",
        "logs/ci/<YYYY-MM-DD>/sc-acceptance-check/adr-compliance.json",
    ],
    23: [
        "scripts/python/perf_smoke_db.py",
        "scripts/python/validate_audit_logs.py",
        "scripts/python/quality_gates.py",
        "logs/ci/<YYYY-MM-DD>/security-audit.jsonl",
        "logs/perf/<YYYY-MM-DD>/db/db-perf-summary.json",
        "logs/perf/<YYYY-MM-DD>/db/run.json",
    ],
    24: [
        "scripts/python/check_sentry_secrets.py",
        "scripts/python/release_health_gate.py",
        ".release-health.json",
        ".github/workflows/windows-release.yml",
        ".github/workflows/windows-release-tag.yml",
        "logs/ci/<YYYY-MM-DD>/release-health.json",
    ],
}


def today_str() -> str:
    return dt.date.today().strftime("%Y-%m-%d")


def write_text(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8", newline="\n")


def write_json(path: Path, obj: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    write_text(path, json.dumps(obj, ensure_ascii=False, indent=2))


def load_view_tasks(path: Path) -> list[dict[str, Any]]:
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, list):
        raise ValueError(f"Unsupported view schema: {path} (expected list root)")
    out: list[dict[str, Any]] = []
    for i, t in enumerate(data):
        if not isinstance(t, dict):
            raise ValueError(f"Invalid task item: {path}[{i}] is {type(t)}")
        out.append(t)
    return out


def normalize_list(value: Any) -> list[str]:
    if value is None:
        return []
    if isinstance(value, list):
        out: list[str] = []
        for item in value:
            if item is None:
                continue
            s = str(item).strip()
            if not s:
                continue
            out.append(s)
        return out
    return [str(value).strip()] if str(value).strip() else []


def merge_unique(existing: list[str], desired: list[str]) -> list[str]:
    seen = set(existing)
    merged = list(existing)
    for item in desired:
        if item not in seen:
            merged.append(item)
            seen.add(item)
    return merged


def insert_after_key(d: dict[str, Any], key: str, insert_key: str, insert_value: Any) -> dict[str, Any]:
    if insert_key in d:
        return dict(d)
    out: dict[str, Any] = {}
    inserted = False
    for k, v in d.items():
        out[k] = v
        if k == key:
            out[insert_key] = insert_value
            inserted = True
    if not inserted:
        out[insert_key] = insert_value
    return out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--fill-known", action="store_true", help="Fill known artifactRefs for taskmaster_id 20..24 in tasks_back.json")
    args = ap.parse_args()

    root = Path(__file__).resolve().parents[2]
    out_dir = root / "logs" / "ci" / today_str() / "task-mapping"
    out_dir.mkdir(parents=True, exist_ok=True)

    report: dict[str, Any] = {
        "date": today_str(),
        "files": [],
        "notes": [
            "artifactRefs is a view-only field (gate artifacts), contractRefs remains for domain contracts only.",
        ],
    }
    txt_lines: list[str] = []

    for rel in VIEW_FILES:
        path = root / rel
        tasks = load_view_tasks(path)
        file_changes: list[dict[str, Any]] = []
        new_tasks: list[dict[str, Any]] = []

        for t in tasks:
            before_has = "artifactRefs" in t
            t2 = insert_after_key(t, "contractRefs", "artifactRefs", normalize_list(t.get("artifactRefs")))

            changes: list[str] = []
            if not before_has:
                changes.append("add:artifactRefs_default_empty")

            if args.fill_known and rel.endswith("tasks_back.json"):
                tm_id = t2.get("taskmaster_id")
                if isinstance(tm_id, int) and tm_id in FILL_BY_TASKMASTER_ID:
                    desired = FILL_BY_TASKMASTER_ID[tm_id]
                    cur = normalize_list(t2.get("artifactRefs"))
                    merged = merge_unique(cur, desired)
                    if merged != cur:
                        t2["artifactRefs"] = merged
                        changes.append("fill:artifactRefs_known")

            if changes:
                file_changes.append(
                    {
                        "id": t2.get("id"),
                        "taskmaster_id": t2.get("taskmaster_id"),
                        "changes": changes,
                    }
                )
                txt_lines.append(f"{rel}: {t2.get('id')} ({t2.get('taskmaster_id')}): {', '.join(changes)}")

            new_tasks.append(t2)

        if file_changes:
            path.write_text(json.dumps(new_tasks, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")

        report["files"].append(
            {
                "path": rel,
                "changes": file_changes,
                "changed_count": len(file_changes),
                "task_count": len(tasks),
            }
        )

    write_json(out_dir / "view-artifactRefs-migration.json", report)
    write_text(out_dir / "view-artifactRefs-migration.txt", "\n".join(txt_lines) + ("\n" if txt_lines else ""))

    print("[OK] Ensured artifactRefs in view task files.")
    print(f"[LOG] {out_dir / 'view-artifactRefs-migration.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

