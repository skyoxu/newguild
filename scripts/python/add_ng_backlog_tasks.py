import json
from pathlib import Path


def _append_unique(tasks: list[dict], new_tasks: list[dict]) -> None:
    """Append new NG tasks if their id does not yet exist.

    This keeps the operation idempotent so the script can be re-run safely.
    """

    existing_ids = {t.get("id") for t in tasks}
    for task in new_tasks:
        if task.get("id") in existing_ids:
            continue
        tasks.append(task)


def update_tasks_back(tasks_back_path: Path) -> None:
    data = json.loads(tasks_back_path.read_text(encoding="utf-8"))

    new_short_and_mid: list[dict] = [
        {
            "id": "NG-0023",
            "story_id": "PH9-BACKLOG-B1",
            "title": "事件命名统一迁移（game.* → core.*.*）",
            "description": (
                "根据 Phase-9-Signal-Backlog.md B1，将仍然存在于测试中的旧事件类型"
                "（如 game.started/score.changed/player.health.changed）统一迁移为"
                " core.*.* 命名约定，并更新相关测试与文档，避免旧前缀继续被误用。"
            ),
            "status": "pending",
            "priority": "P1",
            "layer": "core",
            "depends_on": ["NG-0001"],
            "adr_refs": ["ADR-0004", "ADR-0006", "ADR-0018", "ADR-0023"],
            "chapter_refs": ["CH01", "CH04", "CH05", "CH06", "CH07"],
            "overlay_refs": [
                "docs/architecture/overlays/PRD-Guild-Manager/08/08-Contracts-Guild-Manager-Events.md",
                "docs/architecture/overlays/PRD-Guild-Manager/08/08-功能纵切-公会管理器.md",
                "docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md",
            ],
            "labels": ["signal", "events", "naming", "core"],
            "owner": "architecture",
            "test_refs": [
                "Game.Core.Tests/Engine/GameEngineCoreEventTests.cs",
                "Tests.Godot/tests/Scenes/test_main_scene_signals.gd",
            ],
            "acceptance": [
                "Game.Core 与 Tests 中不再存在 game.started/score.changed/"
                "player.health.changed 等旧事件类型，全部迁移为 core.*.* 命名。",
                "GameEngineCoreEventTests 覆盖新的事件类型常量，并全部通过。",
                "Overlay 08-Contracts-Guild-Manager-Events.md 与 08-功能纵切-公会管理器.md"
                " 中的事件示例与实际事件类型保持一致。",
                "一次 signal 自检（例如全文搜索）可以证明仓库内仅保留 core.*.* 作为事件命名前缀。",
            ],
            "test_strategy": [
                "本地：运行 GameEngineCoreEventTests，并通过全文搜索核对事件类型常量。",
                "CI：在质量门禁中增加针对 core.*.* 事件类型前缀的简单字符串检查脚本。",
            ],
        },
        {
            "id": "NG-0024",
            "story_id": "PH16-BACKLOG-B2",
            "title": "Game.Core Observability 客户端与结构化日志",
            "description": (
                "根据 Phase-16-Observability-Backlog.md B2，在 Game.Core 层实现可复用的"
                " ObservabilityClient/StructuredLogger/PiiDataScrubber，使核心逻辑能够"
                "在不直接依赖 Godot 或 Sentry SDK 的前提下输出结构化日志并上报事件。"
            ),
            "status": "pending",
            "priority": "P2",
            "layer": "core",
            "depends_on": ["NG-0016"],
            "adr_refs": ["ADR-0003", "ADR-0005", "ADR-0015", "ADR-0018"],
            "chapter_refs": ["CH03", "CH09"],
            "overlay_refs": [],
            "labels": ["observability", "logging", "core", "sentry"],
            "owner": "architecture",
            "test_refs": [
                "Game.Core/Observability/ObservabilityClient.cs",
                "Game.Core.Tests/Observability/ObservabilityClientTests.cs",
            ],
            "acceptance": [
                "Game.Core/Observability 下存在 ObservabilityClient/StructuredLogger/"
                "PiiDataScrubber 等类型，且不引用任何 Godot API。",
                "核心服务通过依赖注入使用 ILogger/ObservabilityClient 接口，而不是直接"
                "依赖 Sentry 或具体实现。",
                "ObservabilityClient 能够生成包含 logger、level、tags、extra、breadcrumbs 的"
                "结构化日志对象，并在单元测试中可被验证。",
                "至少一组 xUnit 测试验证 PII 字段在过滤逻辑中被正确脱敏或丢弃。",
            ],
            "test_strategy": [
                "本地：为 ObservabilityClient 编写 xUnit 测试，使用 fake/spy 对象捕获日志"
                "与事件上报行为。",
                "CI：在 dotnet-unit 任务中确保 Observability 测试纳入覆盖率统计，并在"
                "质量门禁报告中展示相关用例。",
            ],
        },
        {
            "id": "NG-0025",
            "story_id": "PH16-BACKLOG-B4",
            "title": "隐私与合规文档（privacy-compliance.md）",
            "description": (
                "根据 Phase-16-Observability-Backlog.md B4，在 docs 目录下新增"
                "privacy-compliance.md，明确日志、审计、Sentry 事件中允许出现的字段、"
                "PII 分类与脱敏策略，以及数据保留与删除（Retention）约束。"
            ),
            "status": "pending",
            "priority": "P2",
            "layer": "docs",
            "depends_on": ["NG-0016"],
            "adr_refs": ["ADR-0003", "ADR-0019", "ADR-0018"],
            "chapter_refs": ["CH02", "CH03", "CH07"],
            "overlay_refs": [],
            "labels": ["docs", "privacy", "compliance", "observability", "security"],
            "owner": "architecture",
            "test_refs": [
                "docs/privacy-compliance.md",
                "logs/ci/<date>/task-links.json",
            ],
            "acceptance": [
                "docs/privacy-compliance.md 存在，并链接自 AGENTS.md 或 testing-framework.md"
                " 中的隐私与日志相关章节。",
                "文档中枚举至少一份字段级别的 PII 分类清单，并描述日志、Sentry 事件中的"
                "脱敏策略与禁止上报的字段类型。",
                "文档引用 ADR-0003 与 ADR-0019，说明与 Release Health、Godot 安全基线之间的关系。",
                "task-links-validate 或等价脚本能够校验 tasks_back.json 中引用"
                "privacy-compliance.md 的任务与文档之间的回链关系。",
            ],
            "test_strategy": [
                "文档审阅：由架构或安全角色人工检查 privacy-compliance.md 是否覆盖日志、"
                "Sentry 与审计 JSONL 的隐私约束。",
                "自动化：在 task-links-validate 中增加针对 privacy-compliance.md 的引用检查，"
                "保证后续任务不会绕过该文档。",
            ],
        },
        {
            "id": "NG-0026",
            "story_id": "PH16-BACKLOG-B5",
            "title": "可观测性与日志使用规范（logging-guidelines.md）",
            "description": (
                "根据 Phase-16-Observability-Backlog.md B5，在 docs 下新增 logging-guidelines.md，"
                "对 logger 命名空间、级别使用方式、日志路径结构（logs/ci、logs/e2e、"
                "user://logs）以及与 ADR-0003 的结构化日志模型进行统一约定。"
            ),
            "status": "pending",
            "priority": "P2",
            "layer": "docs",
            "depends_on": ["NG-0024"],
            "adr_refs": ["ADR-0003", "ADR-0005", "ADR-0018", "ADR-0023"],
            "chapter_refs": ["CH01", "CH03", "CH05", "CH06", "CH07"],
            "overlay_refs": [],
            "labels": ["docs", "logging", "observability", "guidelines"],
            "owner": "architecture",
            "test_refs": [
                "docs/logging-guidelines.md",
                "AGENTS.md",
            ],
            "acceptance": [
                "docs/logging-guidelines.md 存在，并在 AGENTS.md 中被引用为日志使用的"
                "唯一规范来源。",
                "文档对 logger 命名空间（如 game.core、ui.menu、db.access、security）、日志级别"
                "（debug/info/warning/error）以及 breadcrumbs 使用方式给出清晰建议。",
                "文档中的日志路径结构与 6.3 节（logs/** SSoT）保持一致，未引入新的散乱目录。",
                "后续新增的日志相关任务在 adr_refs 和 acceptance 中明确遵守 logging-guidelines.md。",
            ],
            "test_strategy": [
                "文档审阅：在评审 PR 时检查日志相关变更是否引用 logging-guidelines.md 并符合其约定。",
                "自动化：在 task-links-validate 或等价脚本中校验日志相关任务是否引用该文档。",
            ],
        },
        {
            "id": "NG-0027",
            "story_id": "PH13-BACKLOG-B3",
            "title": "代码重复率与圈复杂度门禁",
            "description": (
                "根据 Phase-13-Quality-Gates-Backlog.md B3，为 newguild 模板补齐代码重复率"
                "（Duplication%）与圈复杂度（Cyclomatic Complexity）质量门禁，将 jscpd、"
                "Roslyn 或 SonarQube 指标纳入 quality_gates.py 的统一判断。"
            ),
            "status": "pending",
            "priority": "P2",
            "layer": "ci",
            "depends_on": ["NG-0013"],
            "adr_refs": ["ADR-0005", "ADR-0018", "ADR-0024"],
            "chapter_refs": ["CH01", "CH06", "CH07"],
            "overlay_refs": [],
            "labels": ["ci", "quality-gates", "duplication", "complexity"],
            "owner": "architecture",
            "test_refs": [
                "logs/ci/<date>/quality-gates-duplication.json",
                "logs/ci/<date>/quality-gates-complexity.json",
            ],
            "acceptance": [
                "存在脚本或配置能够生成重复率与圈复杂度的机器可读报告，并写入"
                "logs/ci/<date>/quality-gates-duplication.json 与 quality-gates-complexity.json。",
                "quality_gates.py 能够读取上述报告，根据 Phase-13 设定的阈值（例如 Dup% ≤ 2%，"
                "最大圈复杂度 ≤ 10，平均圈复杂度 ≤ 5）给出 PASS/FAIL。",
                "在 CI 中至少有一条 Job 使用 quality_gates.py 作为门禁，并在失败时提供"
                "清晰的重复率与复杂度摘要。",
            ],
            "test_strategy": [
                "本地：构造一个故意高重复率、高复杂度的小示例，验证 quality_gates.py 能将其"
                "标记为 failed。",
                "CI：在 windows-quality-gate 工作流中增加对 duplication 与 complexity 报告的校验，"
                "并在 Step Summary 中展示关键指标。",
            ],
        },
        {
            "id": "NG-0028",
            "story_id": "PH13-BACKLOG-B4",
            "title": "性能 P95 与审计 JSONL 校验",
            "description": (
                "根据 Phase-13-Quality-Gates-Backlog.md B4，将性能 P95 阈值与 security-audit.jsonl"
                " 的结构校验纳入 quality_gates.py，实现对 logs/perf/summary.json 与"
                "logs/ci/<date>/security-audit.jsonl 的自动化检查。"
            ),
            "status": "pending",
            "priority": "P2",
            "layer": "ci",
            "depends_on": ["NG-0013", "NG-0014", "NG-0015"],
            "adr_refs": ["ADR-0015", "ADR-0005", "ADR-0019", "ADR-0018"],
            "chapter_refs": ["CH02", "CH07", "CH09"],
            "overlay_refs": [],
            "labels": ["ci", "performance", "audit", "quality-gates"],
            "owner": "architecture",
            "test_refs": [
                "logs/perf/<date>/summary.json",
                "logs/ci/<date>/security-audit.jsonl",
                "logs/ci/<date>/quality-gates-perf.json",
            ],
            "acceptance": [
                "新增 scripts/python/validate_perf.py 与 scripts/python/validate_audit_logs.py，"
                "能够检查 P95 是否在预算内，以及 security-audit.jsonl 每行为合法 JSON 且包含必需字段。",
                "quality_gates.py 支持在 all 模式下调用上述校验脚本，并在失败时返回非零退出码。",
                "CI 日志中可以看到 P95 与审计 JSONL 校验的摘要输出，并产生对应 JSON 摘要文件。",
            ],
            "test_strategy": [
                "单元：为 validate_perf 与 validate_audit_logs 编写测试，验证边界条件与错误路径。",
                "集成：在 CI 中对刻意构造的超阈值样例运行 quality_gates.py，验证能够正确失败"
                "并输出预期的 JSON 摘要。",
            ],
        },
        {
            "id": "NG-0029",
            "story_id": "PH14-BACKLOG-B5",
            "title": "Signal 健康度验证与安全相关测试门禁",
            "description": (
                "根据 Phase-14-Godot-Security-Backlog.md B5，在安全相关 Signal 上补齐 XML 注释"
                "与规范化命名，并通过 xUnit、GdUnit4 或专用脚本，将这些检查纳入 CI 中的信号"
                "健康度门禁。"
            ),
            "status": "pending",
            "priority": "P3",
            "layer": "adapter",
            "depends_on": ["NG-0014", "NG-0023"],
            "adr_refs": ["ADR-0019", "ADR-0004", "ADR-0005", "ADR-0018"],
            "chapter_refs": ["CH02", "CH04", "CH07"],
            "overlay_refs": [],
            "labels": ["security", "signal", "gdunit", "adapter", "testing"],
            "owner": "architecture",
            "test_refs": [
                "Tests.Godot/tests/Security/Hard/test_security_signals_health.gd",
                "logs/ci/<date>/signal-health-summary.json",
            ],
            "acceptance": [
                "与安全相关的关键 Signal（例如 SecurityHttpClient.RequestBlocked 等）具备完整的"
                "XML 注释与稳定的命名约定。",
                "存在至少一组 GdUnit4 测试或分析脚本，对这些 Signal 的注册与触发进行验证，并在"
                "失败时产生可读的 JSON 摘要。",
                "CI 中新增或扩展一个步骤，用于执行 Signal 健康度检查，并在日志中给出通过与失败"
                "的统计。",
            ],
            "test_strategy": [
                "GdUnit4：为安全相关 Signal 编写 Hard 套件，用于验证触发路径与 XML 注释是否齐全。",
                "CI：在 signal-compliance 相关工作流中集成健康度检查脚本，并观察在故意缺失"
                "注释或错误命名时是否会失败。",
            ],
        },
        {
            "id": "NG-0030",
            "story_id": "PH15-BACKLOG-B3",
            "title": "性能报告与历史追踪（reports/performance/**）",
            "description": (
                "根据 Phase-15-Performance-Budgets-Backlog.md B3，为性能测试结果生成 HTML 或"
                " JSON 报告与历史记录（如 performance-history.csv），支持人工分析与长期趋势跟踪。"
            ),
            "status": "pending",
            "priority": "P3",
            "layer": "ci",
            "depends_on": ["NG-0015", "NG-0028"],
            "adr_refs": ["ADR-0015", "ADR-0005", "ADR-0018"],
            "chapter_refs": ["CH01", "CH07", "CH09"],
            "overlay_refs": [],
            "labels": ["performance", "reports", "ci", "observability"],
            "owner": "architecture",
            "test_refs": [
                "logs/perf/<date>/summary.json",
                "reports/performance/current-run-report.json",
                "reports/performance/performance-history.csv",
            ],
            "acceptance": [
                "存在脚本可以从 logs/perf/<date>/summary.json 等原始数据生成当前运行报告"
                "与历史记录文件，写入 reports/performance 目录。",
                "CI 将 reports/performance 目录中的文件作为 artifact 上传，便于离线分析。",
                "至少有一个简单的趋势图或统计汇总（可以是 JSON 或 CSV），帮助判断性能回归。",
            ],
            "test_strategy": [
                "本地：构造多次性能采样结果，运行报告生成脚本，检查历史文件是否按预期"
                "累积数据。",
                "CI：在性能相关 Job 中执行报告生成步骤，并检查 artifact 中是否包含预期文件。",
            ],
        },
        {
            "id": "NG-0031",
            "story_id": "PH15-BACKLOG-B5",
            "title": "独立性能门禁 CI 工作流（performance-gates.yml）",
            "description": (
                "根据 Phase-15-Performance-Budgets-Backlog.md B5，新增专门的性能门禁 CI 工作流"
                "（例如 .github/workflows/performance-gates.yml），仅运行性能相关脚本与阈值校验，"
                "与常规 build/test 工作流解耦。"
            ),
            "status": "pending",
            "priority": "P3",
            "layer": "ci",
            "depends_on": ["NG-0015", "NG-0028", "NG-0030"],
            "adr_refs": ["ADR-0015", "ADR-0005", "ADR-0011", "ADR-0008", "ADR-0018"],
            "chapter_refs": ["CH01", "CH06", "CH07", "CH09", "CH10"],
            "overlay_refs": [],
            "labels": ["ci", "performance", "workflow", "quality-gates"],
            "owner": "architecture",
            "test_refs": [
                ".github/workflows/performance-gates.yml",
                "logs/ci/<date>/performance-gates-summary.json",
            ],
            "acceptance": [
                "仓库中存在独立的性能门禁工作流文件 performance-gates.yml，并在文档中说明"
                "触发条件与使用场景。",
                "性能工作流只运行性能采集与阈值校验相关脚本，不重复常规 build/test 步骤。",
                "performance-gates 工作流在阈值未通过时能明确标记失败原因，并输出 JSON 摘要"
                "到 logs/ci/<date>/performance-gates-summary.json。",
            ],
            "test_strategy": [
                "CI：针对测试分支手动触发 performance-gates 工作流，观察在性能超阈值和正常"
                "情况下的行为差异。",
                "文档：在 Phase-15 或 README 中记录如何启用或关闭性能门禁工作流。",
            ],
        },
        {
            "id": "NG-0032",
            "story_id": "PH17-BACKLOG-B4",
            "title": "代码签名与安全分发",
            "description": (
                "根据 Phase-17-Build-Backlog.md B4，在构建链路中引入可配置的代码签名步骤，"
                "使用 Windows SignTool 对 Game.exe（及安装包如存在）进行签名，并在构建与发布"
                "日志中记录签名状态。"
            ),
            "status": "pending",
            "priority": "P3",
            "layer": "ci",
            "depends_on": ["NG-0017"],
            "adr_refs": ["ADR-0008", "ADR-0011", "ADR-0019", "ADR-0005", "ADR-0018"],
            "chapter_refs": ["CH01", "CH02", "CH07", "CH10"],
            "overlay_refs": [],
            "labels": ["build", "signing", "security", "release", "windows"],
            "owner": "architecture",
            "test_refs": [
                "scripts/python/build_windows.py",
                "logs/ci/<date>/signing-summary.json",
                "build/Game.exe",
            ],
            "acceptance": [
                "build_windows.py 或等价构建脚本支持可选的代码签名步骤，通过环境变量配置"
                "证书路径和密码。",
                "当签名启用且证书有效时，生成的 Game.exe 已被 SignTool 签名，且 signing-summary.json"
                " 中记录成功状态；在未配置证书时构建仍可完成但不会尝试签名。",
                "README 或 Phase-17 文档中说明如何在本地与 CI 环境中开启代码签名以及注意事项。",
            ],
            "test_strategy": [
                "本地：在具备测试证书的环境下执行构建，验证 EXE 已被签名且系统属性中可见。",
                "CI：在 Windows 构建 Job 中对 signing-summary.json 做基础格式检查，确保脚本"
                "在有与无证书两种场景下都能稳定运行。",
            ],
        },
        {
            "id": "NG-0033",
            "story_id": "PH17-BACKLOG-B5",
            "title": "导出预设与多配置支持",
            "description": (
                "根据 Phase-17-Build-Backlog.md B5，在 Godot export_presets.cfg 中配置多个导出"
                "预设（Debug、Release、Demo 等），并在构建脚本中增加相应的导出参数支持。"
            ),
            "status": "pending",
            "priority": "P3",
            "layer": "adapter",
            "depends_on": ["NG-0017"],
            "adr_refs": ["ADR-0011", "ADR-0018", "ADR-0005"],
            "chapter_refs": ["CH01", "CH06", "CH07", "CH10"],
            "overlay_refs": [],
            "labels": ["godot", "export", "presets", "windows", "release"],
            "owner": "architecture",
            "test_refs": [
                "export_presets.cfg",
                "logs/ci/<date>/export/summary.json",
            ],
            "acceptance": [
                "export_presets.cfg 中至少定义 Debug 与 Release 两类 Windows Desktop 导出预设，"
                "并按需扩展 Demo 等配置。",
                "构建脚本可以根据参数选择不同的导出预设，生成对应的 EXE 与 PCK。",
                "CI 中至少有一条导出 Job 使用 Release 预设，并在导出 summary 中注明所用配置。",
            ],
            "test_strategy": [
                "本地：在 Godot 编辑器与 headless 模式下分别验证各导出预设可以成功生成可运行的 EXE。",
                "CI：在 export 相关工作流中记录所用预设名称，并检查导出 summary 中的配置字段。",
            ],
        },
    ]

    _append_unique(data, new_short_and_mid)

    tasks_back_path.write_text(
        json.dumps(data, indent=2, ensure_ascii=False),
        encoding="utf-8",
    )


