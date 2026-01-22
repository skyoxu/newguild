from __future__ import annotations

import datetime as dt
import json
from pathlib import Path


def _write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")


def _read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def _append_story_mapping(index_text: str, lines_to_add: list[str]) -> str:
    """
    Insert additional story_id → overlay page entries into 08/_index.md.
    We insert before the "- Core" section to keep grouping stable.
    """
    marker = "- Core"
    parts = index_text.splitlines()
    out: list[str] = []
    inserted = False
    for line in parts:
        if not inserted and line.strip() == marker:
            # Ensure a blank line before insertion if not already present
            if out and out[-1].strip():
                out.append("")
            out.extend(lines_to_add)
            out.append("")
            inserted = True
        out.append(line)
    if not inserted:
        # Fallback: append at end.
        if out and out[-1].strip():
            out.append("")
        out.extend(lines_to_add)
        out.append("")
    return "\n".join(out) + "\n"


def _ensure_story_entries_once(index_text: str, story_lines: list[str]) -> tuple[str, bool]:
    # Avoid duplicate insertion: require at least the first story line to be absent.
    probe = story_lines[0].strip()
    if probe and probe in index_text:
        return index_text, False
    return _append_story_mapping(index_text, story_lines), True


def _update_tasks_overlay(tasks_json_path: Path, mapping: dict[int, str], *, audit_dir: Path) -> None:
    doc = json.loads(tasks_json_path.read_text(encoding="utf-8"))
    tasks = doc.get("master", {}).get("tasks", [])
    changed = []
    for t in tasks:
        try:
            tid = int(t.get("id"))
        except Exception:
            continue
        if tid not in mapping:
            continue
        before = t.get("overlay")
        after = mapping[tid]
        if before == after:
            continue
        t["overlay"] = after
        t["updatedAt"] = dt.datetime.now(tz=dt.timezone.utc).isoformat()
        changed.append({"id": tid, "before": before, "after": after, "title": t.get("title")})

    doc["master"]["tasks"] = tasks
    tasks_json_path.write_text(json.dumps(doc, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (audit_dir / "tasks-overlay-changes.json").write_text(
        json.dumps({"changed": changed}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )


def main() -> int:
    repo_root = Path(__file__).resolve().parents[2]
    overlay_dir = repo_root / "docs" / "architecture" / "overlays" / "PRD-Guild-Manager" / "08"
    index_path = overlay_dir / "_index.md"
    tasks_json_path = repo_root / ".taskmaster" / "tasks" / "tasks.json"

    today = dt.date.today().isoformat()
    audit_dir = repo_root / "logs" / "ci" / today / "overlays-phase2"
    audit_dir.mkdir(parents=True, exist_ok=True)

    # Phase 2 overlay pages (English filenames, Chinese content).
    pages = [
        {
            "file": "08-FeatureSlice-Phase2-Content-Loading.md",
            "title": "08 Phase 2：内容加载与 JSON 清单接入（Assets/Data）",
            "status": "Draft",
            "adr": ["ADR-0005", "ADR-0011", "ADR-0019"],
            "ch": ["CH01", "CH04", "CH05", "CH06", "CH07"],
            "tasks": ["T27"],
            "story": "PRD-GUILD-MANAGER-PHASE2-CONTENT",
            "notes": [
                "将 res://Game.Godot/Assets/Data/content/base/manifest.json 作为入口清单（不复制 PRD）。",
                "Contracts 仍以 Game.Core/Contracts/** 为 SSoT，页面只写事件类型与文件路径。",
            ],
        },
        {
            "file": "08-FeatureSlice-Phase2-Events-ContentDriven.md",
            "title": "08 Phase 2：内容驱动事件（EventCatalog + EventEngine）与命名收口",
            "status": "Draft",
            "adr": ["ADR-0004", "ADR-0005", "ADR-0006", "ADR-0018"],
            "ch": ["CH01", "CH04", "CH05", "CH06", "CH07"],
            "tasks": ["T28", "T29", "T42"],
            "story": "PRD-GUILD-MANAGER-PHASE2-EVENT-CATALOG",
            "notes": [
                "禁止引入第二套事件引擎：只扩展现有 EventEngine/GameTurnSystem 的输入与驱动方式。",
                "事件命名统一迁移（core.*.*）作为止损前置，避免 Phase2 新内容继续使用旧名。",
            ],
        },
        {
            "file": "08-FeatureSlice-Phase2-UI-Usability.md",
            "title": "08 Phase 2：UI 可用性与交互模式统一（Responsive + Clickability）",
            "status": "Draft",
            "adr": ["ADR-0005", "ADR-0011", "ADR-0018", "ADR-0019"],
            "ch": ["CH01", "CH06", "CH07"],
            "tasks": ["T30", "T31", "T32", "T33"],
            "story": "PRD-GUILD-MANAGER-PHASE2-UI-RESPONSIVE",
            "notes": [
                "收口 Scroll/Anchor/mouse_filter，避免透明层吞输入导致不可玩。",
                "统一 loading/error/retry/disabled 交互模式，降低“卡住/无反馈”风险。",
            ],
        },
        {
            "file": "08-FeatureSlice-Phase2-Tactical-Rewards.md",
            "title": "08 Phase 2：战术中心入口与奖励闭环（Tactical + Rewards + XP + Achievements）",
            "status": "Draft",
            "adr": ["ADR-0004", "ADR-0005", "ADR-0006", "ADR-0007", "ADR-0018"],
            "ch": ["CH01", "CH04", "CH05", "CH06", "CH07"],
            "tasks": ["T34", "T35", "T37", "T36"],
            "story": "PRD-GUILD-MANAGER-PHASE2-REWARDS",
            "notes": [
                "奖励发放统一：若没有其他任务负责战斗奖励发放，本阶段在 Reward Ledger 完成。",
                "战术中心只做统一入口与最小编成/校验，驱动现有 PVE/Raid demo 与事件输出。",
            ],
        },
        {
            "file": "08-FeatureSlice-Phase2-Officers.md",
            "title": "08 Phase 2：官员系统（Officer Slots + UI Entry）",
            "status": "Draft",
            "adr": ["ADR-0005", "ADR-0006", "ADR-0018"],
            "ch": ["CH01", "CH05", "CH06", "CH07"],
            "tasks": ["T38", "T39"],
            "story": "PRD-GUILD-MANAGER-PHASE2-OFFICERS",
            "notes": [
                "官员数据与规则在 Core；UI 仅调用服务与展示，不直接写 SQL。",
                "需随 Save/Load 持久化，避免新入口不可回放。",
            ],
        },
        {
            "file": "08-FeatureSlice-Phase2-Worldgen.md",
            "title": "08 Phase 2：世界生成端口与 NPC 原型（WorldGen + Archetypes）",
            "status": "Draft",
            "adr": ["ADR-0005", "ADR-0006", "ADR-0007", "ADR-0018"],
            "ch": ["CH01", "CH05", "CH06", "CH07"],
            "tasks": ["T40", "T41"],
            "story": "PRD-GUILD-MANAGER-PHASE2-WORLDGEN",
            "notes": [
                "世界状态收口在一个端口里：固定 seed 可复现；与 Save/Load 对齐。",
                "NPC 公会原型来自内容 JSON（例如 base/npc_guilds.json），避免硬编码漂移。",
            ],
        },
        {
            "file": "08-FeatureSlice-Phase2-Architecture-Guards.md",
            "title": "08 Phase 2：架构依赖护栏（Ports/Adapters）",
            "status": "Draft",
            "adr": ["ADR-0005", "ADR-0007", "ADR-0011", "ADR-0018"],
            "ch": ["CH01", "CH05", "CH06", "CH07"],
            "tasks": ["T43"],
            "story": "PHASE2-BACK-DEPENDENCY-GUARDS",
            "notes": [
                "在 Phase2 扩展中避免 Core <-> Godot API 互相侵入，提供依赖矩阵与脚本校验骨架。",
            ],
        },
    ]

    created = []
    for page in pages:
        path = overlay_dir / page["file"]
        if path.exists():
            continue
        fm = [
            "---",
            "PRD-ID: PRD-Guild-Manager",
            f"Title: {page['title']}",
            f"Status: {page['status']}",
            "ADR-Refs:",
        ] + [f"  - {x}" for x in page["adr"]] + [
            "Arch-Refs:",
        ] + [f"  - {x}" for x in page["ch"]] + [
            "---",
            "",
        ]
        body = [
            "## 范围与非目标（止损）",
            "",
            "- 范围：仅覆盖 Phase 2 的“内容驱动 + UI 入口”相关纵切；不替代 PRD/Tasks。",
            "- 非目标：不复制 Base/ADR 阈值，不在文档复制 Contracts 字段定义。",
            "",
            "## 关联任务（SSoT）",
            "",
        ] + [f"- `{t}`（见 `.taskmaster/tasks/tasks.json`）" for t in page["tasks"]] + [
            "",
            "## 事件与契约（ADR-0004）",
            "",
            "- 事件类型与触发时机以 `Game.Core/Contracts/**` 为准；本页仅提供索引与口径说明。",
            "",
            "## 验收与证据链（Draft）",
            "",
            "- 本页为 Draft：当对应任务进入实现阶段时，将通过 view 任务 `acceptance[]` 的 `Refs:` 与测试文件内 `ACC:T<id>.<n>` anchors 建立确定性证据链。",
            "",
            "## 备注",
            "",
        ] + [f"- {n}" for n in page["notes"]] + [
            "",
        ]
        _write_text(path, "\n".join(fm + body))
        created.append(page["file"])

    # Update _index.md story mapping section (add phase2 mapping).
    index_text = _read_text(index_path)
    phase2_story_lines = [
        "  - `PRD-GUILD-MANAGER-PHASE2-CONTENT` → `08-FeatureSlice-Phase2-Content-Loading.md`（T27 / GM-0301）",
        "  - `PRD-GUILD-MANAGER-PHASE2-EVENT-CONTRACTS` → `08-FeatureSlice-Phase2-Events-ContentDriven.md`（T28 / GM-0302）",
        "  - `PRD-GUILD-MANAGER-PHASE2-EVENT-CATALOG` → `08-FeatureSlice-Phase2-Events-ContentDriven.md`（T29,T42 / GM-0303）",
        "  - `PRD-GUILD-MANAGER-PHASE2-UI-RESPONSIVE` → `08-FeatureSlice-Phase2-UI-Usability.md`（T30-33 / GM-0304..GM-0307）",
        "  - `PRD-GUILD-MANAGER-PHASE2-TACTICAL-CENTER` → `08-FeatureSlice-Phase2-Tactical-Rewards.md`（T34 / GM-0308）",
        "  - `PRD-GUILD-MANAGER-PHASE2-REWARDS` → `08-FeatureSlice-Phase2-Tactical-Rewards.md`（T35-37 / GM-0309..GM-0311）",
        "  - `PRD-GUILD-MANAGER-PHASE2-OFFICERS` → `08-FeatureSlice-Phase2-Officers.md`（T38-39 / GM-0312..GM-0313）",
        "  - `PRD-GUILD-MANAGER-PHASE2-WORLDGEN` → `08-FeatureSlice-Phase2-Worldgen.md`（T40-41 / GM-0314..GM-0315）",
        "  - `PHASE2-BACK-DEPENDENCY-GUARDS` → `08-FeatureSlice-Phase2-Architecture-Guards.md`（T43）",
    ]
    new_index, changed = _ensure_story_entries_once(index_text, phase2_story_lines)
    if changed:
        _write_text(index_path, new_index)

    # Update tasks.json overlay pointers for Phase2 tasks to specific overlay pages.
    overlay_map = {
        27: "docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Content-Loading.md",
        28: "docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Events-ContentDriven.md",
        29: "docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Events-ContentDriven.md",
        30: "docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-UI-Usability.md",
        31: "docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-UI-Usability.md",
        32: "docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-UI-Usability.md",
        33: "docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-UI-Usability.md",
        34: "docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Tactical-Rewards.md",
        35: "docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Tactical-Rewards.md",
        36: "docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Tactical-Rewards.md",
        37: "docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Tactical-Rewards.md",
        38: "docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Officers.md",
        39: "docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Officers.md",
        40: "docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Worldgen.md",
        41: "docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Worldgen.md",
        42: "docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Events-ContentDriven.md",
        43: "docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Architecture-Guards.md",
    }
    _update_tasks_overlay(tasks_json_path, overlay_map, audit_dir=audit_dir)

    # Write audit report
    report = {
        "ts": dt.datetime.now(tz=dt.timezone.utc).isoformat(),
        "created_pages": created,
        "index_updated": changed,
        "overlay_dir": str(overlay_dir.as_posix()),
        "tasks_overlay_updated_for": sorted(list(overlay_map.keys())),
    }
    (audit_dir / "report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (audit_dir / "report.md").write_text(
        "\n".join(
            [
                "# Phase2 overlays sync report",
                "",
                f"- Date: {today}",
                f"- Created pages: {len(created)}",
                f"- Index updated: {'yes' if changed else 'no'}",
                "",
                "## Created",
                "",
            ]
            + ([f"- {x}" for x in created] if created else ["- (none)"])
            + ["", "## Notes", "", "- Pages are Draft; Test-Refs should be added when implementing tasks.", ""]
        ),
        encoding="utf-8",
    )

    print(f"Overlay dir: {overlay_dir}")
    print(f"Created pages: {len(created)}")
    print(f"Index updated: {changed}")
    print(f"Report: {audit_dir / 'report.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

