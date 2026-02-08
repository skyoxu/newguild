from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


def utc_now_iso() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds")


def read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, obj: Any) -> None:
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def find_by_title(items: list[dict], title: str) -> dict | None:
    return next((t for t in items if isinstance(t, dict) and t.get("title") == title), None)


def find_by_id(items: list[dict], item_id: str) -> dict | None:
    return next((t for t in items if isinstance(t, dict) and str(t.get("id")) == str(item_id)), None)


def master_by_id(master_tasks: list[dict]) -> dict[str, dict]:
    out: dict[str, dict] = {}
    for t in master_tasks:
        if not isinstance(t, dict):
            continue
        tid = t.get("id")
        if isinstance(tid, int):
            out[str(tid)] = t
        elif isinstance(tid, str) and tid.strip():
            out[tid.strip()] = t
    return out


def normalize_str_list(value: Any) -> list[str]:
    if value is None:
        return []
    if isinstance(value, list):
        return [str(v) for v in value if v is not None and str(v).strip() != ""]
    if isinstance(value, str):
        return [value] if value.strip() else []
    return [str(value)]


def main() -> int:
    repo_root = Path(__file__).resolve().parents[2]
    master_path = repo_root / ".taskmaster" / "tasks" / "tasks.json"
    back_path = repo_root / ".taskmaster" / "tasks" / "tasks_back.json"
    gameplay_path = repo_root / ".taskmaster" / "tasks" / "tasks_gameplay.json"
    newguild_path = repo_root / ".taskmaster" / "tasks" / "tasks_newguild.json"

    master_obj = read_json(master_path)
    master_tasks: list[dict] = master_obj["master"]["tasks"]
    back: list[dict] = read_json(back_path)
    gameplay: list[dict] = read_json(gameplay_path)
    newguild: list[dict] = read_json(newguild_path)

    ci_date = os.environ.get("CI_DATE") or datetime.now().strftime("%Y-%m-%d")
    audit_dir = repo_root / "logs" / "ci" / ci_date / "map-prd-routes-fix"
    audit_dir.mkdir(parents=True, exist_ok=True)

    # Backups
    write_json(audit_dir / "tasks.json.before.json", master_obj)
    write_json(audit_dir / "tasks_back.json.before.json", back)
    write_json(audit_dir / "tasks_gameplay.json.before.json", gameplay)
    write_json(audit_dir / "tasks_newguild.json.before.json", newguild)

    report: dict[str, Any] = {"ts_utc": utc_now_iso(), "changes": []}

    master_index = master_by_id(master_tasks)

    def set_master_deps(task_id: str, deps: list[str]) -> None:
        t = master_index.get(task_id)
        if not t:
            return
        before = list(t.get("dependencies") or [])
        t["dependencies"] = deps
        if before != deps:
            report["changes"].append(
                {"file": "tasks.json", "task_id": task_id, "change": "dependencies", "before": before, "after": deps}
            )

    # Fix master dependencies for 44/45: they were view IDs (NG-xxxx); resolve via tasks_back mapping.
    def resolve_back_dep_to_master(dep_id: str) -> str | None:
        dep = find_by_id(back, dep_id)
        if not dep:
            return None
        tm = dep.get("taskmaster_id")
        if isinstance(tm, int):
            return str(tm)
        if isinstance(tm, str) and tm.isdigit():
            return tm
        return None

    t44 = master_index.get("44")
    if t44:
        # Route1 audit: depends_on NG-0039 -> master 9
        set_master_deps("44", [d for d in [resolve_back_dep_to_master("NG-0039")] if d])

    t45 = master_index.get("45")
    if t45:
        # Route1 arch refine: depends_on NG-0020 -> master 2
        set_master_deps("45", [d for d in [resolve_back_dep_to_master("NG-0020")] if d])

    # Fix master deps for 46/47: drop unresolved numeric deps; set 47 depends on 46.
    if "46" in master_index:
        set_master_deps("46", [])
    if "47" in master_index:
        set_master_deps("47", ["46"])

    # Clean up tasks_newguild details to remove wrong stack phrasing.
    for title in ["Implement Core UI Components", "Develop Interaction Patterns"]:
        item = find_by_title(newguild, title)
        if not item:
            continue
        details = str(item.get("details") or "")
        cleaned = details.replace("component-based framework", "Godot scenes and Control nodes")
        cleaned = cleaned.replace("Use libraries like", "Use Godot built-in UI primitives; avoid extra libraries.")
        if cleaned != details:
            item["details"] = cleaned
            report["changes"].append(
                {
                    "file": "tasks_newguild.json",
                    "title": title,
                    "change": "details_sanitize",
                }
            )

    # Fix gameplay mirrored tasks for tm_id 46/47: depends_on should not reference numeric IDs; remove extra field.
    gp_46 = next((t for t in gameplay if isinstance(t, dict) and t.get("taskmaster_id") == 46), None)
    gp_47 = next((t for t in gameplay if isinstance(t, dict) and t.get("taskmaster_id") == 47), None)
    if gp_46:
        before = normalize_str_list(gp_46.get("depends_on"))
        gp_46["depends_on"] = []
        if "contractRefsNotes" in gp_46:
            gp_46.pop("contractRefsNotes", None)
        if before != []:
            report["changes"].append(
                {"file": "tasks_gameplay.json", "taskmaster_id": 46, "change": "depends_on", "before": before, "after": []}
            )
    if gp_47:
        # depend on gp_46 by view id, not by master id
        dep = gp_46.get("id") if gp_46 else None
        desired = [str(dep)] if dep else []
        before = normalize_str_list(gp_47.get("depends_on"))
        gp_47["depends_on"] = desired
        if "contractRefsNotes" in gp_47:
            gp_47.pop("contractRefsNotes", None)
        if before != desired:
            report["changes"].append(
                {
                    "file": "tasks_gameplay.json",
                    "taskmaster_id": 47,
                    "change": "depends_on",
                    "before": before,
                    "after": desired,
                }
            )

    # Persist
    master_obj["master"]["tasks"] = master_tasks
    write_json(master_path, master_obj)
    write_json(back_path, back)
    write_json(gameplay_path, gameplay)
    write_json(newguild_path, newguild)
    write_json(audit_dir / "report.json", report)

    print("OK: fixed PRD route task mapping")
    print(f"report={audit_dir.as_posix()}/report.json")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

