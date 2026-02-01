#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Fix contractRefs for a single view task (tasks_back.json or tasks_gameplay.json).

Repo rules (enforced by convention here):
- contractRefs lives in view task files and is the SSoT for "which events this task cares about".
- Document-style files are read/written via Python with UTF-8.
- Evidence is written under logs/ci/<YYYY-MM-DD>/...
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


@dataclass(frozen=True)
class FixReport:
    task_id: int
    view: str
    path: str
    before: list[str]
    after: list[str]
    acceptance_strings_updated: int


def _view_path(view: str) -> Path:
    if view == "back":
        return REPO_ROOT / ".taskmaster" / "tasks" / "tasks_back.json"
    if view == "gameplay":
        return REPO_ROOT / ".taskmaster" / "tasks" / "tasks_gameplay.json"
    raise ValueError(f"Unsupported view: {view}")


def _normalize(refs: list[str]) -> list[str]:
    # Preserve order, remove exact duplicates.
    out: list[str] = []
    seen: set[str] = set()
    for r in refs:
        s = str(r).strip()
        if not s:
            continue
        if s in seen:
            continue
        seen.add(s)
        out.append(s)
    return out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--task-id", type=int, required=True)
    ap.add_argument("--view", choices=["back", "gameplay"], required=True)
    ap.add_argument("--set", nargs="+", required=True, help="Replace contractRefs with this list.")
    args = ap.parse_args()

    path = _view_path(args.view)
    data = _read_json(path)
    if not isinstance(data, list):
        raise SystemExit(f"Expected a JSON list at {path}")

    matches = [t for t in data if isinstance(t, dict) and t.get("taskmaster_id") == args.task_id]
    if len(matches) != 1:
        raise SystemExit(f"Expected exactly 1 taskmaster_id={args.task_id} in {path}, got {len(matches)}")
    task = matches[0]

    before = list(task.get("contractRefs") or [])
    after = _normalize(list(args.set))
    task["contractRefs"] = after

    # Update acceptance strings that contain the old example; keep it deterministic.
    updated = 0
    acc = task.get("acceptance")
    if isinstance(acc, list):
        for i, s in enumerate(acc):
            if not isinstance(s, str):
                continue
            if "core.guild.member.role_changed" not in s:
                continue
            acc[i] = s.replace(
                "core.guild.member.role_changed",
                "core.guild.officer.assigned, core.guild.officer.revoked",
            )
            updated += 1

    _write_json(path, data)

    report = FixReport(
        task_id=args.task_id,
        view=args.view,
        path=str(path.relative_to(REPO_ROOT)),
        before=before,
        after=after,
        acceptance_strings_updated=updated,
    )

    out_dir = REPO_ROOT / "logs" / "ci" / _today() / "task-contractrefs-fix"
    _write_json(out_dir / f"task-{args.task_id}.json", report.__dict__)
    _write_text(
        out_dir / f"task-{args.task_id}.txt",
        "\n".join(
            [
                f"task_id={args.task_id}",
                f"view={args.view}",
                f"path={report.path}",
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

