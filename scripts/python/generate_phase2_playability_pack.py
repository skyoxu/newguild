"""
Generate a closed-loop (playability-oriented) acceptance pack document for Phase 2 done tasks.

This script follows repo constraints:
- Windows-friendly (run with: py -3 scripts/python/generate_phase2_playability_pack.py)
- UTF-8 read/write for documents
- Writes an evidence JSON and the markdown pack under taskdoc/
"""

from __future__ import annotations

import json
import re
from dataclasses import dataclass
from datetime import date
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[2]


PHASE2_DONE_TASK_IDS: list[int] = [
    43,
    42,
    27,
    28,
    29,
    30,
    31,
    32,
    33,
    34,
    35,
    37,
    36,
    38,
    39,
    40,
    41,
]


@dataclass(frozen=True)
class TaskView:
    task_id: int
    title: str
    master_status: str
    view: str
    view_status: str | None
    layer: str | None
    contract_refs: list[str]
    test_refs: list[str]
    gdunit_acc_files: list[str]
    xunit_acc_files: list[str]


def _read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def _scan_acc_refs(root: Path) -> dict[int, list[str]]:
    rx = re.compile(r"ACC:T(\d+)\.(\d+)", re.IGNORECASE)
    out: dict[int, set[str]] = {}
    for file_path in root.rglob("*"):
        if not file_path.is_file():
            continue
        if file_path.suffix.lower() not in (".gd", ".cs"):
            continue
        text = file_path.read_text(encoding="utf-8", errors="ignore")
        for match in rx.finditer(text):
            task_id = int(match.group(1))
            out.setdefault(task_id, set()).add(file_path.as_posix())
    return {k: sorted(v) for k, v in out.items()}


def _load_task_views() -> dict[int, TaskView]:
    master = _read_json(REPO_ROOT / ".taskmaster/tasks/tasks.json")["master"]["tasks"]
    master_by: dict[int, dict[str, Any]] = {int(t["id"]): t for t in master}

    back = _read_json(REPO_ROOT / ".taskmaster/tasks/tasks_back.json")
    gameplay = _read_json(REPO_ROOT / ".taskmaster/tasks/tasks_gameplay.json")
    back_by: dict[int, dict[str, Any]] = {
        int(t["taskmaster_id"]): t for t in back if t.get("taskmaster_id") is not None
    }
    gameplay_by: dict[int, dict[str, Any]] = {
        int(t["taskmaster_id"]): t for t in gameplay if t.get("taskmaster_id") is not None
    }

    gd_acc = _scan_acc_refs(REPO_ROOT / "Tests.Godot/tests")
    cs_acc = _scan_acc_refs(REPO_ROOT / "Game.Core.Tests")

    views: dict[int, TaskView] = {}
    for task_id in PHASE2_DONE_TASK_IDS:
        master_task = master_by.get(task_id)
        if master_task is None:
            raise KeyError(f"Task {task_id} is missing from master tasks.json")

        back_task = back_by.get(task_id)
        gameplay_task = gameplay_by.get(task_id)

        if back_task and not gameplay_task:
            view = "back"
            view_task = back_task
        elif gameplay_task and not back_task:
            view = "gameplay"
            view_task = gameplay_task
        elif back_task and gameplay_task:
            view = "both"
            view_task = gameplay_task
        else:
            view = "none"
            view_task = None

        contract_refs = (view_task or back_task or {}).get("contractRefs") or []
        test_refs = (view_task or back_task or {}).get("test_refs") or []

        views[task_id] = TaskView(
            task_id=task_id,
            title=str(master_task.get("title") or ""),
            master_status=str(master_task.get("status") or ""),
            view=view,
            view_status=(view_task or {}).get("status") if view_task else None,
            layer=(view_task or {}).get("layer") if view_task else None,
            contract_refs=list(contract_refs),
            test_refs=list(test_refs),
            gdunit_acc_files=gd_acc.get(task_id, []),
            xunit_acc_files=cs_acc.get(task_id, []),
        )

    return views


