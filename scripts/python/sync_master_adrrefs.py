#!/usr/bin/env python3
"""
Synchronize adrRefs in .taskmaster/tasks/tasks.json (master view).

Why:
- Repo tooling (e.g., scripts/sc/git.py smart-commit) reads adrRefs from the
  structured field, not from free-form "details" text.
- tasks_back.json/tasks_gameplay.json already carry adr_refs, but master tasks
  may have adrRefs=null which breaks automation.

This script updates only tasks whose master.adrRefs is missing/null/non-list.
Sources (in priority order):
1) tasks_back.json adr_refs for matching taskmaster_id
2) tasks_gameplay.json adr_refs for matching taskmaster_id
3) Parse "ADR Refs:" line from master.details (semicolon-separated)

Outputs forensic artifacts under logs/ci/<YYYY-MM-DD>/taskmaster-adrrefs-sync/.

Usage (Windows):
  py -3 scripts/python/sync_master_adrrefs.py --apply
  py -3 scripts/python/sync_master_adrrefs.py --dry-run
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import re
from pathlib import Path
from typing import Any, Iterable


def repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")


def write_json(path: Path, obj: Any) -> None:
    write_text(path, json.dumps(obj, ensure_ascii=False, indent=2) + "\n")


def normalize_adr_ids(items: Iterable[str]) -> list[str]:
    out: list[str] = []
    for raw in items:
        s = str(raw).strip()
        if not s:
            continue
        out.append(s)
    # stable unique, keep order
    seen: set[str] = set()
    result: list[str] = []
    for s in out:
        if s not in seen:
            seen.add(s)
            result.append(s)
    return result


def adr_refs_from_details(details: str) -> list[str]:
    for line in (details or "").splitlines():
        if line.startswith("ADR Refs:"):
            tail = line.replace("ADR Refs:", "", 1).strip()
            parts = [p.strip() for p in re.split(r"[;,]", tail) if p.strip()]
            return normalize_adr_ids(parts)
    return []


def index_view_tasks(view: Any) -> dict[int, dict[str, Any]]:
    if not isinstance(view, list):
        return {}
    out: dict[int, dict[str, Any]] = {}
    for t in view:
        if not isinstance(t, dict):
            continue
        tid = t.get("taskmaster_id")
        if isinstance(tid, int):
            out[tid] = t
    return out


def get_view_adr_refs(view_task: dict[str, Any] | None) -> list[str]:
    if not view_task:
        return []
    v = view_task.get("adr_refs")
    if isinstance(v, list):
        return normalize_adr_ids([str(x) for x in v])
    return []


def should_update(master_task: dict[str, Any]) -> bool:
    v = master_task.get("adrRefs")
    return not isinstance(v, list) or len(v) == 0


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true", help="write changes back to tasks.json")
    ap.add_argument("--dry-run", action="store_true", help="do not write changes back to tasks.json")
    args = ap.parse_args()

    if args.apply and args.dry_run:
        raise SystemExit("Use either --apply or --dry-run, not both.")
    apply = args.apply and not args.dry_run

    root = repo_root()
    date = dt.date.today().strftime("%Y-%m-%d")
    out_dir = root / "logs" / "ci" / date / "taskmaster-adrrefs-sync"
    out_dir.mkdir(parents=True, exist_ok=True)

    master_path = root / ".taskmaster" / "tasks" / "tasks.json"
    back_path = root / ".taskmaster" / "tasks" / "tasks_back.json"
    gameplay_path = root / ".taskmaster" / "tasks" / "tasks_gameplay.json"

    master_obj = load_json(master_path)
    tasks = master_obj.get("master", {}).get("tasks", [])
    if not isinstance(tasks, list):
        raise SystemExit("Invalid tasks.json structure: master.tasks must be a list.")

    back_idx = index_view_tasks(load_json(back_path) if back_path.exists() else None)
    gameplay_idx = index_view_tasks(load_json(gameplay_path) if gameplay_path.exists() else None)

    changes: list[dict[str, Any]] = []
    missing: list[str] = []

    for t in tasks:
        if not isinstance(t, dict):
            continue
        tid = str(t.get("id") or "").strip()
        if not tid.isdigit():
            continue
        if not should_update(t):
            continue

        tid_i = int(tid)
        from_back = get_view_adr_refs(back_idx.get(tid_i))
        from_gameplay = get_view_adr_refs(gameplay_idx.get(tid_i))
        from_details = adr_refs_from_details(str(t.get("details") or ""))

        merged = normalize_adr_ids([*from_back, *from_gameplay, *from_details])
        if not merged:
            missing.append(tid)
            continue

        old = t.get("adrRefs")
        t["adrRefs"] = merged
        changes.append(
            {
                "task_id": tid,
                "old": old,
                "new": merged,
                "sources": {
                    "tasks_back": from_back,
                    "tasks_gameplay": from_gameplay,
                    "details": from_details,
                },
            }
        )

    report = {
        "generated": dt.datetime.now(dt.timezone.utc).isoformat(),
        "apply": bool(apply),
        "paths": {
            "tasks_json": str(master_path).replace("\\", "/"),
            "tasks_back": str(back_path).replace("\\", "/"),
            "tasks_gameplay": str(gameplay_path).replace("\\", "/"),
        },
        "changed_tasks": len(changes),
        "missing_tasks": missing,
        "changes": changes,
    }

    write_json(out_dir / "report.json", report)
    write_text(
        out_dir / "report.txt",
        "\n".join(
            [
                f"apply={apply}",
                f"changed_tasks={len(changes)}",
                f"missing_tasks={len(missing)}",
            ]
        )
        + "\n",
    )

    if apply and changes:
        master_path.write_text(json.dumps(master_obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    print(f"[REPORT] {out_dir / 'report.json'}")
    print(f"[REPORT] {out_dir / 'report.txt'}")
    if missing:
        print("[WARN] Some tasks still have empty adrRefs; see report.json for details.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

