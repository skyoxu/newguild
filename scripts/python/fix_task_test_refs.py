#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Fix test_refs for a single view task (tasks_back.json or tasks_gameplay.json).

This is used to keep view.test_refs scoped to what the task actually verifies,
so deterministic checks (anchors/contractRefs coverage) do not get polluted by
unrelated aggregate tests.
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[2]


def _today() -> str:
    return dt.date.today().strftime("%Y-%m-%d")


def _read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def _write_json(path: Path, obj: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")


def _write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")


def _view_path(view: str) -> Path:
    if view == "back":
        return REPO_ROOT / ".taskmaster" / "tasks" / "tasks_back.json"
    if view == "gameplay":
        return REPO_ROOT / ".taskmaster" / "tasks" / "tasks_gameplay.json"
    raise ValueError(f"Unsupported view: {view}")


def _normalize(paths: list[str]) -> list[str]:
    out: list[str] = []
    seen: set[str] = set()
    for p in paths:
        s = str(p).strip()
        if not s:
            continue
        if s in seen:
            continue
        seen.add(s)
        out.append(s)
    return out


@dataclass(frozen=True)
class FixReport:
    task_id: int
    view: str
    path: str
    removed: list[str]
    added: list[str]
    before: list[str]
    after: list[str]
    acceptance_strings_updated: int


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--task-id", type=int, required=True)
    ap.add_argument("--view", choices=["back", "gameplay"], required=True)
    ap.add_argument("--remove", nargs="*", default=[])
    ap.add_argument("--add", nargs="*", default=[])
    args = ap.parse_args()

    remove = _normalize(list(args.remove))
    add = _normalize(list(args.add))

    path = _view_path(args.view)
    data = _read_json(path)
    if not isinstance(data, list):
        raise SystemExit(f"Expected a JSON list at {path}")

    matches = [t for t in data if isinstance(t, dict) and t.get("taskmaster_id") == args.task_id]
    if len(matches) != 1:
        raise SystemExit(f"Expected exactly 1 taskmaster_id={args.task_id} in {path}, got {len(matches)}")
    task = matches[0]

    before = _normalize(list(task.get("test_refs") or []))

    after_list = [x for x in before if x not in set(remove)]
    for x in add:
        if x not in after_list:
            after_list.append(x)
    after = _normalize(after_list)
    task["test_refs"] = after

    updated = 0
    acc = task.get("acceptance")
    if isinstance(acc, list) and remove:
        for i, s in enumerate(acc):
            if not isinstance(s, str):
                continue
            original = s
            for r in remove:
                s = s.replace(f" {r}", "")
                s = s.replace(r, "")
            if s != original:
                acc[i] = s
                updated += 1

    _write_json(path, data)

    report = FixReport(
        task_id=args.task_id,
        view=args.view,
        path=str(path.relative_to(REPO_ROOT)),
        removed=remove,
        added=add,
        before=before,
        after=after,
        acceptance_strings_updated=updated,
    )

    out_dir = REPO_ROOT / "logs" / "ci" / _today() / "task-testrefs-fix"
    _write_json(out_dir / f"task-{args.task_id}.json", report.__dict__)
    _write_text(
        out_dir / f"task-{args.task_id}.txt",
        "\n".join(
            [
                f"task_id={args.task_id}",
                f"view={args.view}",
                f"path={report.path}",
                f"removed={remove}",
                f"added={add}",
                f"before={before}",
                f"after={after}",
                f"acceptance_strings_updated={updated}",
            ]
        )
        + "\n",
    )

    print(f"WROTE {path}")
    print(f"WROTE {out_dir / f'task-{args.task_id}.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

