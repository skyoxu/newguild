#!/usr/bin/env python3
"""
Validate view task ref semantics (deterministic, Windows).

Semantics (repo SSoT):
- `test_refs`: MUST-EXIST references (tests/source files). No `logs/**` paths.
- `artifactRefs`: Gate artifacts / outputs / anchors. MAY use placeholders like `<date>` / `<YYYY-MM-DD>`.
- `contractRefs`: Domain EventType constants only.
  - For tasks in progress or done: must exist in Game.Core/Contracts/**.
  - For tasks still pending/deferred/cancelled: missing EventType is allowed as a planning hint (WARNING),
    so CI doesn't get blocked by future contracts.

This script intentionally does NOT enforce existence for `artifactRefs`.

Outputs:
- logs/ci/<YYYY-MM-DD>/task-mapping/validate-view-ref-semantics.json
"""

from __future__ import annotations

import datetime as dt
import json
import re
from pathlib import Path
from typing import Any


VIEW_FILES = [
    ".taskmaster/tasks/tasks_back.json",
    ".taskmaster/tasks/tasks_gameplay.json",
]


def today_str() -> str:
    return dt.date.today().strftime("%Y-%m-%d")


def write_json(path: Path, obj: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")


def load_view_tasks(path: Path) -> list[dict[str, Any]]:
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, list):
        raise ValueError(f"Unsupported view schema: {path} (expected list root)")
    out: list[dict[str, Any]] = []
    for i, t in enumerate(data):
        if not isinstance(t, dict):
            raise ValueError(f"Invalid task item: {path}[{i}] is {type(t)}")
        out.append(t)
    return out


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
    etype_re = re.compile(r'\bEventType\s*=\s*"([^"]+)"')
    event_types: set[str] = set()
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


def looks_like_log_path(s: str) -> bool:
    v = s.strip().replace("\\", "/")
    return v.startswith("logs/")


def looks_like_event_type(s: str) -> bool:
    # Conservative: treat core/ui/screen prefixes as event type candidates.
    v = s.strip()
    return v.startswith("core.") or v.startswith("ui.") or v.startswith("screen.")


def main() -> int:
    root = Path(__file__).resolve().parents[2]
    out_dir = root / "logs" / "ci" / today_str() / "task-mapping"
    out_dir.mkdir(parents=True, exist_ok=True)

    known_event_types = collect_contract_event_types(root / "Game.Core" / "Contracts")

    report: dict[str, Any] = {
        "date": today_str(),
        "errors": [],
        "warnings": [],
        "rules": {
            "test_refs": "MUST exist; MUST NOT include logs/** paths",
            "artifactRefs": "Gate artifacts; MAY include placeholders; existence NOT enforced",
            "contractRefs": "EventType constants; strict for in-progress/done, warning for pending/deferred/cancelled",
        },
        "contracts_eventType_count": len(known_event_types),
    }

    non_strict_statuses = {"pending", "deferred", "cancelled"}

    for rel in VIEW_FILES:
        path = root / rel
        tasks = load_view_tasks(path)
        for t in tasks:
            tid = t.get("id")
            tm_id = t.get("taskmaster_id")
            status_raw = str(t.get("status") or "").strip().lower()
            strict_contracts = status_raw not in non_strict_statuses

            # artifactRefs: must exist as a list field (schema consistency), but items may be placeholders.
            if "artifactRefs" not in t:
                report["errors"].append({"file": rel, "id": tid, "taskmaster_id": tm_id, "error": "missing artifactRefs field"})
            else:
                if not isinstance(t.get("artifactRefs"), list):
                    report["errors"].append(
                        {"file": rel, "id": tid, "taskmaster_id": tm_id, "error": "artifactRefs must be a list"}
                    )
                for a in normalize_list(t.get("artifactRefs")):
                    if looks_like_event_type(a):
                        report["errors"].append(
                            {
                                "file": rel,
                                "id": tid,
                                "taskmaster_id": tm_id,
                                "error": "artifactRefs contains an EventType-looking string; move it to contractRefs",
                                "value": a,
                            }
                        )

            # test_refs: must not contain logs/**
            for r in normalize_list(t.get("test_refs")):
                if looks_like_log_path(r):
                    report["errors"].append(
                        {
                            "file": rel,
                            "id": tid,
                            "taskmaster_id": tm_id,
                            "error": "test_refs contains logs/**; move it to artifactRefs",
                            "value": r,
                        }
                    )

            # contractRefs: must exist in contracts
            for c in normalize_list(t.get("contractRefs")):
                if known_event_types and c not in known_event_types:
                    entry = {
                        "file": rel,
                        "id": tid,
                        "taskmaster_id": tm_id,
                        "status": status_raw or None,
                        "value": c,
                    }
                    if strict_contracts:
                        entry["error"] = "contractRefs references unknown EventType (missing in Game.Core/Contracts/**)"
                        report["errors"].append(entry)
                    else:
                        entry["warning"] = "contractRefs references unknown EventType (allowed for planning when task is pending/deferred/cancelled)"
                        report["warnings"].append(entry)

    write_json(out_dir / "validate-view-ref-semantics.json", report)
    ok = len(report["errors"]) == 0
    print(f"VIEW_REF_SEMANTICS status={'ok' if ok else 'fail'} out={out_dir}")
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
