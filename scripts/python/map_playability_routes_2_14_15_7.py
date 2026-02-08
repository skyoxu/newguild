from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


REQUIRED_SPEC_KEYS = {
    "route_no",
    "title",
    "description",
    "story_id",
    "layer",
    "labels",
    "chapter_refs",
    "adr_refs",
    "overlay_refs",
    "contract_refs",
    "test_refs",
    "artifact_refs",
    "acceptance",
    "test_strategy",
    "master_dependencies",
    "complexity",
}


def utc_iso() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds")


def ci_date() -> str:
    return os.environ.get("CI_DATE") or datetime.now().strftime("%Y-%m-%d")


def read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, obj: Any) -> None:
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def load_specs(path: Path) -> list[dict[str, Any]]:
    raw = read_json(path)
    if not isinstance(raw, list):
        raise ValueError(f"Invalid specs payload: expected list, got {type(raw).__name__}")

    specs: list[dict[str, Any]] = []
    for index, item in enumerate(raw, start=1):
        if not isinstance(item, dict):
            raise ValueError(f"Invalid spec item #{index}: expected object, got {type(item).__name__}")
        missing = sorted(REQUIRED_SPEC_KEYS.difference(item.keys()))
        if missing:
            raise ValueError(f"Spec #{index} missing keys: {', '.join(missing)}")
        specs.append(item)
    return specs


def find_master_by_title(tasks: list[dict[str, Any]], title: str) -> dict[str, Any] | None:
    return next((task for task in tasks if isinstance(task, dict) and task.get("title") == title), None)


def next_master_id(tasks: list[dict[str, Any]]) -> int:
    ids: list[int] = []
    for item in tasks:
        tid = item.get("id")
        if isinstance(tid, int):
            ids.append(tid)
        elif isinstance(tid, str) and tid.isdigit():
            ids.append(int(tid))
    return (max(ids) + 1) if ids else 1


def next_view_id(items: list[dict[str, Any]], prefix: str) -> str:
    max_no = 0
    for item in items:
        vid = item.get("id")
        if isinstance(vid, str) and vid.startswith(prefix):
            number = vid[len(prefix) :]
            if number.isdigit():
                max_no = max(max_no, int(number))
    return f"{prefix}{max_no + 1:04d}"


def normalize_master_id(value: Any) -> str | None:
    if value is None:
        return None
    if isinstance(value, int):
        return str(value)
    if isinstance(value, str) and value.strip().isdigit():
        return str(int(value.strip()))
    return None


def build_master_details(spec: dict[str, Any]) -> str:
    adr_refs = "; ".join(spec["adr_refs"])
    chapter_refs = "; ".join(spec["chapter_refs"])
    overlay_refs = "; ".join(spec["overlay_refs"])
    test_refs = "; ".join(spec["test_refs"])
    return "\n".join(
        [
            f"Story: {spec['story_id']}",
            f"ADR Refs: {adr_refs}",
            f"Chapters: {chapter_refs}",
            f"Overlays: {overlay_refs}",
            f"Test Refs: {test_refs}",
        ]
    )


def build_view_lookup(items: list[dict[str, Any]]) -> dict[str, dict[str, Any]]:
    mapped: dict[str, dict[str, Any]] = {}
    for item in items:
        tmid = normalize_master_id(item.get("taskmaster_id"))
        if tmid:
            mapped[tmid] = item
    return mapped


def resolve_gameplay_dep_ids(master_dep_ids: list[str], gameplay_by_tm: dict[str, dict[str, Any]]) -> list[str]:
    dep_ids: list[str] = []
    for dep_tm in master_dep_ids:
        item = gameplay_by_tm.get(str(dep_tm))
        if item and isinstance(item.get("id"), str):
            dep_ids.append(item["id"])
    return dep_ids


def resolve_back_dep(
    route_no: int,
    route_back_id: dict[int, str],
    existing_back_ids: set[str],
) -> list[str]:
    if route_no == 2:
        return [dep for dep in ("NG-0041", "NG-0045") if dep in existing_back_ids]
    if route_no == 14:
        return [route_back_id[2]] if route_back_id.get(2) else []
    if route_no == 15:
        return [route_back_id[14]] if route_back_id.get(14) else []
    dep_ids: list[str] = []
    if route_back_id.get(2):
        dep_ids.append(route_back_id[2])
    if route_back_id.get(14):
        dep_ids.append(route_back_id[14])
    return dep_ids


