#!/usr/bin/env python3
"""
Generate C# and GDScript ui.menu event constants from JSON SSoT.

Source of truth:
  Game.Core/Contracts/UI/ui_menu_event_types.json

Outputs:
  Game.Core/Contracts/UI/UiMenuEventTypes.cs
  Game.Godot/Scripts/UI/UiMenuEventTypes.gd
"""
from __future__ import annotations

import io
import json
from pathlib import Path
from typing import Dict, List


ROOT = Path(__file__).resolve().parents[2]
JSON_PATH = ROOT / "Game.Core/Contracts/UI/ui_menu_event_types.json"
CS_PATH = ROOT / "Game.Core/Contracts/UI/UiMenuEventTypes.cs"
GD_PATH = ROOT / "Game.Godot/Scripts/UI/UiMenuEventTypes.gd"


def read_json(path: Path) -> Dict[str, object]:
    with io.open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def build_cs(prefix: str, events: Dict[str, str]) -> str:
    lines: List[str] = [
        "namespace Game.Core.Contracts.UI;",
        "",
        "/// <summary>",
        "/// UI menu event types for main navigation.",
        "/// </summary>",
        "/// <remarks>",
        "/// ADR-0004: event type naming rules.",
        "/// </remarks>",
        "public static class UiMenuEventTypes",
        "{",
    ]
    for name, value in events.items():
        lines.append(f"    public const string {name} = \"{value}\";")
    lines.append("}")
    return "\n".join(lines) + "\n"


def build_gd(prefix: str, events: Dict[str, str]) -> str:
    lines: List[str] = [f'const PREFIX := "{prefix}"']
    for name, value in events.items():
        lines.append(f'const {name.upper()} := "{value}"')
    return "\n".join(lines) + "\n"


def main() -> int:
    data = read_json(JSON_PATH)
    prefix = data.get("prefix")
    events = data.get("events") or {}
    if not isinstance(prefix, str) or not prefix:
        raise ValueError("prefix missing or invalid in JSON source")
    if not isinstance(events, dict) or not events:
        raise ValueError("events missing or invalid in JSON source")

    cs_text = build_cs(prefix, events)
    gd_text = build_gd(prefix, events)

    CS_PATH.write_text(cs_text, encoding="utf-8")
    GD_PATH.write_text(gd_text, encoding="utf-8")
    print("Generated:", CS_PATH)
    print("Generated:", GD_PATH)
    return 0


if __name__ == "__main__":  # pragma: no cover
    raise SystemExit(main())
