from __future__ import annotations

import json
import os
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


@dataclass(frozen=True)
class SourceTaskRef:
    source: str  # tasks_back.json | tasks_newguild.json
    title: str


def utc_now_iso() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds")


def read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, obj: Any) -> None:
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def normalize_list(value: Any) -> list:
    if value is None:
        return []
    if isinstance(value, list):
        return value
    return [value]


def ensure_str_list(value: Any) -> list[str]:
    out: list[str] = []
    for item in normalize_list(value):
        if item is None:
            continue
        out.append(str(item))
    return out


def find_single_by_title(items: list[dict], title: str) -> dict:
    matches = [t for t in items if isinstance(t, dict) and t.get("title") == title]
    if len(matches) != 1:
        raise ValueError(f"Expected exactly 1 match for title={title!r}, got {len(matches)}")
    return matches[0]


def next_master_id(master_tasks: list[dict]) -> int:
    ids: list[int] = []
    for t in master_tasks:
        tid = t.get("id")
        if isinstance(tid, int):
            ids.append(tid)
        elif isinstance(tid, str) and tid.isdigit():
            ids.append(int(tid))
    if not ids:
        return 1
    return max(ids) + 1


def next_gameplay_id(gameplay: list[dict]) -> str:
    # IDs look like GM-0315; return next sequential.
    max_n = 0
    for t in gameplay:
        tid = t.get("id")
        if not isinstance(tid, str):
            continue
        if tid.startswith("GM-") and tid[3:].isdigit():
            max_n = max(max_n, int(tid[3:]))
    return f"GM-{max_n + 1:04d}"


def build_master_task(
    *,
    new_id: int,
    title: str,
    status: str,
    priority: str | None,
    description: str | None,
    story_id: str | None,
    adr_refs: list[str],
    chapter_refs: list[str],
    overlay_refs: list[str],
    dependencies: list[str],
    acceptance: list[str],
    test_refs: list[str],
    test_strategy: str | None,
) -> dict:
    adr_str = "; ".join(adr_refs) if adr_refs else ""
    ch_str = "; ".join(chapter_refs) if chapter_refs else ""
    overlay_str = "; ".join(overlay_refs) if overlay_refs else ""
    test_refs_str = "; ".join(test_refs) if test_refs else ""

    details_parts = []
    if story_id:
        details_parts.append(f"Story: {story_id}")
    if adr_str:
        details_parts.append(f"ADR Refs: {adr_str}")
    if ch_str:
        details_parts.append(f"Chapters: {ch_str}")
    if overlay_str:
        details_parts.append(f"Overlays: {overlay_str}")
    if test_refs_str:
        details_parts.append(f"Test Refs: {test_refs_str}")

    overlay_primary = overlay_refs[0] if overlay_refs else ""
    return {
        "id": str(new_id),
        "title": title,
        "status": status,
        "priority": priority or "medium",
        "description": description or "",
        "details": "\n".join(details_parts).strip(),
        "dependencies": dependencies,
        "adrRefs": adr_refs,
        "archRefs": chapter_refs,
        "overlay": overlay_primary,
        "acceptance": acceptance,
        "testRefs": test_refs,
        "testStrategy": test_strategy
        or "Prefer deterministic xUnit for pure logic; use GdUnit4 for UI/playability; write artifacts under logs/**.",
        "recommendedSubtasks": 0,
        "subtasks": [],
        "updatedAt": utc_now_iso(),
        "complexity": 3,
    }


