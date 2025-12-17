#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Append SaveId value object description into
docs/architecture/overlays/PRD-Guild-Manager/08/08-Contracts-Guild-Manager-Events.md
using UTF-8 encoding.
"""

from __future__ import annotations

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
OVERLAY_FILE = ROOT / "docs" / "architecture" / "overlays" / "PRD-Guild-Manager" / "08" / "08-Contracts-Guild-Manager-Events.md"


BLOCK = """
## SaveId 值对象（GameLoop 关联）

- **SaveIdValue**
  - 用途：作为长生命周期存档槽的逻辑标识，避免直接使用原始字符串参与路径或 SQL 片段拼接。
  - 规则：仅允许 `[a-zA-Z0-9_-]`，长度 1–64，违反规则时抛出异常。
  - 契约位置：`Game.Core/Domain/Turn/SaveIdValue.cs`
  - 关联事件：`core.game_turn.started` / `core.game_turn.phase_changed` / `core.game_turn.week_advanced` 中的 `SaveId` 字段应基于该值对象生成，避免未经验证的输入进入事件与持久化层。
"""


def main() -> None:
    if not OVERLAY_FILE.exists():
        raise SystemExit(f"Overlay file not found: {OVERLAY_FILE}")

    text = OVERLAY_FILE.read_text(encoding="utf-8")
    # Avoid duplicate insertion if script runs multiple times.
    if "SaveId 值对象（GameLoop 关联）" in text:
        print("SaveId section already present; no changes made.")
        return

    new_text = text.rstrip() + "\n" + BLOCK.strip() + "\n"
    OVERLAY_FILE.write_text(new_text, encoding="utf-8")
    print(f"Appended SaveIdValue description to {OVERLAY_FILE}")


if __name__ == "__main__":
    main()

