import argparse
import datetime
import json
import pathlib
import re
from typing import Any, Iterable


def _load_json(path: pathlib.Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def _write_text(path: pathlib.Path, text: str) -> None:
    path.write_text(text, encoding="utf-8")


def _write_json(path: pathlib.Path, obj: Any) -> None:
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _iter_master_tasks(tasks_json: dict) -> Iterable[dict]:
    master = tasks_json.get("master", {})
    tasks = master.get("tasks", [])
    if not isinstance(tasks, list):
        return []
    return tasks


def _read_event_types(contracts_root: pathlib.Path) -> set[str]:
    event_types: set[str] = set()
    marker = "public const string EventType = "
    for cs in contracts_root.rglob("*.cs"):
        for line in cs.read_text(encoding="utf-8").splitlines():
            if marker not in line:
                continue
            tail = line.split(marker, 1)[1].strip()
            if tail.startswith("\"") and "\"" in tail[1:]:
                event_types.add(tail.split("\"", 2)[1])
    return event_types


def _get_view_task_by_taskmaster_id(view_list: list[dict], task_id: int) -> dict | None:
    for t in view_list:
        raw = t.get("taskmaster_id")
        if str(raw).isdigit() and int(raw) == task_id:
            return t
    return None


def _normalize_lines(value: Any) -> list[str]:
    if value is None:
        return []
    if isinstance(value, list):
        return [str(x).strip() for x in value if str(x).strip()]
    if isinstance(value, str):
        v = value.strip()
        return [v] if v else []
    return [str(value).strip()]


def _split_master_details(details: str) -> list[str]:
    normalized = (details or "").replace("\r", "").replace("\n", " / ")
    return [p.strip() for p in normalized.split(" / ") if p.strip()]


def _extract_story_hints(master_task: dict) -> dict[str, str]:
    details = master_task.get("details") or ""
    parts = _split_master_details(details)
    hints: dict[str, str] = {}
    for part in parts:
        if ":" not in part:
            continue
        key, value = part.split(":", 1)
        k = key.strip().lower()
        v = value.strip()
        if not v:
            continue
        if k in {"story", "source-newguild", "rewrite-intent", "source-title"}:
            hints[k] = v
    return hints


def _derive_obligations(task_id: int, master_task: dict, view_task: dict, known_event_types: set[str]) -> dict:
    layer = (view_task.get("layer") or "").strip()
    title = (master_task.get("title") or view_task.get("title") or "").strip()
    description = (master_task.get("description") or view_task.get("description") or "").strip()

    contract_refs = _normalize_lines(view_task.get("contractRefs"))
    contract_refs = [c for c in contract_refs if c in known_event_types]

    test_refs = _normalize_lines(view_task.get("test_refs"))
    artifact_refs = _normalize_lines(view_task.get("artifactRefs"))
    overlay_refs = _normalize_lines(view_task.get("overlay_refs"))
    acceptance = _normalize_lines(view_task.get("acceptance"))

    master_test_strategy = (master_task.get("testStrategy") or "").strip()
    view_test_strategy = _normalize_lines(view_task.get("test_strategy"))

    hints = _extract_story_hints(master_task)

    obligations: dict[str, list[dict]] = {
        "Functional": [],
        "Contracts": [],
        "Tests": [],
        "Observability": [],
        "Wiring": [],
        "Gates": [],
    }

    # Functional (best-effort, anchored to title/description)
    if title:
        obligations["Functional"].append(
            {
                "must": f"Deliver: {title}",
                "evidence": "acceptance[] should describe the user-visible result; tests should cover critical behaviors.",
            }
        )
    if description:
        obligations["Functional"].append(
            {
                "must": f"Meet description: {description}",
                "evidence": "acceptance[] should include a deterministic checkable statement.",
            }
        )

    # Contracts (minimum coverage via contractRefs)
    if contract_refs:
        obligations["Contracts"].append(
            {
                "must": "Ensure contractRefs is the minimal coverage set for the task (publish/consume).",
                "evidence": f"contractRefs={contract_refs}",
            }
        )
        for evt in contract_refs:
            obligations["Contracts"].append(
                {
                    "must": f"Publish/consume domain event type: {evt}",
                    "evidence": "DomainEvent.Type matches EventType constants; consumers should key off evt.Type.",
                }
            )

    # Tests
    if test_refs:
        obligations["Tests"].append(
            {
                "must": "Keep referenced tests present and meaningful.",
                "evidence": f"test_refs={test_refs}",
            }
        )
    else:
        obligations["Tests"].append(
            {
                "must": "Add at least one deterministic test (xUnit for core; GdUnit4 for Godot wiring when applicable).",
                "evidence": "test_refs is empty; add a concrete file path for auditability.",
            }
        )

    if view_test_strategy:
        obligations["Tests"].append(
            {
                "must": "Follow test strategy captured in view.test_strategy (SSoT for optional hints).",
                "evidence": f"test_strategy={view_test_strategy}",
            }
        )
    elif master_test_strategy:
        obligations["Tests"].append(
            {
                "must": "Follow test strategy captured in master.testStrategy.",
                "evidence": master_test_strategy,
            }
        )

    # Observability
    if artifact_refs:
        obligations["Observability"].append(
            {
                "must": "Produce audit artifacts under logs/** as declared.",
                "evidence": f"artifactRefs={artifact_refs}",
            }
        )
    else:
        obligations["Observability"].append(
            {
                "must": "Write evidence artifacts under logs/** (ci/e2e/unit/perf).",
                "evidence": "artifactRefs is empty; add a minimal anchor if this task touches gates/tests.",
            }
        )

    # Wiring
    if layer in {"adapter", "ui", "scene"}:
        obligations["Wiring"].append(
            {
                "must": "Verify UI/clickability and ensure no invisible blockers prevent interaction.",
                "evidence": "GdUnit4 headless smoke + a manual path in acceptance.",
            }
        )
    if contract_refs and layer in {"adapter", "ui", "scene"}:
        obligations["Wiring"].append(
            {
                "must": "Ensure UI actually consumes the events listed in contractRefs (no dead subscriptions).",
                "evidence": "acceptance should mention an observable UI update driven by those events.",
            }
        )

    # Gates
    if "ADR-0005" in _normalize_lines(view_task.get("adr_refs")):
        obligations["Gates"].append(
            {
                "must": "Keep quality gates passing (build/tests/coverage where applicable).",
                "evidence": "ADR-0005 referenced; gates must be deterministic and runnable on Windows.",
            }
        )

    return {
        "task_id": task_id,
        "view_id": view_task.get("id"),
        "layer": layer,
        "title": title,
        "overlay_refs": overlay_refs,
        "hints": hints,
        "derived_obligations": obligations,
        "view_acceptance": acceptance,
        "view_contractRefs": contract_refs,
        "view_test_refs": test_refs,
        "view_artifactRefs": artifact_refs,
    }


def _assess_acceptance_gaps(derived: dict) -> dict:
    acceptance = derived["view_acceptance"]
    contract_refs = derived["view_contractRefs"]
    test_refs = derived["view_test_refs"]
    artifact_refs = derived["view_artifactRefs"]
    layer = derived["layer"]

    gaps: list[str] = []

    if len(acceptance) < 3:
        gaps.append(f"acceptance too short (len={len(acceptance)}), recommend >= 3 statements.")

    # If contracts exist, acceptance should mention an observable publish/consume.
    if contract_refs:
        blob = " ".join(acceptance).lower()
        if not any(k in blob for k in ["event", "publish", "emit", "subscribe", "consume", "type", "domain"]):
            gaps.append("contractRefs present but acceptance does not mention event publish/consume.")

    # If adapter/UI, acceptance should mention clickability/wiring.
    if layer in {"adapter", "ui", "scene"}:
        blob = " ".join(acceptance).lower()
        if not any(k in blob for k in ["click", "clickable", "button", "ui", "screen", "signal"]):
            gaps.append("adapter/UI task but acceptance lacks UI wiring/clickability statement.")

    if not test_refs:
        gaps.append("test_refs empty (should point to concrete tests).")

    if not artifact_refs:
        gaps.append("artifactRefs empty (missing audit anchor under logs/**).")

    return {"task_id": derived["task_id"], "view_id": derived["view_id"], "gaps": gaps}


def _render_md(summary: dict) -> str:
    lines: list[str] = []
    lines.append(f"# Task Obligations Audit (T{summary['task_id_start']}..T{summary['task_id_end']})")
    lines.append("\nGenerated by `scripts/python/llm_extract_task_obligations.py`.\n")
    lines.append(f"- Date: {summary['date']}\n")
    lines.append("## Summary\n")
    lines.append(f"- Tasks analyzed: {summary['tasks_analyzed']}\n")
    lines.append(f"- Tasks with acceptance gaps: {summary['tasks_with_gaps']}\n")
    lines.append("\n## Per Task\n")
    for task in summary["tasks"]:
        lines.append(f"### T{task['task_id']} ({task.get('view_id')})\n")
        lines.append(f"- Layer: {task.get('layer') or '(unknown)'}\n")
        lines.append(f"- Title: {task.get('title') or ''}\n")
        if task.get("overlay_refs"):
            lines.append(f"- Overlay refs: {', '.join(task['overlay_refs'])}\n")

        gaps = next((g for g in summary["gaps"] if g["task_id"] == task["task_id"]), None)
        if gaps and gaps["gaps"]:
            lines.append("- Acceptance gaps:\n")
            for gap in gaps["gaps"]:
                lines.append(f"  - {gap}\n")
        else:
            lines.append("- Acceptance gaps: none\n")

        lines.append("- Obligations:\n")
        for cat, items in task["derived_obligations"].items():
            if not items:
                continue
            lines.append(f"  - {cat}:\n")
            for it in items:
                lines.append(f"    - Must: {it['must']}\n")
                lines.append(f"      Evidence: {it['evidence']}\n")
        lines.append("\n")
    return "".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--task-id-start", type=int, default=27)
    parser.add_argument("--task-id-end", type=int, default=43)
    args = parser.parse_args()

    root = pathlib.Path(__file__).resolve().parents[2]
    methodology_path = root / "docs" / "workflows" / "acceptance-semantics-methodology.md"
    if not methodology_path.exists():
        raise FileNotFoundError(f"Missing methodology doc: {methodology_path}")

    tasks_path = root / ".taskmaster" / "tasks" / "tasks.json"
    gameplay_path = root / ".taskmaster" / "tasks" / "tasks_gameplay.json"
    back_path = root / ".taskmaster" / "tasks" / "tasks_back.json"
    contracts_root = root / "Game.Core" / "Contracts"

    tasks_obj = _load_json(tasks_path)
    gameplay_obj = _load_json(gameplay_path)
    back_obj = _load_json(back_path)

    if not isinstance(gameplay_obj, list) or not isinstance(back_obj, list):
        raise TypeError("Expected tasks_gameplay.json and tasks_back.json to be top-level lists.")

    known_event_types = _read_event_types(contracts_root)

    master_by_id: dict[int, dict] = {}
    for t in _iter_master_tasks(tasks_obj):
        raw = t.get("id")
        if str(raw).isdigit():
            master_by_id[int(raw)] = t

    tasks: list[dict] = []
    gaps: list[dict] = []

    for task_id in range(args.task_id_start, args.task_id_end + 1):
        master = master_by_id.get(task_id)
        if not master:
            continue

        view = _get_view_task_by_taskmaster_id(gameplay_obj, task_id) or _get_view_task_by_taskmaster_id(
            back_obj, task_id
        )
        if not view:
            continue

        derived = _derive_obligations(task_id, master, view, known_event_types)
        tasks.append(derived)
        gaps.append(_assess_acceptance_gaps(derived))

    date = datetime.date.today().isoformat()
    out_dir = root / "logs" / "ci" / date / "acceptance-obligations"
    out_dir.mkdir(parents=True, exist_ok=True)

    summary = {
        "ts": datetime.datetime.utcnow().isoformat() + "Z",
        "date": date,
        "methodology": str(methodology_path.relative_to(root)).replace("\\", "/"),
        "task_id_start": args.task_id_start,
        "task_id_end": args.task_id_end,
        "tasks_analyzed": len(tasks),
        "tasks_with_gaps": sum(1 for g in gaps if g["gaps"]),
        "tasks": tasks,
        "gaps": gaps,
    }

    out_json = out_dir / "obligations.json"
    out_md = out_dir / "obligations.md"
    _write_json(out_json, summary)
    _write_text(out_md, _render_md(summary))

    print(f"Wrote {out_json}")
    print(f"Wrote {out_md}")
    print(f"tasks_analyzed={summary['tasks_analyzed']} tasks_with_gaps={summary['tasks_with_gaps']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