def write_longterm_tasks(longterm_path: Path) -> None:
    """Create or update long-term NG tasks file for optional backlog.

    This file mirrors the structure of tasks_back.json but focuses on
    lower-priority or long-horizon backlog (Phase-9/15 部分条目)。
    """

    longterm_tasks: list[dict] = [
        {
            "id": "NG-1001",
            "story_id": "PH9-BACKLOG-B2",
            "title": "Signal XML 文档注释补全",
            "description": (
                "根据 Phase-9-Signal-Backlog.md B2，为对外可见的关键 Signal 补齐 XML 注释"
                "（至少包含 summary 与 param 描述），优先覆盖 EventBusAdapter 与核心 UI/Glue 信号。"
            ),
            "status": "pending",
            "priority": "P2",
            "layer": "adapter",
            "depends_on": ["NG-0023"],
            "adr_refs": ["ADR-0004", "ADR-0019", "ADR-0018"],
            "chapter_refs": ["CH02", "CH04"],
            "overlay_refs": [],
            "labels": ["signal", "xml-doc", "adapter", "docs"],
            "owner": "architecture",
            "test_refs": [
                "Game.Godot/Adapters/EventBusAdapter.cs",
                "Tests.Godot/tests/Signals/test_signal_xml_docs.gd",
            ],
            "acceptance": [
                "关键对外 Signal（特别是 EventBusAdapter 与主 UI/Glue 层）具有完整的 XML 注释风格说明。",
                "存在简单的脚本或测试可以扫描缺失注释的 Signal，并在报告中列出。",
            ],
            "test_strategy": [
                "工具：编写简单的 C# 或 Python 脚本扫描 Godot 适配层中的 Signal XML 注释覆盖率。",
            ],
        },
        {
            "id": "NG-1002",
            "story_id": "PH9-BACKLOG-B3",
            "title": "Signal 性能基准测试（Signal Performance Benchmark）",
            "description": (
                "根据 Phase-9-Signal-Backlog.md B3，为 Signal 系统提供一组可运行的性能基准测试，"
                "用于说明在高负载下的吞吐能力，并输出到 logs/perf。"
            ),
            "status": "pending",
            "priority": "P2",
            "layer": "adapter",
            "depends_on": ["NG-0023"],
            "adr_refs": ["ADR-0015", "ADR-0004", "ADR-0018"],
            "chapter_refs": ["CH04", "CH09"],
            "overlay_refs": [],
            "labels": ["signal", "performance", "benchmark"],
            "owner": "architecture",
            "test_refs": [
                "Tests.Godot/tests/Performance/SignalPerformanceTest.cs",
                "logs/perf/<date>/signal-benchmark-summary.json",
            ],
            "acceptance": [
                "存在可运行的 Signal 性能基准测试场景，能够在本地或 CI 中执行。",
                "基准执行结果写入 logs/perf，并至少包含 P50 与 P95 统计。",
            ],
            "test_strategy": [
                "本地：在开发机上运行 Signal 基准测试，观察在不同订阅数与事件量下的性能曲线。",
            ],
        },
        {
            "id": "NG-1003",
            "story_id": "PH9-BACKLOG-B4",
            "title": "CI Signal 合规检查工作流（Signal Compliance Workflow）",
            "description": (
                "根据 Phase-9-Signal-Backlog.md B4，设计并实现 signal-compliance CI 工作流，"
                "对 Signal 命名约定与 XML 注释进行自动化检查。"
            ),
            "status": "pending",
            "priority": "P2",
            "layer": "ci",
            "depends_on": ["NG-0023", "NG-1001"],
            "adr_refs": ["ADR-0004", "ADR-0005", "ADR-0018"],
            "chapter_refs": ["CH04", "CH07"],
            "overlay_refs": [],
            "labels": ["ci", "signal", "compliance", "workflow"],
            "owner": "architecture",
            "test_refs": [
                ".github/workflows/signal-compliance.yml",
                "logs/ci/<date>/signal-compliance-summary.json",
            ],
            "acceptance": [
                "仓库中存在独立的 signal-compliance 工作流，能够在 PR 或定时任务中运行。",
                "当 Signal 命名或 XML 注释不符合约定时，工作流会给出清晰的失败原因。",
            ],
            "test_strategy": [
                "CI：构造刻意缺失注释或命名错误的分支，验证 signal-compliance 工作流能够失败并输出摘要。",
            ],
        },
        {
            "id": "NG-1004",
            "story_id": "PH9-BACKLOG-B5",
            "title": "GDScript 订阅生命周期管理",
            "description": (
                "根据 Phase-9-Signal-Backlog.md B5，将 Main.gd 中的订阅与退订模式推广到其它订阅"
                " EventBus 的 GDScript 节点，系统性管理 connect 与 disconnect 生命周期。"
            ),
            "status": "pending",
            "priority": "P3",
            "layer": "adapter",
            "depends_on": ["NG-0023"],
            "adr_refs": ["ADR-0004", "ADR-0019", "ADR-0018"],
            "chapter_refs": ["CH02", "CH04"],
            "overlay_refs": [],
            "labels": ["signal", "gdscript", "lifecycle"],
            "owner": "architecture",
            "test_refs": [
                "Game.Godot/Scripts/Main.gd",
                "Tests.Godot/tests/Signals/test_gdscript_subscription_lifecycle.gd",
            ],
            "acceptance": [
                "关键订阅 EventBus 的 GDScript 节点在 _ready 与 _exit_tree 中成对地 connect 与 disconnect。",
                "存在至少一组测试或脚本可以检测常见的订阅泄漏问题，并输出结果。",
            ],
            "test_strategy": [
                "GdUnit4：通过加载场景并反复 enter 与 exit tree 的方式检测是否出现重复订阅。",
            ],
        },
        {
            "id": "NG-1005",
            "story_id": "PH15-BACKLOG-B4",
            "title": "更细粒度的性能指标采集（GC/Signal/DB 等）",
            "description": (
                "根据 Phase-15-Performance-Budgets-Backlog.md B4，在现有性能采集基础上增加 GC 暂停、"
                "Signal 延迟、DB 查询延迟等更细粒度指标，并汇总到统一的性能报告中。"
            ),
            "status": "pending",
            "priority": "P3",
            "layer": "core",
            "depends_on": ["NG-0015", "NG-0030"],
            "adr_refs": ["ADR-0015", "ADR-0005", "ADR-0018"],
            "chapter_refs": ["CH01", "CH07", "CH09"],
            "overlay_refs": [],
            "labels": ["performance", "metrics", "core"],
            "owner": "architecture",
            "test_refs": [
                "Game.Core/Performance/PerformanceTracker.cs",
                "reports/performance/performance-history.csv",
            ],
            "acceptance": [
                "性能报告中新增 GC、Signal、DB 等关键指标，并在文档中解释采集方式与含义。",
                "新增指标不会引入显著的性能开销（可通过简单基准测试验证）。",
            ],
            "test_strategy": [
                "本地：对比开启与关闭细粒度采集时的基准结果，评估额外开销。",
            ],
        },
    ]

    if longterm_path.exists():
        existing = json.loads(longterm_path.read_text(encoding="utf-8"))
        _append_unique(existing, longterm_tasks)
        payload = existing
    else:
        payload = longterm_tasks

    longterm_path.write_text(
        json.dumps(payload, indent=2, ensure_ascii=False),
        encoding="utf-8",
    )


def main() -> None:
    root = Path(__file__).resolve().parents[2]
    tasks_back = root / ".taskmaster" / "tasks" / "tasks_back.json"
    longterm = root / ".taskmaster" / "tasks" / "tasks_longterm.json"

    if not tasks_back.exists():
        raise SystemExit(f"tasks_back.json not found: {tasks_back}")

    update_tasks_back(tasks_back)
    write_longterm_tasks(longterm)

    print("Updated tasks_back.json with new NG-0023..NG-0033 tasks.")
    print(f"Long-term backlog tasks written to: {longterm}")


if __name__ == "__main__":
    main()

