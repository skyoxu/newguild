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

Hard coverage rule (to avoid wiring leaks):
- For tasks at/after `LAYER_MIN_ENFORCE_FROM_TASKMASTER_ID`, minimum `contractRefs` counts are enforced by layer.
  - adapter -> >= 2
  - core -> >= 1

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

LAYER_MIN_CONTRACT_REFS: dict[str, int] = {
    "adapter": 2,
    "core": 1,
}
LAYER_MIN_ENFORCE_FROM_TASKMASTER_ID = 53


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


def _parse_taskmaster_id(value: Any) -> int | None:
    if value is None:
        return None
    if isinstance(value, int):
        return value
    text = str(value).strip()
    if text.isdigit():
        return int(text)
    m = re.match(r"^[Tt](\d+)$", text)
    if m:
        return int(m.group(1))
    return None


def _required_min_contract_refs(task: dict[str, Any], strict_contracts: bool) -> int:
    if not strict_contracts:
        return 0
    tm_id = _parse_taskmaster_id(task.get("taskmaster_id"))
    if tm_id is None or tm_id < LAYER_MIN_ENFORCE_FROM_TASKMASTER_ID:
        return 0
    layer = str(task.get("layer") or "").strip().lower()
    return LAYER_MIN_CONTRACT_REFS.get(layer, 0)


def run_validation(root: Path, out_path: Path | None = None) -> tuple[bool, dict[str, Any]]:
    out_dir = root / "logs" / "ci" / today_str() / "task-mapping"
    if out_path is None:
        out_path = out_dir / "validate-view-ref-semantics.json"
    out_path.parent.mkdir(parents=True, exist_ok=True)

    known_event_types = collect_contract_event_types(root / "Game.Core" / "Contracts")

    report: dict[str, Any] = {
        "date": today_str(),
        "errors": [],
        "warnings": [],
        "rules": {
            "test_refs": "MUST exist; MUST NOT include logs/** paths",
            "artifactRefs": "Gate artifacts; MAY include placeholders; existence NOT enforced",
            "contractRefs": "EventType constants; strict for in-progress/done, warning for pending/deferred/cancelled",
            "contractRefs_min_coverage": (
                f"For taskmaster_id >= {LAYER_MIN_ENFORCE_FROM_TASKMASTER_ID}: "
                "adapter>=2, core>=1 (strict for in-progress/done)"
            ),
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

            contract_refs = normalize_list(t.get("contractRefs"))

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
            for c in contract_refs:
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

            required_min = _required_min_contract_refs(t, strict_contracts)
            if required_min > 0 and len(contract_refs) < required_min:
                report["errors"].append(
                    {
                        "file": rel,
                        "id": tid,
                        "taskmaster_id": tm_id,
                        "layer": t.get("layer"),
                        "status": status_raw or None,
                        "error": (
                            f"contractRefs minimum coverage not met: "
                            f"required>={required_min}, actual={len(contract_refs)}"
                        ),
                    }
                )

    write_json(out_path, report)
    ok = len(report["errors"]) == 0
    return ok, report


def main() -> int:
    root = Path(__file__).resolve().parents[2]
    ok, report = run_validation(root)
    out_dir = root / "logs" / "ci" / today_str() / "task-mapping"
    print(
        f"VIEW_REF_SEMANTICS status={'ok' if ok else 'fail'} "
        f"errors={len(report.get('errors', []))} warnings={len(report.get('warnings', []))} out={out_dir}"
    )
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
