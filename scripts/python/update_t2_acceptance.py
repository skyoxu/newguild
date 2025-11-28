#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Update acceptance notes for T2-related tasks.

This script appends a short, UTF-8 Chinese acceptance note to
NG-0021 and GM-0103 so that their acceptance blocks explicitly
mention the new T2 test entry points:
  - Game.Core.Tests/Domain/GameLoopTests.cs
  - Tests.Godot/tests/Scenes/Guild/T2PlayableSceneTests.gd

Run once via:
  py -3 scripts/python/update_t2_acceptance.py
"""

from __future__ import annotations

import json
from pathlib import Path


NG_NOTE = (
    "Game.Core.Tests/Domain/GameLoopTests.cs 与 "
    "Tests.Godot/tests/Scenes/Guild/T2PlayableSceneTests.gd 已作为 PRD 3.0.3 "
    "T2 可玩性场景流的最小测试入口，分别覆盖 Core 回合状态与场景可玩闭环，"
    "并挂接在 NG-0021 与 GM-0103 的 Test-Refs 中。"
)


GM_NOTE = (
    "GM-0103 所需的 T2 流程最小测试入口已经落在 "
    "Game.Core.Tests/Domain/GameLoopTests.cs 与 "
    "Tests.Godot/tests/Scenes/Guild/T2PlayableSceneTests.gd；后续扩展测试应在此基础上补充，"
    "而不是新建平行入口文件。"
)


def update_tasks_back(path: Path) -> None:
    data = json.loads(path.read_text(encoding="utf-8"))
    changed = False
    for task in data:
        if task.get("id") == "NG-0021":
            acc = task.setdefault("acceptance", [])
            if NG_NOTE not in acc:
                acc.append(NG_NOTE)
                changed = True
    if changed:
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
        print("[update_t2_acceptance] updated NG-0021 acceptance")
    else:
        print("[update_t2_acceptance] NG-0021 acceptance already contains note")


def update_tasks_gameplay(path: Path) -> None:
    data = json.loads(path.read_text(encoding="utf-8"))
    changed = False
    for task in data:
        if task.get("id") == "GM-0103":
            acc = task.setdefault("acceptance", [])
            if GM_NOTE not in acc:
                acc.append(GM_NOTE)
                changed = True
    if changed:
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
        print("[update_t2_acceptance] updated GM-0103 acceptance")
    else:
        print("[update_t2_acceptance] GM-0103 acceptance already contains note")


def main() -> None:
    tasks_dir = Path(".taskmaster/tasks")
    update_tasks_back(tasks_dir / "tasks_back.json")
    update_tasks_gameplay(tasks_dir / "tasks_gameplay.json")


if __name__ == "__main__":  # pragma: no cover
    main()

