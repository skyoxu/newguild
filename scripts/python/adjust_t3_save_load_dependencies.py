#!/usr/bin/env python3
"""
Adjust master dependencies for T3 Save/Load tasks.

Why:
- Task 25 is a crosscutting capability (Save/Load + schema/migration). It should not be blocked by
  feature tasks (e.g., T3 roster/recruitment) that will later consume persistence.
- The repo uses `.taskmaster/tasks/tasks.json` as the SSoT for status/dependencies.

This script:
- Removes Task 13/14 from Task 25 dependencies (if present)
- Ensures Task 26 depends on Task 25
- Writes an audit report under `logs/ci/<YYYY-MM-DD>/task-mapping/`

All reads/writes are UTF-8.
"""

from __future__ import annotations

import datetime as dt
import json
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[2]
MASTER_PATH = REPO_ROOT / ".taskmaster" / "tasks" / "tasks.json"


def _read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def _write_json_pretty(path: Path, obj: Any) -> None:
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _today_dir() -> Path:
    day = dt.date.today().isoformat()
    out_dir = REPO_ROOT / "logs" / "ci" / day / "task-mapping"
    out_dir.mkdir(parents=True, exist_ok=True)
    return out_dir


def _normalize_deps(deps: list[Any] | None) -> list[str]:
    if not deps:
        return []
    return [str(x) for x in deps]


def main() -> int:
    master_obj = _read_json(MASTER_PATH)
    tasks: list[dict[str, Any]] = master_obj["master"]["tasks"]
    by_id = {str(t.get("id")): t for t in tasks}

    report: dict[str, Any] = {
        "ts": dt.datetime.now().replace(microsecond=0).isoformat(),
        "changes": [],
        "warnings": [],
    }

    core = by_id.get("25")
    if not core:
        report["warnings"].append("Task 25 not found; no changes applied.")
    else:
        before = _normalize_deps(core.get("dependencies"))
        after = [d for d in before if d not in {"13", "14"}]
        if after != before:
            core["dependencies"] = after
            report["changes"].append(
                {
                    "task_id": "25",
                    "field": "dependencies",
                    "before": before,
                    "after": after,
                }
            )

    ui = by_id.get("26")
    if not ui:
        report["warnings"].append("Task 26 not found; no changes applied.")
    else:
        before = _normalize_deps(ui.get("dependencies"))
        if "25" not in before:
            after = before + ["25"]
            ui["dependencies"] = after
            report["changes"].append(
                {
                    "task_id": "26",
                    "field": "dependencies",
                    "before": before,
                    "after": after,
                }
            )

    if report["changes"]:
        _write_json_pretty(MASTER_PATH, master_obj)

    out_dir = _today_dir()
    out_json = out_dir / "t3-save-load-deps-adjust.json"
    out_txt = out_dir / "t3-save-load-deps-adjust.txt"
    _write_json_pretty(out_json, report)
    out_txt.write_text(
        "\n".join(
            [
                f"ts={report['ts']}",
                f"changes={len(report['changes'])}",
                *(f"change: {c}" for c in report["changes"]),
                *(f"warning: {w}" for w in report["warnings"]),
                "",
            ]
        ),
        encoding="utf-8",
    )

    print(f"[REPORT] {out_json}")
    print(f"[REPORT] {out_txt}")
    print("[OK] Done.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

