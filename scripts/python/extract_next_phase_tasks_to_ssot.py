from __future__ import annotations

import datetime as dt
import json
from pathlib import Path


def _load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def _write_json(path: Path, data) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _next_task_id(tasks: list[dict]) -> int:
    ids = []
    for t in tasks:
        try:
            ids.append(int(t.get("id")))
        except Exception:
            continue
    return (max(ids) if ids else 0) + 1


def main() -> int:
    repo_root = Path(__file__).resolve().parents[2]
    ssot_path = repo_root / ".taskmaster" / "tasks" / "tasks.json"
    newguild_path = repo_root / ".taskmaster" / "tasks" / "tasks_newguild.json"

    ssot = _load_json(ssot_path)
    newguild = _load_json(newguild_path)

    if not isinstance(ssot, dict) or "master" not in ssot:
        raise TypeError(f"Unexpected tasks.json shape at {ssot_path}")
    if not isinstance(newguild, list):
        raise TypeError(f"Expected list at {newguild_path}")

    tasks: list[dict] = ssot.get("master", {}).get("tasks", [])
    if not isinstance(tasks, list):
        raise TypeError("tasks.json.master.tasks must be a list")

    today = dt.date.today().isoformat()
    now = dt.datetime.now(tz=dt.timezone.utc).isoformat()
    audit_dir = repo_root / "logs" / "ci" / today / "tasks-extract"
    audit_dir.mkdir(parents=True, exist_ok=True)

    backup_path = audit_dir / f"tasks.json.backup-{today}.json"
    _write_json(backup_path, ssot)

    # Source IDs in tasks_newguild.json
    source_ids = [12, 100, 89, 15, 48, 49, 50, 55, 25, 51, 53, 56, 57, 26, 27]
    by_source_id = {int(t.get("id")): t for t in newguild if "id" in t}

    # Idempotency: if a task already contains our source marker, skip it.
    existing_markers = set()
    for t in tasks:
        det = str(t.get("details", ""))
        if "Source-Newguild:" in det:
            for line in det.splitlines():
                if line.startswith("Source-Newguild:"):
                    existing_markers.add(line.strip())

    def marker(src_id: int) -> str:
        return f"Source-Newguild: T{src_id}"

    # Rewritten tasks: keep aligned to existing repo skeleton (Core/Adapters/Godot UI),
    # avoid parallel systems.
    rewritten_specs: list[dict] = [
        {
            "source_id": 89,
            "title": "阶段2：内容加载与 JSON 清单接入（Assets/Data）",
            "priority": "high",
            "complexity": 6,
            "dependencies": ["8", "11"],
            "adrRefs": ["ADR-0005", "ADR-0011", "ADR-0019"],
            "archRefs": ["CH01", "CH04", "CH05", "CH06", "CH07"],
            "overlay": "docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
            "description": "将现有 res://Game.Godot/Assets/Data/content/base/*.json 作为内容源，建立统一的加载/校验/缓存路径，并把结果以纯 C# DTO 供 Game.Core 使用（Core 不引用 Godot API）。",
            "details": "\n".join(
                [
                    "Story: PRD-GUILD-MANAGER-PHASE2-CONTENT",
                    "ADR Refs: ADR-0005; ADR-0011; ADR-0019",
                    "Chapters: CH01; CH04; CH05; CH06; CH07",
                    "Overlays: docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
                    marker(89),
                    "Rewrite-Intent: Extend existing content assets and adapters; do not introduce a parallel content pipeline.",
                ]
            ),
            "testStrategy": "TDD (red->green->refactor). Add xUnit tests for JSON parse/validation (pure). Add GdUnit4/headless smoke to load base manifest and fail fast with actionable errors. Write evidence under logs/ci and logs/e2e.",
        },
        {
            "source_id": 100,
            "title": "阶段2：事件定义与事件链契约（EventDefinition/EventChain）",
            "priority": "high",
            "complexity": 5,
            "dependencies": ["3"],
            "adrRefs": ["ADR-0004", "ADR-0005", "ADR-0018"],
            "archRefs": ["CH01", "CH04", "CH05", "CH06", "CH07"],
            "overlay": "docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
            "description": "在 Game.Core/Contracts 中补齐事件定义与事件链的强类型契约，并把 IEventCatalog 规范化为可被 EventEngine 使用的最小接口（避免 EmptyEventCatalog/裸实现漂移）。",
            "details": "\n".join(
                [
                    "Story: PRD-GUILD-MANAGER-PHASE2-EVENT-CONTRACTS",
                    "ADR Refs: ADR-0004; ADR-0005; ADR-0018",
                    "Chapters: CH01; CH04; CH05; CH06; CH07",
                    "Overlays: docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
                    marker(100),
                    "Rewrite-Intent: Formalize existing EventEngine inputs; do not build a second event engine.",
                ]
            ),
            "testStrategy": "xUnit contract tests: compile-level + EventType constants. Deterministic serialization tests for definitions (no Godot).",
        },
        {
            "source_id": 12,
            "title": "阶段2：内容驱动事件系统接入（EventCatalog + EventEngine）",
            "priority": "high",
            "complexity": 7,
            "dependencies": ["4", "27", "28"],
            "adrRefs": ["ADR-0004", "ADR-0005", "ADR-0006", "ADR-0018"],
            "archRefs": ["CH01", "CH04", "CH05", "CH06", "CH07"],
            "overlay": "docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
            "description": "把 EventEngine 从 T2 的硬编码演示升级为“由 EventCatalog 提供定义/开关/权重”的内容驱动实现；在 HUD/GameLoop 中替换 EmptyEventCatalog，确保事件可观察且可测试。",
            "details": "\n".join(
                [
                    "Story: PRD-GUILD-MANAGER-PHASE2-EVENT-CATALOG",
                    "ADR Refs: ADR-0004; ADR-0005; ADR-0006; ADR-0018",
                    "Chapters: CH01; CH04; CH05; CH06; CH07",
                    "Overlays: docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
                    marker(12),
                    "Rewrite-Intent: Extend existing GameTurnSystem/EventEngine; keep Core deterministic; publish events via existing IEventBus.",
                ]
            ),
            "testStrategy": "xUnit: EventEngine executes phases and emits expected domain events given a fixed catalog/time/id generator. GdUnit4: headless wiring-smoke validates at least one content-driven event is emitted and UI updates.",
        },
        {
            "source_id": 48,
            "title": "阶段2：UI 响应式布局与可点击性收口（Scroll/Anchor/MouseFilter）",
            "priority": "high",
            "complexity": 5,
            "dependencies": ["13", "19"],
            "adrRefs": ["ADR-0005", "ADR-0011", "ADR-0018"],
            "archRefs": ["CH01", "CH06", "CH07"],
            "overlay": "docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
            "description": "把当前 UI 超出窗口/被透明层遮挡等问题系统性收口：统一 ScrollContainer、Anchor、Container size flags 与 Control.mouse_filter，确保 HUD/Guild/Settings 可完整操作。",
            "details": "\n".join(
                [
                    "Story: PRD-GUILD-MANAGER-PHASE2-UI-RESPONSIVE",
                    "ADR Refs: ADR-0005; ADR-0011; ADR-0018",
                    "Chapters: CH01; CH06; CH07",
                    "Overlays: docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
                    marker(48),
                    "Rewrite-Intent: Fix wiring and usability; do not hide features to pass tests.",
                ]
            ),
            "testStrategy": "GdUnit4: instantiate key screens and assert critical buttons are clickable (mouse_filter not blocking). Headless smoke verifies the same paths.",
        },
        {
            "source_id": 49,
            "title": "阶段2：核心 UI 组件库（Status/Panel/List）",
            "priority": "medium",
            "complexity": 5,
            "dependencies": ["30"],
            "adrRefs": ["ADR-0005", "ADR-0018"],
            "archRefs": ["CH01", "CH06", "CH07"],
            "overlay": "docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
            "description": "沉淀可复用 UI 组件：状态条/错误提示/列表渲染/确认对话框，供战术中心、活动反馈、官员任命等复用，减少散乱 UI 逻辑。",
            "details": "\n".join(
                [
                    "Story: PRD-GUILD-MANAGER-PHASE2-UI-COMPONENTS",
                    "ADR Refs: ADR-0005; ADR-0018",
                    "Chapters: CH01; CH06; CH07",
                    "Overlays: docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
                    marker(49),
                    "Rewrite-Intent: Build reusable components to reduce duplicated UI glue code.",
                ]
            ),
            "testStrategy": "GdUnit4: instantiate each component scene, set sample props, and assert text/state transitions without timers.",
        },
        {
            "source_id": 50,
            "title": "阶段2：交互模式统一（Loading/Error/Disable/Retry）",
            "priority": "medium",
            "complexity": 4,
            "dependencies": ["31"],
            "adrRefs": ["ADR-0005", "ADR-0018", "ADR-0019"],
            "archRefs": ["CH01", "CH06", "CH07"],
            "overlay": "docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
            "description": "为按钮与异步操作建立统一交互：点击后进入 loading、错误可重试、成功自动刷新；错误信息遵循 ADR-0019 脱敏与审计。",
            "details": "\n".join(
                [
                    "Story: PRD-GUILD-MANAGER-PHASE2-UI-INTERACTION",
                    "ADR Refs: ADR-0005; ADR-0018; ADR-0019",
                    "Chapters: CH01; CH06; CH07",
                    "Overlays: docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
                    marker(50),
                    "Rewrite-Intent: Reduce 'stuck' UX; provide deterministic visible states.",
                ]
            ),
            "testStrategy": "GdUnit4: simulate button press and assert state transitions (disabled->loading->enabled) using frame waits only.",
        },
        {
            "source_id": 55,
            "title": "阶段2：活动反馈系统（Activity Feed / Timeline）",
            "priority": "medium",
            "complexity": 6,
            "dependencies": ["29", "32"],
            "adrRefs": ["ADR-0003", "ADR-0004", "ADR-0005", "ADR-0018"],
            "archRefs": ["CH01", "CH03", "CH04", "CH06", "CH07"],
            "overlay": "docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
            "description": "提供一个玩家可见的“发生了什么”面板：订阅 DomainEventEmitted，将关键事件（副本/声望/社交/奖励）按时间线展示，便于测试与玩家理解。",
            "details": "\n".join(
                [
                    "Story: PRD-GUILD-MANAGER-PHASE2-ACTIVITY-FEED",
                    "ADR Refs: ADR-0003; ADR-0004; ADR-0005; ADR-0018",
                    "Chapters: CH01; CH03; CH04; CH06; CH07",
                    "Overlays: docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
                    marker(55),
                    "Rewrite-Intent: Reuse existing EventBusAdapter signal; do not add bespoke log plumbing.",
                ]
            ),
            "testStrategy": "xUnit: event filtering and formatting rules. GdUnit4: publish a deterministic event and assert feed shows it.",
        },
        {
            "source_id": 15,
            "title": "阶段2：战术中心（Tactical Center）入口与最小编成",
            "priority": "medium",
            "complexity": 7,
            "dependencies": ["13", "17", "31"],
            "adrRefs": ["ADR-0004", "ADR-0005", "ADR-0007", "ADR-0018"],
            "archRefs": ["CH01", "CH04", "CH05", "CH06", "CH07"],
            "overlay": "docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
            "description": "为 PVE 副本建立一个统一入口：最小队伍编成/校验/自动分配（基于现有 roster），并驱动现有 RaidEncounter demo/事件输出。",
            "details": "\n".join(
                [
                    "Story: PRD-GUILD-MANAGER-PHASE2-TACTICAL-CENTER",
                    "ADR Refs: ADR-0004; ADR-0005; ADR-0007; ADR-0018",
                    "Chapters: CH01; CH04; CH05; CH06; CH07",
                    "Overlays: docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
                    marker(15),
                    "Rewrite-Intent: Add a single UI entry that drives existing PVE/roster modules.",
                ]
            ),
            "testStrategy": "xUnit: composition validation (deterministic). GdUnit4: open Tactical Center, trigger a raid, observe core.raid.resolved and UI status update.",
        },
        {
            "source_id": 25,
            "title": "阶段2：奖励发放统一（Reward Ledger）",
            "priority": "high",
            "complexity": 6,
            "dependencies": ["17", "19", "25"],
            "adrRefs": ["ADR-0004", "ADR-0005", "ADR-0006", "ADR-0018"],
            "archRefs": ["CH01", "CH04", "CH05", "CH06", "CH07"],
            "overlay": "docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
            "description": "如果没有其他任务负责战斗奖励发放，则在本阶段实现统一奖励账本：监听副本/媒体/事件结果并发放奖励（金币/声望/经验等的最小集合），可 Save/Load 回放。",
            "details": "\n".join(
                [
                    "Story: PRD-GUILD-MANAGER-PHASE2-REWARDS",
                    "ADR Refs: ADR-0004; ADR-0005; ADR-0006; ADR-0018",
                    "Chapters: CH01; CH04; CH05; CH06; CH07",
                    "Overlays: docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
                    marker(25),
                    "Rewrite-Intent: Centralize reward logic; avoid ad-hoc reward mutations scattered across demos.",
                ]
            ),
            "testStrategy": "xUnit: reward rules deterministic for known events. GdUnit4: run raid demo, verify reward UI updated and persists across save/load.",
        },
        {
            "source_id": 51,
            "title": "阶段2：成就系统（Achievement Definitions + Tracker）",
            "priority": "medium",
            "complexity": 5,
            "dependencies": ["36"],
            "adrRefs": ["ADR-0004", "ADR-0005", "ADR-0018"],
            "archRefs": ["CH01", "CH04", "CH06", "CH07"],
            "overlay": "docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
            "description": "建立最小成就定义与追踪：由 DomainEvent 驱动达成判定，并在 UI 中可查看；与奖励账本对齐。",
            "details": "\n".join(
                [
                    "Story: PRD-GUILD-MANAGER-PHASE2-ACHIEVEMENTS",
                    "ADR Refs: ADR-0004; ADR-0005; ADR-0018",
                    "Chapters: CH01; CH04; CH06; CH07",
                    "Overlays: docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
                    marker(51),
                    "Rewrite-Intent: Drive achievements off existing DomainEvent stream.",
                ]
            ),
            "testStrategy": "xUnit: given events -> achievement unlocked. GdUnit4: trigger one achievement and verify UI list updates.",
        },
        {
            "source_id": 53,
            "title": "阶段2：经验系统（XP/Level）与奖励对齐",
            "priority": "medium",
            "complexity": 4,
            "dependencies": ["35"],
            "adrRefs": ["ADR-0004", "ADR-0005", "ADR-0018"],
            "archRefs": ["CH01", "CH04", "CH06", "CH07"],
            "overlay": "docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
            "description": "实现最小经验/等级曲线（确定性），并作为 Reward Ledger 的一种奖励类型展示与持久化。",
            "details": "\n".join(
                [
                    "Story: PRD-GUILD-MANAGER-PHASE2-XP",
                    "ADR Refs: ADR-0004; ADR-0005; ADR-0018",
                    "Chapters: CH01; CH04; CH06; CH07",
                    "Overlays: docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
                    marker(53),
                    "Rewrite-Intent: Keep math deterministic and testable; wire to UI through adapters.",
                ]
            ),
            "testStrategy": "xUnit: XP->Level boundary tests. GdUnit4: show XP changes after a raid and persist across save/load.",
        },
        {
            "source_id": 56,
            "title": "阶段2：官员系统（Officer Slots + Assignment）",
            "priority": "medium",
            "complexity": 5,
            "dependencies": ["13", "25"],
            "adrRefs": ["ADR-0005", "ADR-0006", "ADR-0018"],
            "archRefs": ["CH01", "CH05", "CH06", "CH07"],
            "overlay": "docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
            "description": "在 Guild roster 之上增加最小官员槽位与任命规则（确定性），并落库/随存档持久化。",
            "details": "\n".join(
                [
                    "Story: PRD-GUILD-MANAGER-PHASE2-OFFICERS",
                    "ADR Refs: ADR-0005; ADR-0006; ADR-0018",
                    "Chapters: CH01; CH05; CH06; CH07",
                    "Overlays: docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
                    marker(56),
                    "Rewrite-Intent: Extend existing guild persistence; do not add new DB outside SqliteDataStore path.",
                ]
            ),
            "testStrategy": "xUnit: assignment rules and persistence mapping. GdUnit4: assign an officer and verify it survives restart (save/load).",
        },
        {
            "source_id": 57,
            "title": "阶段2：官员 UI 入口（Simplified Officer UI）",
            "priority": "medium",
            "complexity": 4,
            "dependencies": ["38", "32"],
            "adrRefs": ["ADR-0005", "ADR-0018"],
            "archRefs": ["CH01", "CH06", "CH07"],
            "overlay": "docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
            "description": "提供官员管理的最小 UI：列表展示、任命/撤销、状态提示；交互模式复用统一组件。",
            "details": "\n".join(
                [
                    "Story: PRD-GUILD-MANAGER-PHASE2-OFFICERS-UI",
                    "ADR Refs: ADR-0005; ADR-0018",
                    "Chapters: CH01; CH06; CH07",
                    "Overlays: docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
                    marker(57),
                    "Rewrite-Intent: UI should call Core services via adapters; no direct SQL in scenes.",
                ]
            ),
            "testStrategy": "GdUnit4: open officers panel, assign a member, assert labels updated; no timing-based sleeps.",
        },
        {
            "source_id": 26,
            "title": "阶段2：世界生成端口（World Generation Port）与开局 Seed",
            "priority": "medium",
            "complexity": 6,
            "dependencies": ["15", "25"],
            "adrRefs": ["ADR-0005", "ADR-0006", "ADR-0007", "ADR-0018"],
            "archRefs": ["CH01", "CH05", "CH06", "CH07"],
            "overlay": "docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
            "description": "把世界状态初始化收口为一个端口：开局生成 NPC 公会/对手/基础赛季信息，并与 Save/Load 对齐（可重放）。",
            "details": "\n".join(
                [
                    "Story: PRD-GUILD-MANAGER-PHASE2-WORLDGEN",
                    "ADR Refs: ADR-0005; ADR-0006; ADR-0007; ADR-0018",
                    "Chapters: CH01; CH05; CH06; CH07",
                    "Overlays: docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
                    marker(26),
                    "Rewrite-Intent: Single source of truth for world seed; wire to UI for deterministic testing.",
                ]
            ),
            "testStrategy": "xUnit: deterministic generation given seed. GdUnit4: new game initializes world and displays NPC guild count.",
        },
        {
            "source_id": 27,
            "title": "阶段2：NPC 公会原型（Archetypes）数据驱动落地",
            "priority": "medium",
            "complexity": 6,
            "dependencies": ["40", "27"],
            "adrRefs": ["ADR-0005", "ADR-0006", "ADR-0018"],
            "archRefs": ["CH01", "CH05", "CH06", "CH07"],
            "overlay": "docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
            "description": "将 NPC 公会原型从 json 数据驱动（如 base/npc_guilds.json），并与世界生成端口对齐；避免散落硬编码。",
            "details": "\n".join(
                [
                    "Story: PRD-GUILD-MANAGER-PHASE2-NPC-ARCHETYPES",
                    "ADR Refs: ADR-0005; ADR-0006; ADR-0018",
                    "Chapters: CH01; CH05; CH06; CH07",
                    "Overlays: docs/architecture/overlays/PRD-Guild-Manager/08/_index.md",
                    marker(27),
                    "Rewrite-Intent: Data-driven NPC world; reuse existing content assets and persistence.",
                ]
            ),
            "testStrategy": "xUnit: parse/validate npc guild archetypes. GdUnit4: load content and show at least one archetype in UI.",
        },
    ]

    # Build tasks to append, allocate new numeric IDs in SSoT.
    next_id = _next_task_id(tasks)
    appended = []
    for spec in rewritten_specs:
        src_id = int(spec["source_id"])
        m = marker(src_id)
        if m in existing_markers:
            appended.append({"source_id": src_id, "skipped": True, "reason": "already-present"})
            continue

        src = by_source_id.get(src_id, {})
        src_title = src.get("title")

        t = {
            "id": str(next_id),
            "title": spec["title"],
            "description": spec["description"],
            "details": spec["details"] + (f"\nSource-Title: {src_title}" if src_title else ""),
            "testStrategy": spec["testStrategy"],
            "adrRefs": spec["adrRefs"],
            "archRefs": spec["archRefs"],
            "overlay": spec["overlay"],
            "priority": spec["priority"],
            "complexity": spec["complexity"],
            "dependencies": spec["dependencies"],
            "status": "pending",
            "subtasks": [],
            "recommendedSubtasks": 0,
            "updatedAt": now,
        }

        tasks.append(t)
        appended.append({"source_id": src_id, "new_task_id": next_id, "title": spec["title"]})
        next_id += 1

    ssot["master"]["tasks"] = tasks
    _write_json(ssot_path, ssot)

    report = {
        "ts": now,
        "paths": {
            "ssot": str(ssot_path.as_posix()),
            "ssot_backup": str(backup_path.as_posix()),
            "newguild": str(newguild_path.as_posix()),
        },
        "imported_sources": source_ids,
        "results": appended,
        "notes": [
            "Tasks were rewritten to extend existing skeleton (Game.Core + Adapters + Godot UI).",
            "No parallel event engine/content pipeline should be introduced; use existing EventEngine/GameTurnSystem/IEventBus.",
        ],
    }
    _write_json(audit_dir / "report.json", report)

    md = ["# tasks.json import report", "", f"- Date: {today}", f"- Imported specs: {len(rewritten_specs)}", ""]
    for r in appended:
        if r.get("skipped"):
            md.append(f"- Source T{r['source_id']}: skipped ({r.get('reason')})")
        else:
            md.append(f"- Source T{r['source_id']} -> tasks.json T{r['new_task_id']}: {r['title']}")
    (audit_dir / "report.md").write_text("\n".join(md) + "\n", encoding="utf-8")

    print(f"Updated: {ssot_path}")
    print(f"Backup : {backup_path}")
    print(f"Report : {audit_dir / 'report.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

