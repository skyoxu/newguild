from __future__ import annotations

import argparse
import json
import os
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable


@dataclass(frozen=True)
class RouteSpec:
    name: str
    keywords_any: list[str]


ROUTE_1 = RouteSpec(
    name="Route1_T2_MinimalPlayableLoop",
    keywords_any=[
        "T2",
        "Minimal Playable",
        "Minimal Playable Loop",
        "playable loop",
        "core loop",
        "turn system",
        "Next Turn",
        "Advance Week",
        "Advance Turn",
        "Week:",
        "Phase:",
        "GameTurnSystem",
        "Game Turn",
        "three-phase",
        "three phase",
        "3-phase",
    ],
)

ROUTE_13 = RouteSpec(
    name="Route13_Modal_ContextMenu",
    keywords_any=[
        "Modal",
        "ModalManager",
        "ConfirmDialog",
        "Confirm Dialog",
        "ContextMenu",
        "Context Menu",
        "RightClick",
        "Right Click",
        "Right-Click",
        "DragDrop",
        "Drag and Drop",
    ],
)


def utc_ts() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds")


def read_text_utf8(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def load_json(path: Path) -> Any:
    return json.loads(read_text_utf8(path))


def iter_strings(obj: Any) -> Iterable[str]:
    if obj is None:
        return
    if isinstance(obj, str):
        yield obj
        return
    if isinstance(obj, list):
        for item in obj:
            yield from iter_strings(item)
        return
    if isinstance(obj, dict):
        for key, value in obj.items():
            if isinstance(key, str):
                yield key
            yield from iter_strings(value)


def task_blob(*parts: Any) -> str:
    chunks: list[str] = []
    for part in parts:
        chunks.extend(iter_strings(part))
    return "\n".join(chunks)


def normalize_task_id(raw: Any) -> str | None:
    if raw is None:
        return None
    if isinstance(raw, int):
        return str(raw)
    if isinstance(raw, str):
        raw = raw.strip()
        return raw if raw else None
    return None


def get_master_tasks(master_obj: Any) -> list[dict]:
    master = master_obj.get("master") if isinstance(master_obj, dict) else None
    tasks = master.get("tasks") if isinstance(master, dict) else None
    if not isinstance(tasks, list):
        raise ValueError("Expected tasks.json to be {master:{tasks:[...]}}")
    return [t for t in tasks if isinstance(t, dict)]


def keyword_hits(text: str, keywords: list[str]) -> list[str]:
    lowered = text.lower()
    return [kw for kw in keywords if kw.lower() in lowered]


def status_is_done(status: Any) -> bool:
    return isinstance(status, str) and status.strip().lower() == "done"


def summarize(
    *,
    source: str,
    view_task: dict | None,
    master_task: dict | None,
    route: RouteSpec,
) -> dict:
    title = (
        (view_task or {}).get("title")
        or (master_task or {}).get("title")
        or (view_task or {}).get("name")
        or ""
    )
    blob = task_blob(view_task or {}, master_task or {})
    hits = keyword_hits(blob, route.keywords_any)
    effective_status = (view_task or {}).get("status") or (master_task or {}).get("status")
    return {
        "source": source,
        "taskmaster_id": normalize_task_id((view_task or {}).get("taskmaster_id"))
        or normalize_task_id((master_task or {}).get("id")),
        "title": title,
        "master_status": (master_task or {}).get("status"),
        "view_status": (view_task or {}).get("status"),
        "status_effective": effective_status,
        "done": status_is_done(effective_status),
        "hits": hits,
    }


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Find tasks related to PRD routes (Route1 core loop, Route13 modal/context menu)."
    )
    parser.add_argument(
        "--out",
        default="",
        help="Output JSON path. Default: logs/ci/<CI_DATE>/prd-route-task-candidates.json",
    )
    args = parser.parse_args()

    repo_root = Path(__file__).resolve().parents[2]
    master_path = repo_root / ".taskmaster" / "tasks" / "tasks.json"
    back_path = repo_root / ".taskmaster" / "tasks" / "tasks_back.json"
    gameplay_path = repo_root / ".taskmaster" / "tasks" / "tasks_gameplay.json"
    newguild_path = repo_root / ".taskmaster" / "tasks" / "tasks_newguild.json"

    master_tasks = get_master_tasks(load_json(master_path))
    master_by_id = {
        normalize_task_id(t.get("id")): t
        for t in master_tasks
        if normalize_task_id(t.get("id")) is not None
    }

    views: list[tuple[str, Any]] = [
        ("tasks_back.json", load_json(back_path)),
        ("tasks_gameplay.json", load_json(gameplay_path)),
        ("tasks_newguild.json", load_json(newguild_path)),
    ]

    routes = [ROUTE_1, ROUTE_13]
    results: dict[str, list[dict]] = {r.name: [] for r in routes}

    for task in master_tasks:
        for route in routes:
            rec = summarize(source="tasks.json(master)", view_task=None, master_task=task, route=route)
            if rec["hits"]:
                results[route.name].append(rec)

    for view_name, view_obj in views:
        if not isinstance(view_obj, list):
            continue
        for item in view_obj:
            if not isinstance(item, dict):
                continue
            tid = normalize_task_id(item.get("taskmaster_id"))
            master_task = master_by_id.get(tid) if tid else None
            for route in routes:
                rec = summarize(source=view_name, view_task=item, master_task=master_task, route=route)
                if rec["hits"]:
                    results[route.name].append(rec)

    for route in routes:
        uniq: list[dict] = []
        seen = set()
        for rec in results[route.name]:
            key = (rec.get("source"), str(rec.get("taskmaster_id")), rec.get("title"))
            if key in seen:
                continue
            seen.add(key)
            uniq.append(rec)
        uniq.sort(
            key=lambda r: (
                r.get("done") is True,
                -len(r.get("hits") or []),
                str(r.get("taskmaster_id") or ""),
            )
        )
        results[route.name] = uniq

    report = {
        "ts_utc": utc_ts(),
        "inputs": {
            "master": master_path.as_posix(),
            "back": back_path.as_posix(),
            "gameplay": gameplay_path.as_posix(),
            "newguild": newguild_path.as_posix(),
        },
        "routes": {
            ROUTE_1.name: ROUTE_1.keywords_any,
            ROUTE_13.name: ROUTE_13.keywords_any,
        },
        "results": results,
    }

    if args.out:
        out_path = (repo_root / args.out).resolve()
        out_path.parent.mkdir(parents=True, exist_ok=True)
    else:
        ci_date = os.environ.get("CI_DATE") or datetime.now().strftime("%Y-%m-%d")
        out_path = repo_root / "logs" / "ci" / ci_date / "prd-route-task-candidates.json"
        out_path.parent.mkdir(parents=True, exist_ok=True)

    out_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

    print(f"report: {out_path.as_posix()}")
    for route in routes:
        arr = results[route.name]
        non_done = [r for r in arr if not r.get("done", False)]
        print(f"{route.name}: total={len(arr)} non_done={len(non_done)}")
        for rec in arr[:12]:
            print(
                f"- done={rec.get('done')} id={rec.get('taskmaster_id')} status={rec.get('status_effective')} "
                f"src={rec.get('source')} title={rec.get('title')} hits={rec.get('hits')}"
            )
        print("")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())