def sanitize_newguild_details(text: str) -> str:
    # Remove non-Godot suggestions (React/Electron) and keep it actionable for Godot+C#.
    replaced = text
    replaced = replaced.replace("React", "Godot")
    replaced = replaced.replace("React DnD", "Godot Control drag-and-drop")
    replaced = replaced.replace("custom event handlers", "Godot signals and input events")
    return replaced


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

    # Targets from previous Route scan.
    targets = [
        SourceTaskRef("tasks_back.json", "GameLoop 周推进审计日志（F004）"),
        SourceTaskRef("tasks_back.json", "GameLoop 架构骨架收敛：Ports/时间/状态整理"),
        SourceTaskRef("tasks_newguild.json", "Implement Core UI Components"),
        SourceTaskRef("tasks_newguild.json", "Develop Interaction Patterns"),
    ]

    ci_date = os.environ.get("CI_DATE") or datetime.now().strftime("%Y-%m-%d")
    audit_dir = repo_root / "logs" / "ci" / ci_date / "map-prd-routes"
    audit_dir.mkdir(parents=True, exist_ok=True)

    # Backups
    write_json(audit_dir / "tasks.json.before.json", master_obj)
    write_json(audit_dir / "tasks_back.json.before.json", back)
    write_json(audit_dir / "tasks_gameplay.json.before.json", gameplay)
    write_json(audit_dir / "tasks_newguild.json.before.json", newguild)

    report: dict[str, Any] = {
        "ts_utc": utc_now_iso(),
        "master_added": [],
        "view_updates": [],
        "gameplay_added": [],
        "notes": [
            "Route1/Route13 view tasks with missing taskmaster_id were promoted into master tasks.json (SSoT).",
            "tasks_newguild items were additionally mapped into tasks_gameplay and enriched to match Godot+C# (no React).",
        ],
    }

    new_id = next_master_id(master_tasks)

    def promote_from_view(view_item: dict, *, source_name: str) -> int:
        nonlocal new_id
        assigned_id = new_id
        new_id += 1

        # Extract view fields (snake_case)
        story_id = view_item.get("story_id") or view_item.get("storyId")
        adr_refs = ensure_str_list(view_item.get("adr_refs"))
        chapter_refs = ensure_str_list(view_item.get("chapter_refs"))
        overlay_refs = ensure_str_list(view_item.get("overlay_refs"))
        dependencies = ensure_str_list(view_item.get("depends_on") or view_item.get("dependencies"))
        acceptance = ensure_str_list(view_item.get("acceptance"))
        test_refs = ensure_str_list(view_item.get("test_refs"))
        test_strategy = view_item.get("test_strategy") or view_item.get("testStrategy")

        title = str(view_item.get("title") or "")
        status = str(view_item.get("status") or "pending")
        description = str(view_item.get("description") or "")
        priority = str(view_item.get("priority") or "medium")

        # Enrich tasks_newguild to remove wrong-stack hints.
        if source_name == "tasks_newguild.json":
            description = description.replace("React", "Godot").replace("component-based framework", "Godot scenes and Control nodes")
            details = str(view_item.get("details") or "")
            if details:
                view_item["details"] = sanitize_newguild_details(details)
            view_item["description"] = description
            # Add minimal, deterministic acceptance statements (no Refs yet).
            if not acceptance:
                acceptance = [
                    "Modal system can open/close deterministically (button + ESC), without node leaks or stuck input focus.",
                    "Context menu/right-click interaction uses Godot input events and is testable in headless GdUnit4.",
                ]

            # Strengthen ADR refs for UI interaction work (quality gates).
            if "ADR-0005" not in adr_refs:
                adr_refs.append("ADR-0005")

        master_task = build_master_task(
            new_id=assigned_id,
            title=title,
            status=status,
            priority=priority if priority in ("low", "medium", "high") else "medium",
            description=description,
            story_id=str(story_id) if story_id else None,
            adr_refs=adr_refs,
            chapter_refs=chapter_refs,
            overlay_refs=overlay_refs,
            dependencies=dependencies,
            acceptance=acceptance,
            test_refs=test_refs,
            test_strategy=str(test_strategy) if test_strategy else None,
        )
        master_tasks.append(master_task)
        report["master_added"].append({"id": assigned_id, "title": title, "source": source_name})
        return assigned_id

    # 1) Promote tasks_back entries into master; update in-place with taskmaster_id
    back_promoted: dict[str, int] = {}
    for ref in targets:
        if ref.source != "tasks_back.json":
            continue
        item = find_single_by_title(back, ref.title)
        if item.get("taskmaster_id") is not None:
            continue
        tm_id = promote_from_view(item, source_name="tasks_back.json")
        item["taskmaster_id"] = int(tm_id)
        item["taskmaster_exported"] = True
        back_promoted[ref.title] = tm_id
        report["view_updates"].append(
            {"file": "tasks_back.json", "title": ref.title, "taskmaster_id": tm_id}
        )

    # 2) Promote tasks_newguild entries into master; also map into tasks_gameplay
    newguild_to_master: dict[str, int] = {}
    for ref in targets:
        if ref.source != "tasks_newguild.json":
            continue
        item = find_single_by_title(newguild, ref.title)
        # Add taskmaster_id field even if the file didn't have it historically.
        if item.get("taskmaster_id") is None:
            tm_id = promote_from_view(item, source_name="tasks_newguild.json")
            item["taskmaster_id"] = int(tm_id)
            item["taskmaster_exported"] = True
            newguild_to_master[ref.title] = tm_id
            report["view_updates"].append(
                {"file": "tasks_newguild.json", "title": ref.title, "taskmaster_id": tm_id}
            )
        else:
            newguild_to_master[ref.title] = int(item["taskmaster_id"])

    # Add mirrored entries into tasks_gameplay for the two newguild UI tasks
    for title, tm_id in newguild_to_master.items():
        existing = next(
            (x for x in gameplay if isinstance(x, dict) and x.get("taskmaster_id") == tm_id),
            None,
        )
        if existing is not None:
            continue
        src = find_single_by_title(newguild, title)
        gameplay_id = next_gameplay_id(gameplay)
        gameplay_entry = {
            "id": gameplay_id,
            "taskmaster_id": int(tm_id),
            "taskmaster_exported": True,
            "title": src.get("title"),
            "status": src.get("status", "pending"),
            "priority": "P2",
            "layer": src.get("layer", "adapter"),
            "owner": src.get("owner", ""),
            "labels": ["ui", "playability", "prd-3.3"],
            "story_id": src.get("story_id", "PRD-GUILD-MANAGER"),
            "chapter_refs": src.get("chapter_refs", ["CH03", "CH06", "CH07"]),
            "adr_refs": list(dict.fromkeys(ensure_str_list(src.get("adr_refs")) + ["ADR-0005"])),
            "overlay_refs": src.get(
                "overlay_refs",
                ["docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md"],
            ),
            "depends_on": ensure_str_list(src.get("depends_on") or src.get("dependencies")),
            "description": str(src.get("description") or ""),
            "acceptance": ensure_str_list(src.get("acceptance"))
            or [
                "UI interaction is deterministic in headless GdUnit4: modal open/close and context menu open/close are observable via nodes or labels.",
                "No stuck focus/input: ESC closes modal; right-click menu does not block navigation; actions are idempotent.",
            ],
            "test_strategy": "GdUnit4 playability (headless) + targeted xUnit for pure logic where applicable.",
            "test_refs": [],
            "contractRefs": [],
            "artifactRefs": [],
            "contractRefsNotes": "Route13 is UI behavior; no new domain contract required unless UI events are later standardized.",
        }
        gameplay.append(gameplay_entry)
        report["gameplay_added"].append({"id": gameplay_id, "taskmaster_id": tm_id, "title": title})

    # Persist
    master_obj["master"]["tasks"] = master_tasks
    write_json(master_path, master_obj)
    write_json(back_path, back)
    write_json(gameplay_path, gameplay)
    write_json(newguild_path, newguild)

    write_json(audit_dir / "report.json", report)

    print("OK: mapped view tasks into master + gameplay")
    print(f"report={audit_dir.as_posix()}/report.json")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

