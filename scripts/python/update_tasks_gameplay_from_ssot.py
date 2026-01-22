from __future__ import annotations

import datetime as dt
import json
from pathlib import Path


def _load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def _write_json(path: Path, data) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _taskmaster_story_id_from_details(details: str) -> str | None:
    for line in (details or "").splitlines():
        if line.startswith("Story:"):
            return line.split("Story:", 1)[1].strip()
    return None


def _prio_to_p(prio: str) -> str:
    p = (prio or "").strip().lower()
    if p == "high":
        return "P1"
    if p == "medium":
        return "P2"
    if p == "low":
        return "P3"
    return "P2"


def _overlay_refs_min(overlay: str) -> list[str]:
    base = "docs/architecture/overlays/PRD-Guild-Manager/08"
    refs = [
        f"{base}/_index.md",
        f"{base}/ACCEPTANCE_CHECKLIST.md",
    ]
    if overlay and overlay not in refs:
        refs.insert(1, overlay)
    return refs


def _labels_for(taskmaster_id: int) -> list[str]:
    phase = ["phase2"]
    if taskmaster_id in (27, 41):
        return phase + ["content", "assets", "json"]
    if taskmaster_id in (28, 29, 33):
        return phase + ["events", "wiring"]
    if taskmaster_id in (30, 31, 32, 39):
        return phase + ["ui"]
    if taskmaster_id in (34,):
        return phase + ["tactical", "pve"]
    if taskmaster_id in (35, 36, 37):
        return phase + ["progression", "rewards"]
    if taskmaster_id in (38,):
        return phase + ["officers", "guild"]
    if taskmaster_id in (40,):
        return phase + ["worldgen", "seed"]
    return phase


def _layer_for(taskmaster_id: int) -> str:
    # "core" or "adapter" in this view.
    if taskmaster_id in (28, 29, 35, 36, 37, 38, 40, 41):
        return "core"
    return "adapter"


def _contract_refs_for(taskmaster_id: int) -> list[str]:
    if taskmaster_id == 29:
        return ["core.game_turn.week_advanced", "core.guild.created", "core.raid.resolved"]
    if taskmaster_id == 33:
        return ["core.raid.resolved", "core.reputation.changed", "core.social.relationship.changed"]
    if taskmaster_id == 34:
        return ["core.raid.scheduled", "core.raid.resolved"]
    if taskmaster_id == 35:
        return ["core.raid.resolved", "core.media.beat.triggered", "core.reputation.changed"]
    if taskmaster_id == 40:
        return []
    return []


