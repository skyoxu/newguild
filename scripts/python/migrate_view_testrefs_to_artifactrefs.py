#!/usr/bin/env python3
"""
Migrate log/artifact paths out of `test_refs` into `artifactRefs` in view task files.

Why:
- `test_refs` should be "must exist" references (tests and/or source files).
- `artifactRefs` is for gate artifacts / outputs and MAY contain placeholders like `<date>` / `<YYYY-MM-DD>`.
- Keeping `logs/**` inside `test_refs` makes "existence semantics" inconsistent and causes future hard gates to misfire.

Scope:
- `.taskmaster/tasks/tasks_back.json`
- `.taskmaster/tasks/tasks_gameplay.json`

Behavior (idempotent):
- Any `test_refs` entry that looks like an output artifact is moved to `artifactRefs`.
  Current heuristic: starts with `logs/` (or `logs\\`) OR contains `logs/` as a prefix after normalization.
- Keeps ordering stable (appends moved items to `artifactRefs` if not already present).

Artifacts:
- `logs/ci/<YYYY-MM-DD>/task-mapping/testrefs-to-artifactrefs-migration.json`
"""

from __future__ import annotations

import datetime as dt
import json
from pathlib import Path
from typing import Any


VIEW_FILES = [
    ".taskmaster/tasks/tasks_back.json",
    ".taskmaster/tasks/tasks_gameplay.json",
]


def today_str() -> str:
    return dt.date.today().strftime("%Y-%m-%d")


def write_json(path: Path, obj: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")


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
            s = str(item).strip()
            if s:
                out.append(s)
        return out
    s = str(value).strip()
    return [s] if s else []


def is_log_path(s: str) -> bool:
    v = s.strip().replace("\\", "/")
    return v.startswith("logs/")


def merge_unique(existing: list[str], to_add: list[str]) -> list[str]:
    seen = set(existing)
    merged = list(existing)
    for item in to_add:
        if item not in seen:
            merged.append(item)
            seen.add(item)
    return merged


def main() -> int:
    root = Path(__file__).resolve().parents[2]
    out_dir = root / "logs" / "ci" / today_str() / "task-mapping"
    out_dir.mkdir(parents=True, exist_ok=True)

    report: dict[str, Any] = {
        "date": today_str(),
        "files": [],
        "rule": "Move test_refs entries starting with logs/ into artifactRefs (placeholders allowed there).",
    }

    for rel in VIEW_FILES:
        path = root / rel
        tasks = load_view_tasks(path)
        changes: list[dict[str, Any]] = []
        new_tasks: list[dict[str, Any]] = []

        for t in tasks:
            test_refs = normalize_list(t.get("test_refs"))
            artifact_refs = normalize_list(t.get("artifactRefs"))

            moved = [r for r in test_refs if is_log_path(r)]
            kept = [r for r in test_refs if r not in moved]

            if moved:
                merged_artifacts = merge_unique(artifact_refs, moved)
                t2 = dict(t)
                t2["test_refs"] = kept
                t2["artifactRefs"] = merged_artifacts
                changes.append(
                    {
                        "id": t.get("id"),
                        "taskmaster_id": t.get("taskmaster_id"),
                        "moved_count": len(moved),
                        "moved": moved,
                    }
                )
                new_tasks.append(t2)
            else:
                new_tasks.append(t)

        if changes:
            path.write_text(json.dumps(new_tasks, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")

        report["files"].append({"path": rel, "changed_tasks": len(changes), "changes": changes})

    write_json(out_dir / "testrefs-to-artifactrefs-migration.json", report)
    print("[OK] Migrated logs/** out of test_refs into artifactRefs.")
    print(f"[LOG] {out_dir / 'testrefs-to-artifactrefs-migration.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

