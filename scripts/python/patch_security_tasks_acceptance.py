#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Patch NG-0014/NG-0042 acceptance fields in .taskmaster/tasks/tasks_back.json
to incorporate clarified security requirements (B3/B5/C3) using UTF-8.
"""

from __future__ import annotations

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
TASKS_BACK_FILE = ROOT / ".taskmaster" / "tasks" / "tasks_back.json"


def main() -> None:
    if not TASKS_BACK_FILE.exists():
        raise SystemExit(f"tasks_back.json not found at {TASKS_BACK_FILE}")

    data = json.loads(TASKS_BACK_FILE.read_text(encoding="utf-8"))
    if not isinstance(data, list):
        raise SystemExit("tasks_back.json is expected to be a JSON array.")

    changed = False

    for task in data:
        if not isinstance(task, dict):
            continue
        tid = task.get("id")
        acceptance = task.get("acceptance")
        if tid == "NG-0014" and isinstance(acceptance, list):
            # Insert a new acceptance line after the first item if not already present.
            line = (
                "SecurityUrlAdapter 在引入 ALLOWED_EXTERNAL_HOSTS 白名单（可通过环境变量或配置）时，"
                "默认开启安全收敛：当 allowedDomains == null 时不会在生产环境下“全部放行”，"
                "而是保持本地/测试环境明确配置或显式 opt-in，符合 ADR-0019 对外链白名单的防御性要求。"
            )
            if line not in acceptance:
                acceptance.insert(1, line)
                task["acceptance"] = acceptance
                changed = True

        if tid == "NG-0042" and isinstance(acceptance, list):
            updated_items = []
            for item in acceptance:
                if isinstance(item, str) and "SqliteDataStore" in item and " .. " in item and "路径" in item:
                    # Refine path traversal acceptance to mention both ../ and ..\
                    new_item = item.replace("包含 .. 的路径", "包含 .. 或 ..\\\\ 的路径")
                    updated_items.append(new_item)
                    if new_item != item:
                        changed = True
                else:
                    updated_items.append(item)
            task["acceptance"] = updated_items

    if changed:
        TASKS_BACK_FILE.write_text(
            json.dumps(data, ensure_ascii=False, indent=2),
            encoding="utf-8",
        )
        print(f"Updated acceptance for NG-0014/NG-0042 in {TASKS_BACK_FILE}")
    else:
        print("No changes applied (acceptance already up to date).")


if __name__ == "__main__":
    main()

