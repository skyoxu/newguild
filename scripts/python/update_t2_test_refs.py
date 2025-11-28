#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Update T2-related test_refs in Taskmaster and overlay docs.

This script:
- Ensures NG-0021 and GM-0103 reference the new T2 test files:
  * Game.Core.Tests/Domain/GameLoopTests.cs
  * Tests.Godot/tests/Scenes/Guild/T2PlayableSceneTests.gd
- Updates Overlay 08 Test-Refs block to include the same paths.

All file I/O uses UTF-8.
"""

from __future__ import annotations

import json
from pathlib import Path


GAME_LOOP_TEST = "Game.Core.Tests/Domain/GameLoopTests.cs"
T2_SCENE_TEST = "Tests.Godot/tests/Scenes/Guild/T2PlayableSceneTests.gd"


def _ensure_in_list(items: list[str], value: str) -> None:
    if value not in items:
        items.append(value)


def update_tasks_back(path: Path) -> None:
    text = path.read_text(encoding="utf-8")
    data = json.loads(text)

    changed = False
    for task in data:
        if task.get("id") == "NG-0021":
            tests = task.setdefault("test_refs", [])
            before = set(tests)
            _ensure_in_list(tests, GAME_LOOP_TEST)
            _ensure_in_list(tests, T2_SCENE_TEST)
            if set(tests) != before:
                changed = True

    if changed:
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
        print("[update_t2_test_refs] updated NG-0021 test_refs in tasks_back.json")
    else:
        print("[update_t2_test_refs] NG-0021 already up to date in tasks_back.json")


def update_tasks_gameplay(path: Path) -> None:
    text = path.read_text(encoding="utf-8")
    data = json.loads(text)

    changed = False
    for task in data:
        if task.get("id") == "GM-0103":
            tests = task.setdefault("test_refs", [])
            before = set(tests)
            _ensure_in_list(tests, GAME_LOOP_TEST)
            _ensure_in_list(tests, T2_SCENE_TEST)
            if set(tests) != before:
                changed = True

    if changed:
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
        print("[update_t2_test_refs] updated GM-0103 test_refs in tasks_gameplay.json")
    else:
        print("[update_t2_test_refs] GM-0103 already up to date in tasks_gameplay.json")


def update_overlay_08(path: Path) -> None:
    text = path.read_text(encoding="utf-8")
    updated = text

    # 插入 GameLoopTests 到 Core xUnit 区块
    core_block_old = (
        "  # Core 回合与事件引擎（xUnit）\n"
        "  - Game.Core.Tests/Domain/GameTurnSystemTests.cs\n"
        "  - Game.Core.Tests/Domain/EventEngineTests.cs\n"
    )
    core_block_new = (
        "  # Core 回合与事件引擎（xUnit）\n"
        "  - Game.Core.Tests/Domain/GameTurnSystemTests.cs\n"
        "  - Game.Core.Tests/Domain/EventEngineTests.cs\n"
        f"  - {GAME_LOOP_TEST}\n"
    )
    if GAME_LOOP_TEST not in text and core_block_old in text:
        updated = updated.replace(core_block_old, core_block_new)

    # 插入 T2 场景测试到 GdUnit4 区块
    scene_block_old = (
        "  # 场景与公会 UI（GdUnit4）\n"
        "  - Tests.Godot/tests/Scenes/test_main_scene_smoke.gd\n"
        "  - Tests.Godot/tests/Scenes/test_guild_main_scene.gd\n"
        "  - Tests.Godot/tests/Integration/test_guild_workflow.gd\n"
    )
    scene_block_new = (
        "  # 场景与公会 UI（GdUnit4）\n"
        "  - Tests.Godot/tests/Scenes/test_main_scene_smoke.gd\n"
        "  - Tests.Godot/tests/Scenes/test_guild_main_scene.gd\n"
        f"  - {T2_SCENE_TEST}\n"
        "  - Tests.Godot/tests/Integration/test_guild_workflow.gd\n"
    )
    if T2_SCENE_TEST not in updated and scene_block_old in updated:
        updated = updated.replace(scene_block_old, scene_block_new)

    if updated != text:
        path.write_text(updated, encoding="utf-8")
        print("[update_t2_test_refs] updated overlay 08 Test-Refs")
    else:
        print("[update_t2_test_refs] overlay 08 already up to date or pattern not found")


def main() -> None:
    tasks_dir = Path(".taskmaster/tasks")
    update_tasks_back(tasks_dir / "tasks_back.json")
    update_tasks_gameplay(tasks_dir / "tasks_gameplay.json")

    overlay_path = Path("docs/architecture/overlays/PRD-Guild-Manager/08/08-功能纵切-公会管理器.md")
    if overlay_path.exists():
        update_overlay_08(overlay_path)
    else:
        print("[update_t2_test_refs] overlay 08 file not found, skip")


if __name__ == "__main__":  # pragma: no cover
    main()

