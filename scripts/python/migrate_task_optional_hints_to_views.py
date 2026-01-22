import datetime
import json
import pathlib
from typing import Any


def _load_json(path: pathlib.Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def _write_json(path: pathlib.Path, data: Any) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _extract_optional_hints_from_master(task: dict) -> list[str]:
    hints: list[str] = []

    details = (task.get("details") or "").strip()
    test_strategy = (task.get("testStrategy") or "").strip()

    if test_strategy:
        hints.append(test_strategy)

    if details:
        # Master details can be written as " / " chunks or as multi-line blocks.
        # Normalize both forms into flat " / " chunks.
        normalized = details.replace("\r", "").replace("\n", " / ")
        parts = [p.strip() for p in normalized.split(" / ") if p.strip()]
        for part in parts:
            low = part.lower()
            if low.startswith("rewrite-intent:") or "rewrite-intent:" in low:
                hints.append(part)
                continue
            if low.startswith("demo-path:") or "demo-path:" in low:
                hints.append(part)
                continue
            if low.startswith("hardening:") or "hardening:" in low:
                hints.append(part)
                continue
            if low.startswith("fortify:") or "fortify:" in low:
                hints.append(part)
                continue
            if low.startswith("stop-loss:") or "stop-loss:" in low:
                hints.append(part)
                continue
            if low.startswith("optional:") or "optional:" in low:
                hints.append(part)
                continue

    # De-dup but keep order.
    seen = set()
    unique: list[str] = []
    for h in hints:
        if h in seen:
            continue
        seen.add(h)
        unique.append(h)
    return unique


def _normalize_optional_line(text: str) -> str:
    text = text.strip()
    if not text:
        return ""
    if text.lower().startswith("optional:"):
        return "Optional:" + text[len("Optional:") :].lstrip()
    return "Optional: " + text


def _ensure_list(value: Any) -> list[str]:
    if value is None:
        return []
    if isinstance(value, list):
        return [str(x) for x in value]
    if isinstance(value, str):
        v = value.strip()
        return [v] if v else []
    return [str(value)]


def _apply_to_view_item(view_task: dict, optional_hints: list[str]) -> dict:
    before = _ensure_list(view_task.get("test_strategy"))

    # Keep non-optional lines as the stable "mandatory" strategy, and regenerate optional lines
    # from the master task hints to avoid drift and multi-line blobs.
    base_lines = [l for l in before if not l.strip().lower().startswith("optional:")]
    after = list(base_lines)

    for hint in optional_hints:
        line = _normalize_optional_line(hint)
        if not line or line in after:
            continue
        after.append(line)

    if after != before:
        view_task["test_strategy"] = after
    return {"before": before, "after": after}


def main() -> int:
    root = pathlib.Path(__file__).resolve().parents[2]
    tasks_path = root / ".taskmaster" / "tasks" / "tasks.json"
    gameplay_path = root / ".taskmaster" / "tasks" / "tasks_gameplay.json"
    back_path = root / ".taskmaster" / "tasks" / "tasks_back.json"

    tasks_obj = _load_json(tasks_path)
    gameplay_obj = _load_json(gameplay_path)
    back_obj = _load_json(back_path)

    if not isinstance(gameplay_obj, list) or not isinstance(back_obj, list):
        raise TypeError("Expected tasks_gameplay.json and tasks_back.json to be top-level lists.")

    master_tasks = tasks_obj.get("master", {}).get("tasks", [])
    if not isinstance(master_tasks, list):
        raise TypeError("Expected tasks.json master.tasks to be a list.")

    # This round: Phase2 (T27..T43).
    round_ids = set(range(27, 44))
    master_by_id = {int(t["id"]): t for t in master_tasks if str(t.get("id", "")).isdigit()}

    gameplay_by_taskmaster_id = {
        int(t["taskmaster_id"]): t
        for t in gameplay_obj
        if str(t.get("taskmaster_id", "")).isdigit()
    }
    back_by_taskmaster_id = {
        int(t["taskmaster_id"]): t
        for t in back_obj
        if str(t.get("taskmaster_id", "")).isdigit()
    }

    results: list[dict] = []
    skipped: list[dict] = []

    for task_id in sorted(round_ids):
        master = master_by_id.get(task_id)
        if not master:
            skipped.append({"task_id": task_id, "reason": "missing_in_master"})
            continue

        optional_hints = _extract_optional_hints_from_master(master)
        if not optional_hints:
            skipped.append({"task_id": task_id, "reason": "no_optional_hints"})
            continue

        view = gameplay_by_taskmaster_id.get(task_id) or back_by_taskmaster_id.get(task_id)
        if not view:
            skipped.append({"task_id": task_id, "reason": "missing_in_views"})
            continue

        apply_result = _apply_to_view_item(view, optional_hints)
        before = apply_result["before"]
        after = apply_result["after"]
        before_set = set(before)
        after_set = set(after)
        added = [line for line in after if line not in before_set]
        removed = [line for line in before if line not in after_set]

        results.append(
            {
                "task_id": task_id,
                "view_id": view.get("id"),
                "before_len": len(before),
                "after_len": len(after),
                "added": added,
                "removed": removed,
                "source_optional_hints": optional_hints,
                "final_optional_lines": [l for l in after if l.strip().lower().startswith("optional:")],
            }
        )

    _write_json(gameplay_path, gameplay_obj)
    _write_json(back_path, back_obj)

    out_dir = root / "logs" / "ci" / datetime.date.today().isoformat() / "task-mapping"
    out_dir.mkdir(parents=True, exist_ok=True)
    report_path = out_dir / "migrate_task_optional_hints_to_views.json"
    report_path.write_text(
        json.dumps(
            {
                "ts": datetime.datetime.utcnow().isoformat() + "Z",
                "round_task_ids": sorted(round_ids),
                "results": results,
                "skipped": skipped,
            },
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )

    print(f"Wrote {report_path}")
    changed_count = sum(1 for r in results if r["added"] or r["removed"])
    print(f"Changed: {changed_count} Skipped: {len(skipped)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