def _acceptance_for(taskmaster_id: int) -> list[str]:
    # Keep acceptance actionable and aligned to repo conventions. Use file refs placeholders where needed.
    if taskmaster_id == 27:
        return [
            "提供一个统一的内容清单加载入口（从 res://Game.Godot/Assets/Data/content/base/manifest.json），能解析并返回可用的内容条目列表；失败应给出可定位错误。 Refs: `Tests.Godot/tests/Content/test_content_manifest_load.gd`",
            "内容解析/校验逻辑在纯 C# 层可单测（不依赖 Godot API）。 Refs: `Game.Core.Tests/Content/ContentManifestTests.cs`",
        ]
    if taskmaster_id == 28:
        return [
            "为 EventDefinition/EventChain 增加强类型契约，并满足 ADR-0004 的事件命名与常量化要求。 Refs: `Game.Core.Tests/Contracts/EventDefinitionContractsTests.cs`",
        ]
    if taskmaster_id == 29:
        return [
            "EventEngine 使用 EventCatalog 进行内容驱动（不再依赖 HUD 内的 EmptyEventCatalog 兜底），并能在固定时间/固定种子下产出确定性事件。 Refs: `Game.Core.Tests/Engine/EventEngineContentDrivenTests.cs`",
            "Headless wiring-smoke 能观察到至少一个内容驱动事件并验证 UI/状态同步。 Refs: `Game.Godot/Scripts/CI/WiringSmoke.gd`",
        ]
    if taskmaster_id == 30:
        return [
            "关键界面（HUD/Guild/Settings）在默认窗口尺寸内可完整操作（滚动/布局/点击不被遮挡）。 Refs: `Tests.Godot/tests/Scenes/test_ui_clickability_smoke.gd`",
        ]
    if taskmaster_id == 31:
        return [
            "新增可复用 UI 组件（状态/错误/列表等），用于多个面板复用而非复制粘贴。 Refs: `Tests.Godot/tests/UI/test_ui_components_smoke.gd`",
        ]
    if taskmaster_id == 32:
        return [
            "异步交互具备统一状态（loading/disabled/error/retry），并且错误信息遵循 ADR-0019 脱敏策略。 Refs: `Tests.Godot/tests/UI/test_ui_interaction_states.gd`",
        ]
    if taskmaster_id == 33:
        return [
            "提供活动反馈面板，能把关键事件按时间线展示（至少覆盖 raid/media/social）。 Refs: `Tests.Godot/tests/UI/test_activity_feed_smoke.gd`",
        ]
    if taskmaster_id == 34:
        return [
            "提供战术中心入口：最小编成/校验/触发 raid，并能观察到 core.raid.resolved。 Refs: `Tests.Godot/tests/Scenes/test_tactical_center_smoke.gd`",
        ]
    if taskmaster_id == 35:
        return [
            "奖励发放由统一 Reward Ledger 处理（raid/media 等结果驱动），并可 Save/Load 回放。 Refs: `Game.Core.Tests/Progression/RewardLedgerTests.cs`",
        ]
    if taskmaster_id == 36:
        return [
            "成就定义与追踪由 DomainEvent 驱动，至少实现 1 个确定性成就并可在 UI 中查看。 Refs: `Game.Core.Tests/Progression/AchievementTrackerTests.cs` `Tests.Godot/tests/UI/test_achievements_smoke.gd`",
        ]
    if taskmaster_id == 37:
        return [
            "经验/等级曲线为确定性规则，且与奖励账本对齐并可持久化。 Refs: `Game.Core.Tests/Progression/ExperienceSystemTests.cs`",
        ]
    if taskmaster_id == 38:
        return [
            "官员槽位与任命规则可用且可持久化（随 Save/Load）。 Refs: `Game.Core.Tests/Guild/OfficerAssignmentTests.cs`",
        ]
    if taskmaster_id == 39:
        return [
            "提供官员 UI 入口，能任命/撤销并显示状态；交互模式复用统一组件。 Refs: `Tests.Godot/tests/UI/test_officer_ui_smoke.gd`",
        ]
    if taskmaster_id == 40:
        return [
            "世界生成端口收口开局初始化：给定固定 seed 可生成确定性的 NPC 公会集合，并与 Save/Load 对齐。 Refs: `Game.Core.Tests/World/WorldGenerationPortTests.cs`",
        ]
    if taskmaster_id == 41:
        return [
            "NPC 公会原型从内容 json 数据驱动加载，并能被世界生成端口消费。 Refs: `Game.Core.Tests/World/NpcGuildArchetypeTests.cs`",
        ]
    return []


def _test_refs_for(taskmaster_id: int) -> list[str]:
    # Provide suggested test files (may be created in implementation phase).
    mapping = {
        27: ["Game.Core.Tests/Content/ContentManifestTests.cs", "Tests.Godot/tests/Content/test_content_manifest_load.gd"],
        28: ["Game.Core.Tests/Contracts/EventDefinitionContractsTests.cs"],
        29: ["Game.Core.Tests/Engine/EventEngineContentDrivenTests.cs", "Game.Godot/Scripts/CI/WiringSmoke.gd"],
        30: ["Tests.Godot/tests/Scenes/test_ui_clickability_smoke.gd"],
        31: ["Tests.Godot/tests/UI/test_ui_components_smoke.gd"],
        32: ["Tests.Godot/tests/UI/test_ui_interaction_states.gd"],
        33: ["Tests.Godot/tests/UI/test_activity_feed_smoke.gd"],
        34: ["Tests.Godot/tests/Scenes/test_tactical_center_smoke.gd"],
        35: ["Game.Core.Tests/Progression/RewardLedgerTests.cs"],
        36: ["Game.Core.Tests/Progression/AchievementTrackerTests.cs", "Tests.Godot/tests/UI/test_achievements_smoke.gd"],
        37: ["Game.Core.Tests/Progression/ExperienceSystemTests.cs"],
        38: ["Game.Core.Tests/Guild/OfficerAssignmentTests.cs"],
        39: ["Tests.Godot/tests/UI/test_officer_ui_smoke.gd"],
        40: ["Game.Core.Tests/World/WorldGenerationPortTests.cs"],
        41: ["Game.Core.Tests/World/NpcGuildArchetypeTests.cs"],
    }
    return mapping.get(taskmaster_id, [])


