#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
One-off helper script to append GameLoop-related technical debt tasks
(NG-0039 .. NG-0044) into .taskmaster/tasks/tasks_back.json using UTF-8.

It is safe to run multiple times; existing ids will not be duplicated.
"""

from __future__ import annotations

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
TASKS_BACK_FILE = ROOT / ".taskmaster" / "tasks" / "tasks_back.json"


def main() -> None:
    if not TASKS_BACK_FILE.exists():
        raise SystemExit(f"tasks_back.json not found at {TASKS_BACK_FILE}")

    data = json.loads(TASKS_BACK_FILE.read_text(encoding="utf-8"))
    if not isinstance(data, list):
        raise SystemExit("tasks_back.json is expected to be a JSON array.")

    existing_ids = {t.get("id") for t in data if isinstance(t, dict)}
    new_tasks = []

    def add_task(task: dict) -> None:
        tid = task.get("id")
        if not tid or tid in existing_ids:
            return
        new_tasks.append(task)
        existing_ids.add(tid)

    # Stage 0: GameLoop security audit baseline and task mapping
    add_task(
        {
            "id": "NG-0039",
            "story_id": "PH16-GAMELOOP-SECURITY-BASELINE",
            "title": "GameLoop 安全审计基线与任务映射收口",
            "description": (
                "根据最新的 GameLoop 安全审计报告（logs/security-audit-gameloop.*）和 cifix.txt，"
                "对照 ADR-0019/0004/0005/0018 重新梳理 F001–F007，区分 T2 阶段必须完成与 NG 安全 backlog 的工作，"
                "并在 tasks_back.json / tasks_gameplay.json 中补齐 adr_refs/chapter_refs/overlay_refs/test_refs，"
                "使后续审计与 task_links_validate 以统一口径运行。"
            ),
            "status": "pending",
            "priority": "P1",
            "layer": "docs",
            "depends_on": ["NG-0001"],
            "adr_refs": [
                "ADR-0019",
                "ADR-0004",
                "ADR-0005",
                "ADR-0018",
            ],
            "chapter_refs": [
                "CH01",
                "CH02",
                "CH04",
                "CH06",
                "CH07",
            ],
            "overlay_refs": [],
            "labels": [
                "security",
                "gameloop",
                "docs",
                "audit",
            ],
            "owner": "architecture",
            "test_refs": [
                "logs/security-audit-gameloop.json",
                "logs/SECURITY_AUDIT_GAMELOOP_REPORT.md",
                "cifix.txt",
            ],
            "acceptance": [
                "SECURITY_AUDIT_GAMELOOP_REPORT.md 以 ADR-0019 为基线，明确区分 T2_BLOCKING 与 NG_BACKLOG 安全问题，并与 cifix.txt 中的技术债记录一致。",
                "F001/F002/F005 至少映射到一条 T2 相关 NG/GM 任务，F003/F004/F006/F007 映射到 NG 安全或架构 backlog 任务，且在 tasks_back.json / tasks_gameplay.json 中补齐 adr_refs/chapter_refs/overlay_refs/test_refs。",
                "运行 py -3 scripts/python/task_links_validate.py 时，GameLoop 相关任务不再因为 ADR/Chapter 映射或缺失 Test-Refs 报错。",
            ],
            "test_strategy": [
                "人工对比 logs/security-audit-gameloop.* 与 tasks_back.json / tasks_gameplay.json，确认每条 F00x 至少有一条对应任务可追踪。",
                "运行 task_links_validate.py 与 check_tasks_all_refs.py，确认新增/调整后的 NG/GM 任务全部通过回链校验。",
            ],
            "taskmaster_exported": False,
        }
    )

    # Stage 1: SaveIdValue + validation + security tests
    add_task(
        {
            "id": "NG-0040",
            "story_id": "PH16-GAMELOOP-SAVEID-VALUE",
            "title": "GameLoop SaveIdValue 值对象与白名单校验",
            "description": (
                "在 Game.Core 中为 GameLoop 引入 SaveIdValue 值对象（或等价验证器），"
                "实现基于 ADR-0019 的白名单规则（仅允许 [a-zA-Z0-9_-] 且长度 1-64），"
                "并在 GameTurnState/GameTurnSystem 与 GameLoop 相关合约中统一使用该类型；"
                "同时新增 SaveId 安全向单元测试，覆盖非法输入与边界值。"
            ),
            "status": "pending",
            "priority": "P1",
            "layer": "core",
            "depends_on": ["NG-0039"],
            "adr_refs": [
                "ADR-0019",
                "ADR-0004",
                "ADR-0005",
                "ADR-0018",
            ],
            "chapter_refs": [
                "CH01",
                "CH02",
                "CH04",
                "CH06",
                "CH07",
            ],
            "overlay_refs": [],
            "labels": [
                "security",
                "gameloop",
                "core",
                "t2",
            ],
            "owner": "architecture",
            "test_refs": [
                "Game.Core/Domain/Turn/SaveIdValue.cs",
                "Game.Core.Tests/Domain/SaveIdValueTests.cs",
                "Game.Core.Tests/Domain/GameTurnSystemTests.cs",
                "Game.Core.Tests/Domain/GameLoopTests.cs",
            ],
            "acceptance": [
                "Game.Core/Domain/Turn 下新增 SaveIdValue（或等价类型），实现 [a-zA-Z0-9_-]{1,64} 的白名单校验，非法输入抛出 ArgumentException。",
                "GameTurnState / GameTurnSystem 在内部使用 SaveIdValue 作为 SaveId 的入口类型，确保后续事件与存储只接受经过校验的值。",
                "Game.Core.Tests 中新增 SaveIdValueTests 与针对无效 SaveId 的 GameTurnSystem / GameLoop 测试，覆盖空字符串、超长、SQL 片段、路径遍历等典型攻击向量。",
                "dotnet test Game.Core.Tests 在本地与 CI 均可通过，且与 PRD 3.0.3 T2 可玩性场景流描述一致。",
            ],
            "test_strategy": [
                "单元测试：在 SaveIdValueTests 中穷举典型非法/合法输入，验证构造/工厂方法的行为与异常信息。",
                "集成测试：在 GameTurnSystemTests 与 GameLoopTests 中增加使用非法 SaveId 的路径，验证系统拒绝不符合白名单的存档槽 ID。",
            ],
            "taskmaster_exported": False,
        }
    )

    # Stage 2: Architecture refinement (ports / time / state)
    add_task(
        {
            "id": "NG-0041",
            "story_id": "PH07-GAMELOOP-ARCH-REFINE",
            "title": "GameLoop 架构骨架收敛：Ports/时间/状态整理",
            "description": (
                "根据 cifix.txt 的建议，围绕 GameLoop 相关代码完成一轮轻量级架构收敛："
                "将 IAICoordinator/IEventEngine/IGameTurnSystem/IEventBus 等接口统一归档到 Ports 命名空间；"
                "移除当前未使用的 ITime/IAICoordinator 依赖或明确使用场景；"
                "将 _firstTurnStarted 等可变实例状态迁移到 GameTurnState 等不可变记录类型中；"
                "并统一使用 DateTimeOffset 表达时间语义。"
            ),
            "status": "pending",
            "priority": "P2",
            "layer": "core",
            "depends_on": ["NG-0020"],
            "adr_refs": [
                "ADR-0007",
                "ADR-0005",
                "ADR-0018",
            ],
            "chapter_refs": [
                "CH01",
                "CH05",
                "CH06",
                "CH07",
            ],
            "overlay_refs": [],
            "labels": [
                "architecture",
                "gameloop",
                "ports",
                "refactor",
            ],
            "owner": "architecture",
            "test_refs": [
                "Game.Core/Engine/GameTurnSystem.cs",
                "Game.Core/Domain/Turn/GameTurnState.cs",
                "Game.Core/Contracts/DomainEvent.cs",
                "Game.Core.Tests/Domain/GameTurnSystemTests.cs",
            ],
            "acceptance": [
                "所有 GameLoop 相关接口（IGameTurnSystem/IEventEngine/IAICoordinator/IEventBus 及相关仓储接口）统一落盘在 Game.Core.Ports 命名空间或其子命名空间中。",
                "GameTurnSystem 中不再保留完全未使用的 ITime/IAICoordinator 依赖字段，或在测试中有明确用途并通过测试用例覆盖。",
                "与首回合相关的状态（如是否已发布 GameTurnStarted）通过 GameTurnState 等不可变 record 表达，而不是隐藏在可变实例字段中。",
                "DomainEvent 等核心事件类型统一使用 DateTimeOffset 表示时间，避免新的 DateTime 混用进入 GameLoop 关键路径。",
            ],
            "test_strategy": [
                "结构检查：通过现有 dependency_guard.py（或临时脚本）扫描接口文件位置与命名空间，确认 Ports/Domain/Engine 层次符合 ADR-0007。",
                "回归测试：运行 Game.Core.Tests 全量，确保架构调整后所有 GameLoop 相关测试依旧通过。",
            ],
            "taskmaster_exported": False,
        }
    )

    # Stage 3: Storage and path security (SqliteDataStore + GodotSQLiteDatabase)
    add_task(
        {
            "id": "NG-0042",
            "story_id": "PH06-GAMELOOP-SQLITE-PATH-HARDENING",
            "title": "SqliteDataStore 路径校验与插件后端安全收紧",
            "description": (
                "在 Game.Godot/Adapters/SqliteDataStore 中增强路径与扩展名校验：对 user:// 路径进行规范化后再做前缀与 .. 检查，"
                "增加 .db/.sqlite/.sqlite3 等扩展名白名单，并为已有 ValidatePath 逻辑补充文件大小上限检查；"
                "同时通过环境变量限制 plugin backend 的启用范围（如仅在开发环境允许），将 plugin backend 风险收敛为受控技术债。"
            ),
            "status": "pending",
            "priority": "P2",
            "layer": "adapter",
            "depends_on": ["NG-0014"],
            "adr_refs": [
                "ADR-0019",
                "ADR-0006",
                "ADR-0005",
                "ADR-0011",
            ],
            "chapter_refs": [
                "CH02",
                "CH05",
                "CH07",
                "CH10",
            ],
            "overlay_refs": [],
            "labels": [
                "security",
                "sqlite",
                "godot",
                "adapter",
            ],
            "owner": "architecture",
            "test_refs": [
                "Game.Godot/Adapters/SqliteDataStore.cs",
                "logs/ci/<date>/security-audit.jsonl",
            ],
            "acceptance": [
                "SqliteDataStore 对传入路径在检查前统一规范化分隔符，严格限制为 user:// 前缀并拒绝任何包含 .. 的路径。",
                "SqliteDataStore 增加数据库文件扩展名白名单（如 .db/.sqlite/.sqlite3），非法扩展名会被拒绝并记录审计日志。",
                "针对已有数据库文件增加简单的大小上限检查（防 DoS），超限时抛出安全异常并写入 security-audit.jsonl。",
                "通过环境变量（例如 ALLOW_PLUGIN_BACKEND）或等价方案，将 plugin backend 限定在开发/实验环境，生产默认使用 managed backend，并在文档中明确记录。",
            ],
            "test_strategy": [
                "单元/集成测试：为 SqliteDataStore 增加路径与扩展名相关的测试用例，覆盖非法前缀、..、非法扩展名、超大文件等场景。",
                "安全审计：在启用/禁用 plugin backend 的不同配置下运行最小数据库操作，验证 security-audit.jsonl 中有预期的审计条目。",
            ],
            "taskmaster_exported": False,
        }
    )

    add_task(
        {
            "id": "NG-0043",
            "story_id": "PH06-GAMELOOP-DB-ERROR-AND-AUDIT",
            "title": "GodotSQLiteDatabase 错误脱敏与审计挂钩",
            "description": (
                "在 Game.Godot/Adapters/Db/GodotSQLiteDatabase 中按 ADR-0019 要求改造错误处理："
                "开发环境保留详细异常信息方便排查；生产环境仅抛出脱敏后的通用错误消息，"
                "并将包含路径/SQL 明细的诊断信息写入安全审计日志或内部日志文件；"
                "同时为 OpenAsync/Execute* 等路径补充最小测试或 smoke 验证。"
            ),
            "status": "pending",
            "priority": "P3",
            "layer": "adapter",
            "depends_on": ["NG-0042"],
            "adr_refs": [
                "ADR-0019",
                "ADR-0006",
                "ADR-0005",
                "ADR-0011",
            ],
            "chapter_refs": [
                "CH02",
                "CH05",
                "CH07",
                "CH10",
            ],
            "overlay_refs": [],
            "labels": [
                "security",
                "sqlite",
                "godot",
                "error-handling",
            ],
            "owner": "architecture",
            "test_refs": [
                "Game.Godot/Adapters/Db/GodotSQLiteDatabase.cs",
                "logs/ci/<date>/security-audit.jsonl",
            ],
            "acceptance": [
                "GodotSQLiteDatabase 在 DEBUG 构建下仍然抛出包含路径/SQL 的详细异常，方便开发调试。",
                "在 Release/CI 环境下，同一代码路径仅向调用方暴露脱敏后的通用错误消息，详细路径与 SQL 片段被写入受控的审计/诊断日志。",
                "新增或扩展现有 smoke/集成测试，验证在数据库打开失败、SQL 执行失败等典型失败场景下，错误信息不会直接泄露敏感实现细节。",
            ],
            "test_strategy": [
                "环境对比测试：在 DEBUG 与 Release 配置下分别触发数据库异常，验证对调用方的异常消息差异以及 security-audit.jsonl 中的记录行为。",
                "CI 验证：在 windows-quality-gate 或等价工作流中加入一次最小化的 GodotSQLiteDatabase smoke 调用，确保改造后不会破坏现有管线。",
            ],
            "taskmaster_exported": False,
        }
    )

    # Stage 5: Outer quality gates for GameLoop (contracts / checks)
    add_task(
        {
            "id": "NG-0044",
            "story_id": "PH13-GAMELOOP-OUTER-QUALITY-GATES",
            "title": "GameLoop 外圈质量门禁脚本与 Taskmaster 集成",
            "description": (
                "围绕 GameLoop 与 SaveId/契约，补充一组外圈质量门禁脚本："
                "例如 check_gameloop_contracts.py 用于校验 GameLoop 相关契约/事件是否与 Overlay 08 与 GuildContractsTests 同步；"
                "在 quality_gates.py 或独立脚本中汇总 GameLoop 安全/契约/测试结果，输出 logs/ci/<date>/gameloop-quality-guard.json，"
                "供 Taskmaster MCP 与人工 review 使用。"
            ),
            "status": "pending",
            "priority": "P3",
            "layer": "ci",
            "depends_on": [
                "NG-0040",
                "NG-0041",
            ],
            "adr_refs": [
                "ADR-0015",
                "ADR-0005",
                "ADR-0019",
                "ADR-0018",
            ],
            "chapter_refs": [
                "CH01",
                "CH02",
                "CH06",
                "CH07",
                "CH09",
            ],
            "overlay_refs": [],
            "labels": [
                "quality-gates",
                "gameloop",
                "ci",
                "contracts",
            ],
            "owner": "architecture",
            "test_refs": [
                "scripts/python/check_gameloop_contracts.py",
                "logs/ci/<date>/gameloop-quality-guard.json",
            ],
            "acceptance": [
                "新增 scripts/python/check_gameloop_contracts.py 或等价脚本，能够扫描 GameLoop 相关契约/测试/Overlay 文档，并在发现不一致时给出结构化 JSON 报告。",
                "在 quality_gates.py 或独立 Python 脚本中聚合 SaveId 安全测试、GameLoop 合同测试与审计日志检查结果，输出 logs/ci/<date>/gameloop-quality-guard.json。",
                "Taskmaster 与 docs/workflows/task-master-superclaude-integration.md 中补充一小段说明：在进入 GameLoop 重大改动前建议先跑外圈质量门禁脚本，并将结果作为 PR review 的辅助参考，而非硬门禁。",
            ],
            "test_strategy": [
                "脚本级测试：在本地构造刻意破坏 GameLoop 契约/测试/Overlay 的分支，运行 check_gameloop_contracts.py 验证其能准确发现并报告问题。",
                "流程测试：在 CI 或本地通过 build_taskmaster_tasks.py + Taskmaster MCP 读取 gameloop-quality-guard.json，验证报告能被后续工作流消费。",
            ],
            "taskmaster_exported": False,
        }
    )

    if new_tasks:
        data.extend(new_tasks)
        TASKS_BACK_FILE.write_text(
            json.dumps(data, ensure_ascii=False, indent=2),
            encoding="utf-8",
        )
        print(f"Appended {len(new_tasks)} tasks to {TASKS_BACK_FILE}")
    else:
        print("No new tasks appended (all ids already present).")


if __name__ == "__main__":
    main()