def _render_markdown(views: dict[int, TaskView], ci_date: str) -> str:
    lines: list[str] = []
    lines.append("# Phase 2 Playability Acceptance Pack (Repo-Derived)")
    lines.append("")
    lines.append(
        "This pack is derived from the current repository state (scanned from task views + test anchors). "
        "Goal: make the already-implemented Phase 2 work verifiable as a closed-loop playable route."
    )
    lines.append("")

    lines.append("## Runtime Entry")
    lines.append("")
    lines.append("- Main scene: `res://Game.Godot/Scenes/Main.tscn` (from `project.godot:run/main_scene`)")
    lines.append("- Menu event types: `ui.menu.start|guild|settings|activity|quit`")
    lines.append("  - C#: `Game.Core/Contracts/UI/UiMenuEventTypes.cs`")
    lines.append("  - GDScript: `Game.Godot/Scripts/UI/UiMenuEventTypes.gd`")
    lines.append("- Screens: `Game.Godot/Scenes/Screens/StartScreen.tscn`, `GuildScreen.tscn`, `ActivityFeedScreen.tscn`")
    lines.append("- Key autoloads: `/root/EventBus`, `/root/DataStore`, `/root/GuildManager`, `/root/CompositionRoot`")
    lines.append("")
    lines.append("## Manual Play Routes (Godot Editor F5)")
    lines.append("")
    lines.append("1) Boot + Navigation: MainMenu -> Activity/Guild/Settings/Play -> Back -> Menu")
    lines.append("2) Activity Feed: verify expected domain events are visible (allowlist-based)")
    lines.append("3) Save/Load: Play -> StartScreen -> Save+Load -> Activity shows `core.save.*` and `core.load.*`")
    lines.append("4) Raid demo: StartScreen -> Demo Raid -> observe `core.raid.scheduled` + `core.raid.resolved`")
    lines.append("5) Media beat: HUD MediaBeatButton -> observe `core.media.beat.triggered`")
    lines.append("6) Reputation: StartScreen -> Demo Reputation -> observe `core.reputation.changed`")
    lines.append("7) Guild vertical slice: Guild -> Create -> Roster actions -> Recruitment actions -> Activity events + UI updates")
    lines.append("")

    lines.append("## Automated Gates")
    lines.append("")
    lines.append("- xUnit (Core): `dotnet test .\\Game.Core.Tests\\Game.Core.Tests.csproj -c Debug`")
    lines.append("- GdUnit4 (Godot): run the `Tests.Godot` test project via CI runner (`scripts/python/godot_tests.py`).")
    lines.append("")

    lines.append("## Coverage Matrix (Phase 2 done tasks)")
    lines.append("")
    lines.append("| Task | Title | View | test_refs | GdUnit ACC files | xUnit ACC files | Notes |")
    lines.append("|---:|---|---|---:|---:|---:|---|")
    for task_id in PHASE2_DONE_TASK_IDS:
        t = views[task_id]
        notes: list[str] = []
        gd_test_refs = [p for p in t.test_refs if p.lower().endswith(".gd")]
        if gd_test_refs and not t.gdunit_acc_files:
            notes.append("GdUnit tests referenced but missing ACC anchors (attribution gap)")
        if t.view == "gameplay" and t.view_status not in (None, "done"):
            notes.append("tasks_gameplay status != done (metadata drift vs master)")
        lines.append(
            f"| T{t.task_id} | {t.title.replace('|','/')} | {t.view} | {len(t.test_refs)} | "
            f"{len(t.gdunit_acc_files)} | {len(t.xunit_acc_files)} | {'; '.join(notes)} |"
        )
    lines.append("")

    lines.append("## Per-Task Test Refs")
    lines.append("")
    for task_id in PHASE2_DONE_TASK_IDS:
        t = views[task_id]
        lines.append(f"### T{t.task_id}: {t.title}")
        lines.append("")
        lines.append(f"- master status: `{t.master_status}`")
        lines.append(f"- view: `{t.view}` (view status: `{t.view_status}` layer: `{t.layer}`)")
        if t.contract_refs:
            lines.append(f"- contractRefs ({len(t.contract_refs)}):")
            for ev in t.contract_refs:
                lines.append(f"  - `{ev}`")
        lines.append(f"- test_refs ({len(t.test_refs)}):")
        for ref in t.test_refs:
            lines.append(f"  - `{ref}`")
        if t.gdunit_acc_files:
            lines.append(f"- ACC anchors (GdUnit) ({len(t.gdunit_acc_files)} files):")
            for p in t.gdunit_acc_files:
                lines.append(f"  - `{p}`")
        if t.xunit_acc_files:
            lines.append(f"- ACC anchors (xUnit) ({len(t.xunit_acc_files)} files):")
            for p in t.xunit_acc_files:
                lines.append(f"  - `{p}`")
        lines.append("")

    lines.append("## Known Playability Gaps")
    lines.append("")
    lines.append("- Some done tasks are core-only capabilities (contracts/engines/gates). They can be validated by xUnit without a dedicated UI entry.")
    lines.append("- ActivityFeed is allowlist-based. Domain events not included in allowlist are not observable via manual play routes.")
    lines.append("- If a dedicated Tactical Center screen is required, add a `ui.menu.tactical` route + screen + GdUnit scenario.")
    lines.append("")
    lines.append("## Evidence")
    lines.append("")
    lines.append(f"- Scan artifact: `logs/ci/{ci_date}/phase2-playability-pack-scan.json`")
    lines.append("")
    return "\n".join(lines)


def main() -> int:
    ci_date = date.today().isoformat()

    views = _load_task_views()

    out_dir = REPO_ROOT / "logs/ci" / ci_date / "playability-pack"
    out_dir.mkdir(parents=True, exist_ok=True)

    evidence_path = out_dir / "phase2-playability-pack-evidence.json"
    evidence = {
        "ci_date": ci_date,
        "task_ids": PHASE2_DONE_TASK_IDS,
        "tasks": {
            str(k): {
                "title": v.title,
                "master_status": v.master_status,
                "view": v.view,
                "view_status": v.view_status,
                "layer": v.layer,
                "contractRefs": v.contract_refs,
                "test_refs": v.test_refs,
                "acc": {"gdunit": v.gdunit_acc_files, "xunit": v.xunit_acc_files},
            }
            for k, v in views.items()
        },
    }
    evidence_path.write_text(json.dumps(evidence, ensure_ascii=False, indent=2), encoding="utf-8")

    md = _render_markdown(views, ci_date=ci_date)
    out_md = REPO_ROOT / "taskdoc/phase2-playability-acceptance-pack.md"
    out_md.parent.mkdir(parents=True, exist_ok=True)
    out_md.write_text(md, encoding="utf-8")

    print(f"WROTE {out_md.as_posix()}")
    print(f"WROTE {evidence_path.as_posix()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
