from __future__ import annotations

import argparse
import json
import os
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable


@dataclass(frozen=True)
class Keywords:
    route1: list[str]
    route13: list[str]


DEFAULT_KEYWORDS = Keywords(
    route1=[
        "3.0.3",
        "T2",
        "minimal playable",
        "playable loop",
        "core loop",
        "turn system",
        "three phase",
        "three-phase",
        "3-phase",
        "advance week",
        "advance turn",
        "next turn",
        "week:",
        "phase:",
        "resolution",
        "player phase",
        "ai simulation",
        "game turn",
        "hud",
        "weeklabel",
        "phaselabel",
    ],
    route13=[
        "modal",
        "modalmanager",
        "confirm dialog",
        "confirmdialog",
        "context menu",
        "right click",
        "right-click",
        "esc",
        "keyboard navigation",
        "accessibility",
        "drag and drop",
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


def score(text: str, keywords: list[str]) -> tuple[int, list[str]]:
    lowered = text.lower()
    hits: list[str] = []
    points = 0
    for kw in keywords:
        if kw.lower() in lowered:
            hits.append(kw)
            points += 1
    return points, hits


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
    if not isinstance(master_obj, dict):
        raise ValueError("tasks.json root must be an object")
    master = master_obj.get("master")
    if not isinstance(master, dict):
        raise ValueError("tasks.json must contain master object")
    tasks = master.get("tasks")
    if not isinstance(tasks, list):
        raise ValueError("tasks.json master.tasks must be a list")
    return [t for t in tasks if isinstance(t, dict)]


def effective_status(view_task: dict, master_task: dict | None) -> str | None:
    return (view_task.get("status") if isinstance(view_task, dict) else None) or (
        master_task.get("status") if master_task else None
    )


def summarize_task(
    *,
    source: str,
    view_task: dict,
    master_task: dict | None,
    keywords: Keywords,
) -> dict:
    title = (
        view_task.get("title")
        or (master_task or {}).get("title")
        or view_task.get("name")
        or ""
    )
    blob = task_blob(view_task, master_task or {})
    r1_score, r1_hits = score(blob, keywords.route1)
    r13_score, r13_hits = score(blob, keywords.route13)
    return {
        "source": source,
        "taskmaster_id": normalize_task_id(view_task.get("taskmaster_id"))
        or normalize_task_id((master_task or {}).get("id")),
        "master_status": (master_task or {}).get("status"),
        "view_status": view_task.get("status"),
        "status_effective": effective_status(view_task, master_task),
        "title": title,
        "route1_score": r1_score,
        "route1_hits": r1_hits,
        "route13_score": r13_score,
        "route13_hits": r13_hits,
    }


def scan_master_only(master_tasks: list[dict], keywords: Keywords) -> list[dict]:
    out: list[dict] = []
    for task in master_tasks:
        blob = task_blob(task)
        r1_score, r1_hits = score(blob, keywords.route1)
        r13_score, r13_hits = score(blob, keywords.route13)
        if r1_score <= 0 and r13_score <= 0:
            continue
        out.append(
            {
                "source": "tasks.json(master)",
                "taskmaster_id": task.get("id"),
                "master_status": task.get("status"),
                "view_status": None,
                "status_effective": task.get("status"),
                "title": task.get("title", ""),
                "route1_score": r1_score,
                "route1_hits": r1_hits,
                "route13_score": r13_score,
                "route13_hits": r13_hits,
            }
        )
    return out


def scan_view(
    *,
    source: str,
    view_items: list[Any],
    master_by_id: dict[str, dict],
    keywords: Keywords,
) -> list[dict]:
    out: list[dict] = []
    for item in view_items:
        if not isinstance(item, dict):
            continue
        tid = normalize_task_id(item.get("taskmaster_id"))
        master_task = master_by_id.get(tid) if tid else None
        summary = summarize_task(
            source=source, view_task=item, master_task=master_task, keywords=keywords
        )
        if summary["route1_score"] <= 0 and summary["route13_score"] <= 0:
            continue
        out.append(summary)
    return out


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Scan task views for Route 1 (T2 core loop) and Route 13 (modal/context menu) related tasks."
    )
    parser.add_argument(
        "--out",
        default="",
        help="Output JSON path. Default: logs/ci/<CI_DATE>/route1-route13-task-scan.json",
    )
    args = parser.parse_args()

    repo_root = Path(__file__).resolve().parents[2]
    master_path = repo_root / ".taskmaster" / "tasks" / "tasks.json"
    back_path = repo_root / ".taskmaster" / "tasks" / "tasks_back.json"
    gameplay_path = repo_root / ".taskmaster" / "tasks" / "tasks_gameplay.json"
    newguild_path = repo_root / ".taskmaster" / "tasks" / "tasks_newguild.json"

    master_obj = load_json(master_path)
    master_tasks = get_master_tasks(master_obj)
    master_by_id = {
        normalize_task_id(t.get("id")): t for t in master_tasks if normalize_task_id(t.get("id")) is not None
    }

    back = load_json(back_path)
    gameplay = load_json(gameplay_path)
    newguild = load_json(newguild_path)

    keywords = DEFAULT_KEYWORDS

    matches: list[dict] = []
    matches.extend(scan_master_only(master_tasks, keywords))
    if isinstance(back, list):
        matches.extend(
            scan_view(
                source="tasks_back.json",
                view_items=back,
                master_by_id=master_by_id,
                keywords=keywords,
            )
        )
    if isinstance(gameplay, list):
        matches.extend(
            scan_view(
                source="tasks_gameplay.json",
                view_items=gameplay,
                master_by_id=master_by_id,
                keywords=keywords,
            )
        )
    if isinstance(newguild, list):
        matches.extend(
            scan_view(
                source="tasks_newguild.json",
                view_items=newguild,
                master_by_id=master_by_id,
                keywords=keywords,
            )
        )

    # Deduplicate
    deduped: list[dict] = []
    seen = set()
    for m in matches:
        key = (m.get("source"), str(m.get("taskmaster_id")), m.get("title"))
        if key in seen:
            continue
        seen.add(key)
        deduped.append(m)

    # Sort: Route1 hits first, then Route13; higher score first.
    deduped.sort(
        key=lambda m: (
            -(m.get("route1_score", 0) > 0),
            -m.get("route1_score", 0),
            -(m.get("route13_score", 0) > 0),
            -m.get("route13_score", 0),
            str(m.get("taskmaster_id") or ""),
        )
    )

    report = {
        "ts_utc": utc_ts(),
        "inputs": {
            "master": master_path.as_posix(),
            "back": back_path.as_posix(),
            "gameplay": gameplay_path.as_posix(),
            "newguild": newguild_path.as_posix(),
        },
        "keywords": {"route1": keywords.route1, "route13": keywords.route13},
        "matches": deduped,
    }

    if args.out:
        out_path = (repo_root / args.out).resolve()
        out_path.parent.mkdir(parents=True, exist_ok=True)
    else:
        ci_date = os.environ.get("CI_DATE") or datetime.now().strftime("%Y-%m-%d")
        out_path = repo_root / "logs" / "ci" / ci_date / "route1-route13-task-scan.json"
        out_path.parent.mkdir(parents=True, exist_ok=True)

    out_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

    route1 = [m for m in deduped if m.get("route1_score", 0) > 0]
    route13 = [m for m in deduped if m.get("route13_score", 0) > 0]

    print(f"report: {out_path.as_posix()}")
    print(f"route1 matches: {len(route1)}")
    print(f"route13 matches: {len(route13)}")

    def show(label: str, arr: list[dict]) -> None:
        print(f"\n== {label} top ==")
        for m in arr[:8]:
            print(
                f"- src={m.get('source')} id={m.get('taskmaster_id')} status={m.get('status_effective')} "
                f"r1={m.get('route1_score')} r13={m.get('route13_score')} title={m.get('title')}"
            )

    show("route1", route1)
    show("route13", route13)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())

