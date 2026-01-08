#!/usr/bin/env python3
"""
Suggest an execution order for T3 tasks based on `.taskmaster/tasks/tasks.json`.

Notes:
- This does NOT rewrite tasks.json ordering; it only produces an auditable suggestion.
- It includes only tasks whose `title` contains 'T3'.
- It respects `dependencies` (topological sort). If there are ties, it prioritizes:
  1) Crosscutting tasks (title contains 'Save/Load' or 'Schema Migration')
  2) Lower numeric task id

Output:
- `logs/ci/<YYYY-MM-DD>/t3-task-order/t3-order.json`
- `logs/ci/<YYYY-MM-DD>/t3-task-order/t3-order.txt`
"""

from __future__ import annotations

import datetime as dt
import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[2]
MASTER_PATH = REPO_ROOT / ".taskmaster" / "tasks" / "tasks.json"


def _read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def _write_json_pretty(path: Path, obj: Any) -> None:
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _today_dir() -> Path:
    day = dt.date.today().isoformat()
    out_dir = REPO_ROOT / "logs" / "ci" / day / "t3-task-order"
    out_dir.mkdir(parents=True, exist_ok=True)
    return out_dir


def _sid(value: Any) -> str:
    return str(value)


def _is_crosscutting(title: str) -> bool:
    t = title.lower()
    return ("save/load" in t) or ("schema migration" in t)


def _sort_key(task_id: str, title: str) -> tuple[int, int]:
    cross = 0 if _is_crosscutting(title) else 1
    n = int(task_id) if task_id.isdigit() else 10**9
    return (cross, n)


@dataclass(frozen=True)
class TaskNode:
    id: str
    title: str
    deps: tuple[str, ...]


def main() -> int:
    master_obj = _read_json(MASTER_PATH)
    tasks: list[dict[str, Any]] = master_obj["master"]["tasks"]
    by_id = {str(t.get("id")): t for t in tasks}

    t3_nodes: dict[str, TaskNode] = {}
    for t in tasks:
        title = (t.get("title") or "").strip()
        if "T3" not in title:
            continue
        tid = _sid(t.get("id"))
        deps = tuple(_sid(x) for x in (t.get("dependencies") or []))
        t3_nodes[tid] = TaskNode(id=tid, title=title, deps=deps)

    # Build indegree for Kahn topological sort restricted to T3 tasks, but keep
    # external dependencies as "prereq" info only.
    indeg: dict[str, int] = {tid: 0 for tid in t3_nodes}
    edges: dict[str, set[str]] = {tid: set() for tid in t3_nodes}
    external_prereqs: dict[str, list[str]] = {tid: [] for tid in t3_nodes}

    for tid, node in t3_nodes.items():
        for dep in node.deps:
            if dep in t3_nodes:
                indeg[tid] += 1
                edges[dep].add(tid)
            else:
                external_prereqs[tid].append(dep)

    ready = sorted(
        [tid for tid, d in indeg.items() if d == 0],
        key=lambda x: _sort_key(x, t3_nodes[x].title),
    )

    order: list[str] = []
    while ready:
        cur = ready.pop(0)
        order.append(cur)
        for nxt in sorted(edges[cur], key=lambda x: _sort_key(x, t3_nodes[x].title)):
            indeg[nxt] -= 1
            if indeg[nxt] == 0:
                ready.append(nxt)
        ready.sort(key=lambda x: _sort_key(x, t3_nodes[x].title))

    cycle = [tid for tid, d in indeg.items() if d > 0]
    report: dict[str, Any] = {
        "ts": dt.datetime.now().replace(microsecond=0).isoformat(),
        "t3_task_count": len(t3_nodes),
        "order": order,
        "cycle": cycle,
        "tasks": [
            {
                "id": tid,
                "title": t3_nodes[tid].title,
                "dependencies": list(t3_nodes[tid].deps),
                "external_prereqs": sorted(external_prereqs[tid]),
                "status": by_id.get(tid, {}).get("status"),
            }
            for tid in order
        ],
    }

    out_dir = _today_dir()
    out_json = out_dir / "t3-order.json"
    out_txt = out_dir / "t3-order.txt"
    _write_json_pretty(out_json, report)
    out_txt.write_text(
        "\n".join(
            [
                f"ts={report['ts']}",
                f"t3_task_count={report['t3_task_count']}",
                ("cycle_detected=" + ", ".join(cycle)) if cycle else "cycle_detected=(none)",
                "",
                "order:",
                *[
                    f"- {tid}: {t3_nodes[tid].title}  deps={list(t3_nodes[tid].deps)}"
                    for tid in order
                ],
                "",
            ]
        ),
        encoding="utf-8",
    )

    print(f"[REPORT] {out_json}")
    print(f"[REPORT] {out_txt}")
    if cycle:
        print("[WARN] Cycle detected among T3 tasks; see report for details.")
    else:
        print("[OK] No cycles detected among T3 tasks.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

