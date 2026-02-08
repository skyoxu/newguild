import argparse
import datetime
import json
import pathlib
from typing import Any


def _load_json(path: pathlib.Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def _write_json(path: pathlib.Path, data: Any) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _extract_optional_hints_from_master(task: dict[str, Any]) -> list[str]:
    hints: list[str] = []

    details = (task.get("details") or "").strip()
    for test_strategy_line in _ensure_list(task.get("testStrategy")):
        line = test_strategy_line.strip()
        if line:
            hints.append(line)

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


def _parse_task_ids(expr: str) -> set[int]:
    task_ids: set[int] = set()
    tokens = [token.strip() for token in expr.split(",") if token.strip()]
    for token in tokens:
        if "-" in token:
            left, right = token.split("-", 1)
            start = int(left.strip())
            end = int(right.strip())
            if start > end:
                start, end = end, start
            for task_id in range(start, end + 1):
                task_ids.add(task_id)
            continue
        task_ids.add(int(token))
    return task_ids


def _apply_to_view_item(view_task: dict[str, Any], optional_hints: list[str]) -> dict[str, list[str]]:
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
    parser = argparse.ArgumentParser(
        description=(
            "Migrate optional hints from tasks.json master details/testStrategy "
            "into tasks_back/tasks_gameplay test_strategy with Optional: prefix."
        )
    )
    parser.add_argument(
        "--task-ids",
        default="44-51",
        help="Comma-separated task IDs and ranges. Example: 44-51,60,62",
    )
    args = parser.parse_args()

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

    round_ids = _parse_task_ids(args.task_ids)
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

    results: list[dict[str, Any]] = []
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

        target_views: list[dict[str, Any]] = []
        gameplay_view = gameplay_by_taskmaster_id.get(task_id)
        back_view = back_by_taskmaster_id.get(task_id)
        if gameplay_view is not None:
            target_views.append(gameplay_view)
        if back_view is not None:
            target_views.append(back_view)

        if not target_views:
            skipped.append({"task_id": task_id, "reason": "missing_in_views"})
            continue

        for view in target_views:
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
                    "final_optional_lines": [
                        line for line in after if line.strip().lower().startswith("optional:")
                    ],
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
                "ts": datetime.datetime.now(datetime.timezone.utc).isoformat(),
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
    changed_count = sum(1 for result in results if result["added"] or result["removed"])
    print(f"Changed: {changed_count} Skipped: {len(skipped)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
