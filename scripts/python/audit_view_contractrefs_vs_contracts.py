#!/usr/bin/env python3
"""
Audit view task files:
- contractRefs must reference existing EventType constants in Game.Core/Contracts/** (A+exceptions policy).
- artifactRefs must exist as a list field in view tasks.

Outputs (forensics):
- logs/ci/<YYYY-MM-DD>/task-mapping/contractrefs-audit-after-artifactrefs.json
"""

from __future__ import annotations

import datetime as dt
import json
import re
from pathlib import Path
from typing import Any


def today_str() -> str:
    return dt.date.today().strftime("%Y-%m-%d")


def write_json(path: Path, obj: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")


def normalize_list(value: Any) -> list[str]:
    if value is None:
        return []
    if isinstance(value, list):
        out: list[str] = []
        for item in value:
            s = str(item).strip()
            if s:
                out.append(s)
        return out
    s = str(value).strip()
    return [s] if s else []


def collect_contract_event_types(contracts_root: Path) -> set[str]:
    event_types: set[str] = set()
    etype_re = re.compile(r'\bEventType\s*=\s*"([^"]+)"')
    if not contracts_root.exists():
        return event_types
    for p in contracts_root.rglob("*.cs"):
        try:
            text = p.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        for m in etype_re.finditer(text):
            s = m.group(1).strip()
            if s:
                event_types.add(s)
    return event_types


def main() -> int:
    root = Path(__file__).resolve().parents[2]
    contracts_root = root / "Game.Core" / "Contracts"
    known_event_types = collect_contract_event_types(contracts_root)

    views = {
        "tasks_back.json": root / ".taskmaster" / "tasks" / "tasks_back.json",
        "tasks_gameplay.json": root / ".taskmaster" / "tasks" / "tasks_gameplay.json",
    }

    path_like = re.compile(r"(^\.|/|\\|\.py$|\.yml$|\.md$|^logs/|^scripts/|^\.github/)")

    report: dict[str, Any] = {
        "date": today_str(),
        "contracts_eventType_count": len(known_event_types),
        "invalid_contractRefs": [],
        "suspicious_contractRefs": [],
        "missing_artifactRefs": [],
        "notes": [
            "A+exceptions: contractRefs should only reference domain EventType constants (Game.Core/Contracts/**).",
            "artifactRefs is for gate artifacts (scripts/logs/workflows/docs) and must not be mixed into contractRefs.",
        ],
    }

    for view_name, view_path in views.items():
        tasks = json.loads(view_path.read_text(encoding="utf-8"))
        if not isinstance(tasks, list):
            raise ValueError(f"Unsupported view schema: {view_path} (expected list root)")
        for t in tasks:
            if not isinstance(t, dict):
                continue

            if "artifactRefs" not in t:
                report["missing_artifactRefs"].append({"view": view_name, "id": t.get("id")})

            for s in normalize_list(t.get("contractRefs")):
                if known_event_types and s not in known_event_types:
                    report["invalid_contractRefs"].append(
                        {"view": view_name, "id": t.get("id"), "taskmaster_id": t.get("taskmaster_id"), "value": s}
                    )
                if path_like.search(s):
                    report["suspicious_contractRefs"].append(
                        {
                            "view": view_name,
                            "id": t.get("id"),
                            "taskmaster_id": t.get("taskmaster_id"),
                            "value": s,
                            "reason": "looks like a path/artifact, not EventType",
                        }
                    )

    out_dir = root / "logs" / "ci" / today_str() / "task-mapping"
    out_path = out_dir / "contractrefs-audit-after-artifactrefs.json"
    write_json(out_path, report)

    print(f"AUDIT_OK path={out_path}")
    print(f"invalid_contractRefs={len(report['invalid_contractRefs'])}")
    print(f"suspicious_contractRefs={len(report['suspicious_contractRefs'])}")
    print(f"missing_artifactRefs={len(report['missing_artifactRefs'])}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