def _test_strategy_for(taskmaster_id: int) -> list[str]:
    base = [
        "坚持 TDD：red -> green -> refactor；Core 不依赖 Godot API。",
        "测试与取证产物统一写入 logs/**（遵循仓库 SSoT）。",
    ]
    if taskmaster_id in (30, 31, 32, 33, 34, 39):
        return base + ["为 UI/场景行为添加 GdUnit4 headless 用例，避免仅字符串断言。"]
    if taskmaster_id in (27, 28, 29, 35, 36, 37, 38, 40, 41):
        return base + ["领域逻辑用 xUnit 覆盖关键边界与确定性规则。"]
    return base


def main() -> int:
    repo_root = Path(__file__).resolve().parents[2]
    ssot_path = repo_root / ".taskmaster" / "tasks" / "tasks.json"
    gameplay_path = repo_root / ".taskmaster" / "tasks" / "tasks_gameplay.json"
    back_path = repo_root / ".taskmaster" / "tasks" / "tasks_back.json"

    ssot = _load_json(ssot_path)
    gameplay = _load_json(gameplay_path)
    back = _load_json(back_path)

    if not isinstance(ssot, dict) or "master" not in ssot:
        raise TypeError(f"Unexpected tasks.json shape: {ssot_path}")
    if not isinstance(gameplay, list):
        raise TypeError(f"Unexpected tasks_gameplay.json shape: {gameplay_path}")
    if not isinstance(back, list):
        raise TypeError(f"Unexpected tasks_back.json shape: {back_path}")

    ssot_tasks = ssot.get("master", {}).get("tasks", [])
    by_tm_id = {int(t.get("id")): t for t in ssot_tasks if str(t.get("id", "")).isdigit()}

    by_tm_to_back_id = {int(e.get("taskmaster_id")): e.get("id") for e in back if str(e.get("taskmaster_id", "")).isdigit()}
    by_tm_to_gameplay_id = {int(e.get("taskmaster_id")): e.get("id") for e in gameplay if str(e.get("taskmaster_id", "")).isdigit()}

    # Phase-2 tasks we imported into tasks.json as T27..T41.
    phase2_tm_ids = list(range(27, 42))
    # Allocate deterministic gameplay IDs for these phase-2 tasks.
    phase2_id_map = {tm_id: f"GM-03{idx:02d}" for idx, tm_id in enumerate(phase2_tm_ids, start=1)}

    # Merge into lookup for dependency resolution.
    tm_to_any_id: dict[int, str] = {}
    tm_to_any_id.update(by_tm_to_back_id)
    tm_to_any_id.update(by_tm_to_gameplay_id)
    tm_to_any_id.update(phase2_id_map)

    today = dt.date.today().isoformat()
    now = dt.datetime.now(tz=dt.timezone.utc).isoformat()
    audit_dir = repo_root / "logs" / "ci" / today / "tasks-gameplay-sync"
    audit_dir.mkdir(parents=True, exist_ok=True)

    backup_path = audit_dir / f"tasks_gameplay.json.backup-{today}.json"
    _write_json(backup_path, gameplay)

    existing_tm_ids = {int(e.get("taskmaster_id")) for e in gameplay if str(e.get("taskmaster_id", "")).isdigit()}
    changes = []
    added = []

    # Append or update entries for each phase-2 task.
    for tm_id in phase2_tm_ids:
        t = by_tm_id.get(tm_id)
        if not t:
            changes.append({"taskmaster_id": tm_id, "action": "skip", "reason": "missing_in_tasks_json"})
            continue

        story_id = _taskmaster_story_id_from_details(str(t.get("details", ""))) or f"PRD-GUILD-MANAGER-PHASE2-T{tm_id}"
        entry_id = phase2_id_map[tm_id]

        depends = []
        for dep in (t.get("dependencies") or []):
            try:
                dep_int = int(dep)
            except Exception:
                continue
            dep_id = tm_to_any_id.get(dep_int)
            if dep_id and dep_id not in depends:
                depends.append(dep_id)

        entry = {
            "id": entry_id,
            "story_id": story_id,
            "title": t.get("title", ""),
            "description": t.get("description", ""),
            "status": t.get("status", "pending"),
            "priority": _prio_to_p(str(t.get("priority", ""))),
            "layer": _layer_for(tm_id),
            "depends_on": depends,
            "adr_refs": list(t.get("adrRefs") or []),
            "chapter_refs": list(t.get("archRefs") or []),
            "overlay_refs": _overlay_refs_min(str(t.get("overlay", ""))),
            "owner": "architecture",
            "labels": _labels_for(tm_id),
            "contractRefs": _contract_refs_for(tm_id),
            "artifactRefs": [
                "logs/ci/<YYYY-MM-DD>/tasks-gameplay-sync/report.json",
            ],
            "acceptance": _acceptance_for(tm_id),
            "test_strategy": _test_strategy_for(tm_id),
            "test_refs": _test_refs_for(tm_id),
            "taskmaster_id": tm_id,
            "taskmaster_exported": False,
        }

        if tm_id in existing_tm_ids:
            # Update existing entry in-place.
            for i, e in enumerate(gameplay):
                if int(e.get("taskmaster_id")) == tm_id:
                    before = {"id": e.get("id"), "title": e.get("title"), "depends_on": e.get("depends_on")}
                    gameplay[i] = entry
                    after = {"id": entry_id, "title": entry.get("title"), "depends_on": entry.get("depends_on")}
                    changes.append({"taskmaster_id": tm_id, "action": "update", "before": before, "after": after})
                    break
        else:
            gameplay.append(entry)
            added.append({"taskmaster_id": tm_id, "id": entry_id, "title": entry.get("title")})

    # Ensure deterministic order: keep existing order, then append phase2 tasks by taskmaster_id.
    # (Do not reorder existing entries.)
    # We already appended in tm_id order; keep as-is.

    _write_json(gameplay_path, gameplay)

    report = {
        "ts": now,
        "paths": {
            "tasks_json": str(ssot_path.as_posix()),
            "tasks_gameplay": str(gameplay_path.as_posix()),
            "tasks_gameplay_backup": str(backup_path.as_posix()),
            "tasks_back": str(back_path.as_posix()),
        },
        "phase2_taskmaster_ids": phase2_tm_ids,
        "phase2_gameplay_ids": phase2_id_map,
        "added": added,
        "changes": changes,
        "notes": [
            "Mapping rule preserved: tasks.json.master.tasks[].id -> tasks_gameplay[].taskmaster_id.",
            "depends_on references existing view IDs (GM-/NG-) whenever available to keep cross-file consistency.",
            "Acceptance/test_refs include suggested paths; actual test generation belongs to implementation stages.",
        ],
    }
    _write_json(audit_dir / "report.json", report)

    md = ["# tasks_gameplay sync report", "", f"- Date: {today}", f"- Added: {len(added)}", ""]
    for a in added:
        md.append(f"- {a['id']} (taskmaster_id={a['taskmaster_id']}): {a['title']}")
    if changes:
        md.append("")
        md.append(f"- Updated: {sum(1 for c in changes if c.get('action')=='update')}")
    (audit_dir / "report.md").write_text("\n".join(md) + "\n", encoding="utf-8")

    print(f"Updated: {gameplay_path}")
    print(f"Backup : {backup_path}")
    print(f"Report : {audit_dir / 'report.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

