#!/usr/bin/env python3
"""
Validate content assets (JSON) and write CI artifacts under logs/ci/<date>/.

Hard rules (stdlib only):
- All JSON files under `Game.Godot/Assets/Data/**` must be parseable UTF-8 JSON (skip non-json).
- `event_definition_minimal.json` must contain a minimal set of keys and obey ID/version rules.
- `balance_params.default.json` must contain a minimal registry schema.

Outputs:
- logs/ci/<YYYY-MM-DD>/content-validation/report.json
- logs/ci/<YYYY-MM-DD>/content-validation/report.txt
"""

from __future__ import annotations

import datetime as dt
import json
import os
import re
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[2]
DATA_ROOT = REPO_ROOT / "Game.Godot" / "Assets" / "Data"


CONTENT_ID_RE = re.compile(r"^content\.[a-z0-9_]+\.[a-z0-9_]+\.[a-z0-9_]+$")


def _today_ci_dir() -> Path:
    out_dir = REPO_ROOT / "logs" / "ci" / dt.date.today().isoformat() / "content-validation"
    out_dir.mkdir(parents=True, exist_ok=True)
    return out_dir


def _read_json(path: Path) -> tuple[dict[str, Any] | list[Any] | None, str | None]:
    try:
        text = path.read_text(encoding="utf-8")
    except UnicodeDecodeError as exc:
        return None, f"non_utf8: {exc}"
    except OSError as exc:
        return None, f"io_error: {exc}"
    try:
        return json.loads(text), None
    except Exception as exc:
        return None, f"json_parse_error: {exc}"


def _validate_event_template(obj: Any) -> list[str]:
    issues: list[str] = []
    if not isinstance(obj, dict):
        return ["root_not_object"]

    required = ["id", "version", "type", "title", "weight", "conditions", "effects"]
    for k in required:
        if k not in obj:
            issues.append(f"missing:{k}")

    cid = obj.get("id")
    if not isinstance(cid, str) or not cid.strip():
        issues.append("id_invalid")
    else:
        if not CONTENT_ID_RE.match(cid.strip()):
            issues.append("id_format_invalid_expected_content_module_kind_name")

    ver = obj.get("version")
    if not isinstance(ver, int) or ver < 1:
        issues.append("version_invalid")

    w = obj.get("weight")
    if not isinstance(w, (int, float)):
        issues.append("weight_invalid")

    for k in ("conditions", "effects"):
        v = obj.get(k)
        if not isinstance(v, list):
            issues.append(f"{k}_not_list")

    return issues


def _validate_balance_registry(obj: Any) -> list[str]:
    issues: list[str] = []
    if not isinstance(obj, dict):
        return ["root_not_object"]

    sv = obj.get("schemaVersion")
    if not isinstance(sv, int) or sv < 1:
        issues.append("schemaVersion_invalid")

    params = obj.get("params")
    if not isinstance(params, list) or not params:
        issues.append("params_missing_or_empty")
        return issues

    required = {"key", "version", "value", "unit", "min", "max", "scope", "description"}
    for i, p in enumerate(params):
        if not isinstance(p, dict):
            issues.append(f"params[{i}]_not_object")
            continue
        missing = [k for k in sorted(required) if k not in p]
        for k in missing:
            issues.append(f"params[{i}].missing:{k}")
        if "key" in p and (not isinstance(p["key"], str) or not p["key"].strip()):
            issues.append(f"params[{i}].key_invalid")
        if "version" in p and (not isinstance(p["version"], int) or p["version"] < 1):
            issues.append(f"params[{i}].version_invalid")
    return issues


def main() -> int:
    out_dir = _today_ci_dir()

    report: dict[str, Any] = {
        "ts": dt.datetime.now().replace(microsecond=0).isoformat(),
        "data_root": str(DATA_ROOT),
        "files": [],
        "status": "ok",
        "errors": 0,
    }

    if not DATA_ROOT.exists():
        report["status"] = "fail"
        report["errors"] += 1
        report["files"].append({"path": str(DATA_ROOT), "error": "missing_data_root"})
    else:
        json_files = sorted([p for p in DATA_ROOT.rglob("*.json") if p.is_file()])
        for p in json_files:
            obj, err = _read_json(p)
            rel = str(p.relative_to(REPO_ROOT)).replace("\\", "/")
            entry: dict[str, Any] = {"path": rel, "status": "ok", "issues": []}
            if err is not None:
                entry["status"] = "fail"
                entry["issues"].append(err)
                report["errors"] += 1
                report["files"].append(entry)
                continue

            # Targeted validations for known minimum templates
            if rel.endswith("Game.Godot/Assets/Data/Templates/event_definition_minimal.json"):
                issues = _validate_event_template(obj)
                if issues:
                    entry["status"] = "fail"
                    entry["issues"].extend(issues)
                    report["errors"] += 1
            if rel.endswith("Game.Godot/Assets/Data/Balance/balance_params.default.json"):
                issues = _validate_balance_registry(obj)
                if issues:
                    entry["status"] = "fail"
                    entry["issues"].extend(issues)
                    report["errors"] += 1

            report["files"].append(entry)

    if report["errors"] > 0:
        report["status"] = "fail"

    out_json = out_dir / "report.json"
    out_txt = out_dir / "report.txt"
    out_json.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    lines = [
        f"ts={report['ts']}",
        f"status={report['status']}",
        f"errors={report['errors']}",
        f"data_root={report['data_root']}",
        "",
    ]
    for f in report["files"]:
        if f.get("status") == "fail":
            lines.append(f"FAIL {f['path']}: {', '.join(f.get('issues', []))}")
    out_txt.write_text("\n".join(lines) + "\n", encoding="utf-8")

    print(f"[REPORT] {out_json}")
    print(f"[REPORT] {out_txt}")
    print(f"[{ 'OK' if report['status']=='ok' else 'FAIL' }] content_validation errors={report['errors']}")
    return 0 if report["status"] == "ok" else 1


if __name__ == "__main__":
    raise SystemExit(main())