def main() -> int:
    repo_root = Path(__file__).resolve().parents[2]
    tasks_json_path = repo_root / ".taskmaster" / "tasks" / "tasks.json"
    gameplay_path = repo_root / ".taskmaster" / "tasks" / "tasks_gameplay.json"
    back_path = repo_root / ".taskmaster" / "tasks" / "tasks_back.json"
    specs_path = Path(__file__).resolve().parent / "data" / "playability_routes_2_14_15_7.json"

    tasks_json = read_json(tasks_json_path)
    master_tasks: list[dict[str, Any]] = tasks_json["master"]["tasks"]
    gameplay: list[dict[str, Any]] = read_json(gameplay_path)
    back: list[dict[str, Any]] = read_json(back_path)
    route_specs = load_specs(specs_path)

    out_dir = repo_root / "logs" / "ci" / ci_date() / "route-mapping-2-14-15-7"
    out_dir.mkdir(parents=True, exist_ok=True)
    write_json(out_dir / "tasks.json.before.json", tasks_json)
    write_json(out_dir / "tasks_gameplay.json.before.json", gameplay)
    write_json(out_dir / "tasks_back.json.before.json", back)

    report: dict[str, Any] = {
        "ts": utc_iso(),
        "created_master": [],
        "created_gameplay": [],
        "created_back": [],
        "reused_master": [],
    }

    route_tm_id: dict[int, str] = {}
    next_master = next_master_id(master_tasks)

    for spec in route_specs:
        route_no = int(spec["route_no"])
        existing_master = find_master_by_title(master_tasks, spec["title"])
        if existing_master is not None:
            tmid = normalize_master_id(existing_master.get("id"))
            if not tmid:
                raise ValueError(f"Master id invalid for route {route_no}: {existing_master.get('id')}")
            route_tm_id[route_no] = tmid
            report["reused_master"].append({"route": route_no, "id": tmid, "title": spec["title"]})
            continue

        tmid = str(next_master)
        next_master += 1
        route_tm_id[route_no] = tmid

        master_item = {
            "id": tmid,
            "title": spec["title"],
            "status": "pending",
            "priority": "medium",
            "description": spec["description"],
            "details": build_master_details(spec),
            "dependencies": [str(dep) for dep in spec["master_dependencies"]],
            "adrRefs": spec["adr_refs"],
            "archRefs": spec["chapter_refs"],
            "overlay": spec["overlay_refs"][0],
            "acceptance": spec["acceptance"],
            "testRefs": spec["test_refs"],
            "testStrategy": "TDD (red->green->refactor). Prefer deterministic xUnit + GdUnit4 playability route checks.",
            "recommendedSubtasks": 0,
            "subtasks": [],
            "updatedAt": utc_iso(),
            "complexity": int(spec["complexity"]),
        }
        master_tasks.append(master_item)
        report["created_master"].append({"route": route_no, "id": tmid, "title": spec["title"]})

    gameplay_by_tm = build_view_lookup(gameplay)
    back_by_tm = build_view_lookup(back)

    route_back_id: dict[int, str] = {}
    for spec in route_specs:
        route_no = int(spec["route_no"])
        tmid = route_tm_id[route_no]
        back_item = back_by_tm.get(tmid)
        if back_item and isinstance(back_item.get("id"), str):
            route_back_id[route_no] = back_item["id"]

    existing_back_ids = {item.get("id") for item in back if isinstance(item.get("id"), str)}

    for spec in route_specs:
        route_no = int(spec["route_no"])
        tmid = route_tm_id[route_no]

        if tmid not in gameplay_by_tm:
            gid = next_view_id(gameplay, "GM-")
            gameplay_item = {
                "id": gid,
                "taskmaster_id": int(tmid),
                "taskmaster_exported": True,
                "title": spec["title"],
                "status": "pending",
                "priority": "P2",
                "layer": spec["layer"],
                "owner": "",
                "labels": spec["labels"],
                "story_id": spec["story_id"],
                "chapter_refs": spec["chapter_refs"],
                "adr_refs": spec["adr_refs"],
                "overlay_refs": spec["overlay_refs"],
                "depends_on": resolve_gameplay_dep_ids([str(dep) for dep in spec["master_dependencies"]], gameplay_by_tm),
                "description": spec["description"],
                "acceptance": spec["acceptance"],
                "test_strategy": spec["test_strategy"],
                "test_refs": spec["test_refs"],
                "contractRefs": spec["contract_refs"],
                "artifactRefs": spec["artifact_refs"],
            }
            gameplay.append(gameplay_item)
            gameplay_by_tm[tmid] = gameplay_item
            report["created_gameplay"].append({"route": route_no, "id": gid, "taskmaster_id": tmid})

        if tmid not in back_by_tm:
            bid = next_view_id(back, "NG-")
            back_dep = resolve_back_dep(route_no, route_back_id, existing_back_ids)
            back_item = {
                "id": bid,
                "taskmaster_id": int(tmid),
                "taskmaster_exported": True,
                "title": spec["title"],
                "status": "pending",
                "priority": "P2",
                "layer": spec["layer"],
                "owner": "",
                "labels": [*spec["labels"], "backlog"],
                "story_id": spec["story_id"],
                "chapter_refs": spec["chapter_refs"],
                "adr_refs": spec["adr_refs"],
                "overlay_refs": spec["overlay_refs"],
                "depends_on": back_dep,
                "description": spec["description"],
                "acceptance": spec["acceptance"],
                "test_strategy": spec["test_strategy"],
                "test_refs": spec["test_refs"],
                "contractRefs": spec["contract_refs"],
                "artifactRefs": spec["artifact_refs"],
            }
            back.append(back_item)
            back_by_tm[tmid] = back_item
            existing_back_ids.add(bid)
            route_back_id[route_no] = bid
            report["created_back"].append({"route": route_no, "id": bid, "taskmaster_id": tmid})

    tasks_json["master"]["tasks"] = master_tasks
    write_json(tasks_json_path, tasks_json)
    write_json(gameplay_path, gameplay)
    write_json(back_path, back)
    write_json(out_dir / "report.json", report)

    print("OK: mapped routes 2,14,15,7 into master/gameplay/back")
    print(f"report={out_dir.as_posix()}/report.json")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
