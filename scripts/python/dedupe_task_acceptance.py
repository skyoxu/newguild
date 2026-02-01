#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Deduplicate acceptance items for a single view task.

This helps keep ACC anchor mapping stable:
- validate_acceptance_anchors.py maps acceptance index n -> ACC:T<id>.n
- duplicated acceptance lines create unnecessary anchors and failures
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


@dataclass(frozen=True)
class DedupeReport:
    task_id: int
    view: str
    path: str
    before_count: int
    after_count: int
    removed_count: int


def _view_path(view: str) -> Path:
    if view == "back":
        return REPO_ROOT / ".taskmaster" / "tasks" / "tasks_back.json"
    if view == "gameplay":
        return REPO_ROOT / ".taskmaster" / "tasks" / "tasks_gameplay.json"
    raise ValueError(f"Unsupported view: {view}")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--task-id", type=int, required=True)
    ap.add_argument("--view", choices=["back", "gameplay"], required=True)
    args = ap.parse_args()

    path = _view_path(args.view)
    data = _read_json(path)
    if not isinstance(data, list):
        raise SystemExit(f"Expected a JSON list at {path}")

    matches = [t for t in data if isinstance(t, dict) and t.get("taskmaster_id") == args.task_id]
    if len(matches) != 1:
        raise SystemExit(f"Expected exactly 1 taskmaster_id={args.task_id} in {path}, got {len(matches)}")
    task = matches[0]

    acc = task.get("acceptance")
    if not isinstance(acc, list):
        raise SystemExit("acceptance is missing or not a list")

    before = list(acc)
    seen: set[str] = set()
    after: list[Any] = []
    for item in before:
        if not isinstance(item, str):
            after.append(item)
            continue
        if item in seen:
            continue
        seen.add(item)
        after.append(item)

    task["acceptance"] = after
    _write_json(path, data)

    report = DedupeReport(
        task_id=args.task_id,
        view=args.view,
        path=str(path.relative_to(REPO_ROOT)),
        before_count=len(before),
        after_count=len(after),
        removed_count=len(before) - len(after),
    )

    out_dir = REPO_ROOT / "logs" / "ci" / _today() / "task-acceptance-dedupe"
    _write_json(out_dir / f"task-{args.task_id}.json", report.__dict__)

    print(f"WROTE {path}")
    print(f"WROTE {out_dir / f'task-{args.task_id}.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

