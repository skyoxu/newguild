#!/usr/bin/env python3
"""
Validate ui.menu event types stay in sync across C# and GDScript.

Single source of truth:
  Game.Core/Contracts/UI/ui_menu_event_types.json

Outputs a report under logs/ci/<YYYY-MM-DD>/ui-menu-event-types/report.json.
Exit code:
  0 = ok
  1 = mismatch
"""
from __future__ import annotations

import datetime as dt
import io
import json
import re
from pathlib import Path
from typing import Dict, List, Tuple


ROOT = Path(__file__).resolve().parents[2]
JSON_PATH = ROOT / "Game.Core/Contracts/UI/ui_menu_event_types.json"
CS_PATH = ROOT / "Game.Core/Contracts/UI/UiMenuEventTypes.cs"
GD_PATH = ROOT / "Game.Godot/Scripts/UI/UiMenuEventTypes.gd"

CS_CONST_RE = re.compile(r'public\s+const\s+string\s+(?P<name>\w+)\s*=\s*"(?P<value>[^"]+)"\s*;')
GD_CONST_RE = re.compile(r'^\s*const\s+(?P<name>[A-Z0-9_]+)\s*:=\s*"(?P<value>[^"]+)"\s*$')


def read_json(path: Path) -> Dict[str, object]:
    with io.open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def parse_cs_constants(text: str) -> Dict[str, str]:
    values: Dict[str, str] = {}
    for m in CS_CONST_RE.finditer(text):
        values[m.group("name")] = m.group("value")
    return values


def parse_gd_constants(text: str) -> Dict[str, str]:
    values: Dict[str, str] = {}
    for line in text.splitlines():
        m = GD_CONST_RE.match(line)
        if not m:
            continue
        values[m.group("name")] = m.group("value")
    return values


def diff_sets(source: List[str], target: List[str]) -> Tuple[List[str], List[str]]:
    src = set(source)
    tgt = set(target)
    missing = sorted(src - tgt)
    extra = sorted(tgt - src)
    return missing, extra


def main() -> int:
    errors: List[str] = []
    report: Dict[str, object] = {
        "ok": False,
        "json_path": str(JSON_PATH),
        "cs_path": str(CS_PATH),
        "gd_path": str(GD_PATH),
        "errors": errors,
    }

    if not JSON_PATH.exists():
        errors.append("JSON source missing")
        return write_report(report, ok=False)

    data = read_json(JSON_PATH)
    prefix = data.get("prefix")
    events = data.get("events") or {}
    if not isinstance(prefix, str) or not prefix:
        errors.append("prefix missing or invalid in JSON source")
        return write_report(report, ok=False)

    if not isinstance(events, dict) or not events:
        errors.append("events missing or invalid in JSON source")
        return write_report(report, ok=False)

    event_values = list(events.values())
    invalid = [v for v in event_values if not isinstance(v, str) or not v.startswith(prefix)]
    if invalid:
        errors.append("events contain invalid values or wrong prefix")

    cs_values = parse_cs_constants(CS_PATH.read_text(encoding="utf-8")) if CS_PATH.exists() else {}
    gd_values = parse_gd_constants(GD_PATH.read_text(encoding="utf-8")) if GD_PATH.exists() else {}

    report["prefix"] = prefix
    report["events"] = events
    report["cs_values"] = cs_values
    report["gd_values"] = gd_values

    if not cs_values:
        errors.append("C# constants not found")
    if not gd_values:
        errors.append("GDScript constants not found")

    gd_prefix = gd_values.get("PREFIX")
    if gd_prefix != prefix:
        errors.append("GDScript PREFIX does not match JSON prefix")

    missing_cs, extra_cs = diff_sets(event_values, list(cs_values.values()))
    missing_gd, extra_gd = diff_sets(event_values, [v for k, v in gd_values.items() if k != "PREFIX"])

    if missing_cs or extra_cs:
        errors.append("C# constants mismatch")
    if missing_gd or extra_gd:
        errors.append("GDScript constants mismatch")

    report["cs_diff"] = {"missing": missing_cs, "extra": extra_cs}
    report["gd_diff"] = {"missing": missing_gd, "extra": extra_gd}

    ok = len(errors) == 0
    return write_report(report, ok=ok)


def write_report(report: Dict[str, object], ok: bool) -> int:
    report["ok"] = ok
    date = dt.date.today().strftime("%Y-%m-%d")
    out_dir = ROOT / "logs" / "ci" / date / "ui-menu-event-types"
    out_dir.mkdir(parents=True, exist_ok=True)
    out_path = out_dir / "report.json"
    with io.open(out_path, "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=2)
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0 if ok else 1


if __name__ == "__main__":  # pragma: no cover
    raise SystemExit(main())
