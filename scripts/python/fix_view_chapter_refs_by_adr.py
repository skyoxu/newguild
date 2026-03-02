#!/usr/bin/env python3
"""Check/fix chapter_refs in view task files based on adr_refs -> ADR_FOR_CH mapping.

Default behavior is non-destructive check-only. Use --write to apply changes.
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
from pathlib import Path
from typing import Any

from check_tasks_all_refs import ADR_FOR_CH


VIEW_BACK = ".taskmaster/tasks/tasks_back.json"
VIEW_GAMEPLAY = ".taskmaster/tasks/tasks_gameplay.json"


def _today() -> str:
    return dt.date.today().strftime("%Y-%m-%d")


def _load_json_list(path: Path) -> list[dict[str, Any]]:
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, list):
        raise ValueError(f"{path.as_posix()} must be a JSON array")
    return data


def _write_json_list(path: Path, data: list[dict[str, Any]]) -> None:
    path.write_text(
        json.dumps(data, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )


def _expected_chapters(task: dict[str, Any]) -> list[str]:
    adr_refs = task.get("adr_refs")
    if not isinstance(adr_refs, list):
        return []
    expected: set[str] = set()
    for adr in adr_refs:
        mapped = ADR_FOR_CH.get(str(adr))
        if mapped:
            expected.update(mapped)
    return sorted(expected)


def _normalize_chapters(value: Any) -> list[str]:
    if not isinstance(value, list):
        return []
    return sorted({str(v) for v in value})


def _process_file(path: Path, *, write: bool) -> dict[str, Any]:
    tasks = _load_json_list(path)
    changed = 0
    items: list[dict[str, Any]] = []

    for task in tasks:
        expected = _expected_chapters(task)
        current = _normalize_chapters(task.get("chapter_refs"))
        if current != expected:
            changed += 1
            items.append(
                {
                    "id": task.get("id"),
                    "taskmaster_id": task.get("taskmaster_id"),
                    "before": current,
                    "after": expected,
                }
            )
            task["chapter_refs"] = expected

    if write and changed > 0:
        _write_json_list(path, tasks)

    return {
        "file": path.as_posix(),
        "tasks": len(tasks),
        "changed": changed,
        "changes": items,
    }


def _mode_paths(root: Path, mode: str) -> list[Path]:
    if mode == "back":
        return [root / VIEW_BACK]
    if mode == "gameplay":
        return [root / VIEW_GAMEPLAY]
    return [root / VIEW_BACK, root / VIEW_GAMEPLAY]


def run_fix(root: Path, *, mode: str = "all", write: bool = True) -> dict[str, Any]:
    files = _mode_paths(root, mode)
    file_reports = [_process_file(path, write=write) for path in files]
    changed_total = sum(int(r["changed"]) for r in file_reports)
    result = {
        "action": "fix-view-chapter-refs-by-adr",
        "date": _today(),
        "mode": mode,
        "write": write,
        "changed_total": changed_total,
        "files": file_reports,
    }

    out_dir = root / "logs" / "ci" / _today() / "task-mapping"
    out_dir.mkdir(parents=True, exist_ok=True)
    out_file = out_dir / "fix-view-chapter-refs-by-adr.json"
    out_file.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    result["report_file"] = out_file.as_posix()
    return result


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Check/fix chapter_refs in view task files based on adr_refs map.",
    )
    parser.add_argument(
        "--mode",
        choices=["all", "back", "gameplay"],
        default="all",
        help="Select which view files to fix.",
    )
    parser.add_argument(
        "--write",
        action="store_true",
        help="Apply fixes to files. Default is check-only (no write).",
    )
    parser.add_argument(
        "--check",
        action="store_true",
        help="Deprecated compatibility flag; check-only is already the default.",
    )
    args = parser.parse_args()

    root = Path(__file__).resolve().parents[2]
    result = run_fix(root, mode=args.mode, write=args.write)
    status = "ok" if result["changed_total"] == 0 else ("fixed" if args.write else "drift")
    print(
        "FIX_VIEW_CHAPTER_REFS "
        f"status={status} mode={args.mode} changed={result['changed_total']} report={result['report_file']}"
    )
    return 0 if status in {"ok", "fixed"} else 1


if __name__ == "__main__":
    raise SystemExit(main())
