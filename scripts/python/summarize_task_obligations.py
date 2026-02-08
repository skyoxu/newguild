from __future__ import annotations

import json
import os
from datetime import datetime
from pathlib import Path
from typing import Any


def today_str() -> str:
    return os.environ.get("CI_DATE") or datetime.now().strftime("%Y-%m-%d")


def read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")


def _normalize_list(value: Any) -> list[str]:
    if value is None:
        return []
    if isinstance(value, list):
        return [str(x) for x in value if str(x).strip()]
    if isinstance(value, str):
        return [value] if value.strip() else []
    return [str(value)]


def main() -> int:
    repo_root = Path(__file__).resolve().parents[2]
    base_dir = repo_root / "logs" / "ci" / today_str() / "acceptance-obligations"
    inp = base_dir / "obligations.json"
    if not inp.exists():
        raise SystemExit(f"Missing obligations.json: {inp}")

    data = read_json(inp)
    tasks = data.get("tasks") or []
    gaps_list = data.get("gaps") or []
    gaps_by_task_id: dict[str, list[str]] = {}
    for entry in gaps_list:
        if not isinstance(entry, dict):
            continue
        task_id = entry.get("task_id")
        if task_id is None:
            continue
        key = str(task_id)
        gaps_by_task_id[key] = _normalize_list(entry.get("gaps"))

    lines: list[str] = []
    lines.append("# Acceptance Obligations Summary (T44..T47)")
    lines.append("")
    lines.append("Source: `logs/ci/<date>/acceptance-obligations/obligations.json`")
    lines.append("")

    for t in tasks:
        task_id = t.get("task_id")
        title = t.get("title") or ""
        layer = t.get("layer") or ""
        view = t.get("view_id") or ""
        gaps = gaps_by_task_id.get(str(task_id), [])
        obligations = t.get("derived_obligations") or {}
        contract_refs = _normalize_list(t.get("view_contractRefs"))
        test_refs = _normalize_list(t.get("view_test_refs"))
        artifact_refs = _normalize_list(t.get("view_artifactRefs"))
        view_acceptance = _normalize_list(t.get("view_acceptance"))

        lines.append(f"## T{task_id} ({view})")
        lines.append(f"- Title: {title}")
        lines.append(f"- Layer: {layer}")
        if gaps:
            lines.append("- Acceptance gaps:")
            for g in gaps:
                lines.append(f"  - {g}")
        else:
            lines.append("- Acceptance gaps: (none)")

        if contract_refs:
            lines.append(f"- contractRefs: {', '.join(contract_refs)}")
        if test_refs:
            lines.append(f"- test_refs: {', '.join(test_refs)}")
        if artifact_refs:
            lines.append(f"- artifactRefs: {', '.join(artifact_refs)}")
        if view_acceptance:
            lines.append(f"- acceptance_count: {len(view_acceptance)}")

        def dump_bucket(name: str) -> None:
            bucket = obligations.get(name) or []
            if not bucket:
                return
            lines.append(f"### Obligations/{name}")
            for item in bucket:
                if isinstance(item, dict):
                    must = item.get("must") or item.get("Must") or ""
                    ev = item.get("evidence") or item.get("Evidence") or ""
                    if must:
                        lines.append(f"- Must: {must}")
                    if ev:
                        lines.append(f"  Evidence: {ev}")
                else:
                    lines.append(f"- {item}")

        for bucket_name in ["Functional", "Contracts", "Tests", "Observability", "Wiring", "Gates"]:
            dump_bucket(bucket_name)

        lines.append("")

    out = base_dir / "obligations.summary.md"
    write_text(out, "\n".join(lines))
    print(f"Wrote {out.as_posix()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
