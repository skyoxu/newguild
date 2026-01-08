#!/usr/bin/env python3
"""
Migrate view task files field name: contract_refs -> contractRefs.

Why:
- The workflow/scripts copied from the sibling project expect `contractRefs` (camelCase).
- This repo previously used `contract_refs` in view files; we now align to `contractRefs`.

Scope:
- `.taskmaster/tasks/tasks_back.json`
- `.taskmaster/tasks/tasks_gameplay.json`

Behavior (idempotent):
- If a task has only `contract_refs`, rename to `contractRefs`.
- If a task has both, merge (preserve `contractRefs` order, append missing items from `contract_refs`).
- If neither exists, add `contractRefs: []` to keep schema consistent.
- Removes `contract_refs` in all cases.
- Also updates human text mentions "(contract_refs)" -> "(contractRefs)" inside `acceptance` and `test_strategy`.

Outputs:
- `logs/ci/<YYYY-MM-DD>/task-mapping/view-contractRefs-migration.json`
- `logs/ci/<YYYY-MM-DD>/task-mapping/view-contractRefs-migration.txt`

All reads/writes are UTF-8.
"""

from __future__ import annotations

import datetime as dt
import json
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[2]
VIEW_PATHS = [
    REPO_ROOT / ".taskmaster" / "tasks" / "tasks_back.json",
    REPO_ROOT / ".taskmaster" / "tasks" / "tasks_gameplay.json",
]


def _read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def _write_json_pretty(path: Path, obj: Any) -> None:
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _today_ci_dir() -> Path:
    out_dir = REPO_ROOT / "logs" / "ci" / dt.date.today().isoformat() / "task-mapping"
    out_dir.mkdir(parents=True, exist_ok=True)
    return out_dir


def _ensure_list(value: Any) -> list[str]:
    if value is None:
        return []
    if isinstance(value, list):
        out: list[str] = []
        for x in value:
            if x is None:
                continue
            s = str(x).strip()
            if s:
                out.append(s)
        return out
    # If someone put a scalar by mistake, coerce to single-item list.
    s = str(value).strip()
    return [s] if s else []


def _merge_preserve_left(left: list[str], right: list[str]) -> list[str]:
    seen = set(left)
    out = list(left)
    for x in right:
        if x not in seen:
            out.append(x)
            seen.add(x)
    return out


def _replace_text_fields(task: dict[str, Any], changes: list[str]) -> None:
    for key in ("acceptance", "test_strategy"):
        val = task.get(key)
        if not isinstance(val, list):
            continue
        updated = False
        new_list: list[str] = []
        for item in val:
            if not isinstance(item, str):
                new_list.append(item)
                continue
            if "contract_refs" in item:
                updated = True
                new_list.append(item.replace("contract_refs", "contractRefs"))
            else:
                new_list.append(item)
        if updated:
            task[key] = new_list
            changes.append(f"text:{key}")


def _migrate_view(path: Path) -> dict[str, Any]:
    view_obj = _read_json(path)
    if not isinstance(view_obj, list):
        raise TypeError(f"{path}: expected a JSON list")

    task_changes: list[dict[str, Any]] = []
    for t in view_obj:
        if not isinstance(t, dict):
            continue

        changes: list[str] = []
        old_snake = t.get("contract_refs", None)
        old_camel = t.get("contractRefs", None)

        snake_list = _ensure_list(old_snake)
        camel_list = _ensure_list(old_camel)

        if "contractRefs" not in t and "contract_refs" not in t:
            t["contractRefs"] = []
            changes.append("add:contractRefs_default_empty")
        elif "contractRefs" not in t and "contract_refs" in t:
            t["contractRefs"] = snake_list
            changes.append("rename:contract_refs->contractRefs")
        elif "contractRefs" in t and "contract_refs" in t:
            merged = _merge_preserve_left(camel_list, snake_list)
            if merged != camel_list:
                t["contractRefs"] = merged
                changes.append("merge:contract_refs_into_contractRefs")
            else:
                # Still record that we removed the old field.
                changes.append("remove:contract_refs")
        else:
            # contractRefs already exists (ensure list normalization)
            if camel_list != old_camel:
                t["contractRefs"] = camel_list
                changes.append("normalize:contractRefs_list")

        if "contract_refs" in t:
            del t["contract_refs"]

        _replace_text_fields(t, changes)

        if changes:
            task_changes.append(
                {
                    "id": t.get("id"),
                    "taskmaster_id": t.get("taskmaster_id"),
                    "changes": changes,
                }
            )

    _write_json_pretty(path, view_obj)
    return {
        "view_file": path.name,
        "tasks_total": len(view_obj),
        "tasks_changed": len(task_changes),
        "task_changes": task_changes,
    }


def main() -> int:
    results: list[dict[str, Any]] = []
    for path in VIEW_PATHS:
        results.append(_migrate_view(path))

    out_dir = _today_ci_dir()
    report = {
        "generated": dt.datetime.now().isoformat(timespec="seconds"),
        "results": results,
    }
    out_json = out_dir / "view-contractRefs-migration.json"
    out_txt = out_dir / "view-contractRefs-migration.txt"
    _write_json_pretty(out_json, report)
    out_txt.write_text(
        "\n".join(
            [
                f"generated: {report['generated']}",
                *[f"{r['view_file']}: changed={r['tasks_changed']}/{r['tasks_total']}" for r in results],
                "",
            ]
        ),
        encoding="utf-8",
    )
    print(f"[REPORT] {out_json}")
    print(f"[REPORT] {out_txt}")
    print("[OK] Migrated contract_refs -> contractRefs in view task files.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
