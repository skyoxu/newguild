---
title: 07 dev build and gates (Godot + C#) v2
status: base-SSoT
generated_variant: deep-optimized
ssot_scope: chapter-07-only
reuse_level: base-clean
adr_refs: [ADR-0002, ADR-0003, ADR-0005, ADR-0007, ADR-0011, ADR-0015, ADR-0018]
last_updated: 2026-01-22
---

> 目标：本章给出 Windows-only 的 Godot 4.5 + C#（.NET 8）项目在本地与 CI 中的**可执行质量门禁**与**可追溯工件**口径（ADR-0005/ADR-0011），并对齐安全与发布健康的基线（ADR-0002/ADR-0003）。

## 0.1 工具链与职责边界（概览）

本项目是 Windows-only（ADR-0011），核心门禁通过 Python 驱动并在 CI 固化（ADR-0005）。工具链分工如下：

- Godot：运行与 headless 冒烟（含 GdUnit4 场景测试）
- .NET 8：编译与 xUnit 单测（领域层）
- Python（`py -3`）：门禁聚合、静态扫描、日志工件归档（统一写入 `logs/**`）
- GitHub Actions：Windows Runner 上执行 `windows-quality-gate` 等工作流

## 0.2 门禁执行流程（本地/CI 一致）

原则：本地与 CI 使用同一组脚本/参数，输出统一落盘 `logs/**`，便于取证与归档（目录约定见 `AGENTS.md` 的“6.3 日志与工件（SSoT）”）。

```mermaid
flowchart TD
  A[Developer] --> B[Git push / PR]
  B --> C[GitHub Actions: windows-quality-gate]
  C --> D[dotnet build (warnaserror)]
  D --> E[Unit tests + Coverage gate]
  E --> F[Dependency Guard (hard gate)]
  F --> G[Links/Overlay/Contracts validation]
  G --> H[Godot headless smoke/security]
  H --> I[Perf smoke (DB) + validate_perf]
  I --> J[Release health (optional)]
  C --> K[Upload logs/** artifacts]
```

## A) 质量门禁矩阵（最小可执行）

| Gate | 工具/脚本（SSoT） | 策略/口径 | 失败动作 | 主要工件（示例） |
| --- | --- | --- | --- | --- |
| Build | `dotnet build -warnaserror`（由 `scripts/sc/acceptance_check.py` 与 CI 调用） | 编译/告警即错误（ADR-0005） | fail | `logs/ci/<date>/sc-acceptance-check/dotnet-build-warnaserror.log` |
| Unit + Coverage | `scripts/sc/test.py --type unit` | 覆盖率门禁口径见 `AGENTS.md` 6.2 | fail | `logs/unit/<date>/summary.json` |
| Dependency Guard | `scripts/python/dependency_guard.py` | 依赖矩阵口径见本章 7.y | fail（硬门禁） | `logs/ci/<date>/dependency-guard.json`、`dependency-guard-summary.txt` |
| Links/Overlay | `scripts/python/task_links_validate.py` / `validate_task_overlays.py` | Base/Overlay 回链一致性 | fail | `logs/ci/<date>/sc-acceptance-check/task-links-validate.log` |
| Contracts | `scripts/python/validate_contracts.py` | 契约可编译 + 回链一致 | fail | `logs/ci/<date>/sc-acceptance-check/validate-contracts.log` |
| Security (soft) | `scripts/python/security_soft_scan.py` 等 | 防御性扫描（ADR-0002） | 默认不阻断（可提升） | `logs/ci/<date>/sc-acceptance-check/security-soft-scan.json` |
| Godot Headless | `scripts/sc/test.py --type all` 或 `scripts/python/*gdunit*` | 场景冒烟/集成 | fail/soft（按工作流配置） | `logs/e2e/**` |
| Perf (DB) | `scripts/python/perf_smoke_db.py` + `validate_perf.py` | 性能预算口径见 ADR-0015 | fail（在 `windows-quality-gate`） | `logs/perf/<date>/db/db-perf-summary.json` |
| Release Health | `scripts/python/release_health_gate.py` | 发布健康口径见 ADR-0003 | fail（非 PR 时） | `logs/ci/<date>/release-health.json` |

## B) Windows 本地运行（建议命令集）

> 说明：以下命令以 PowerShell 为例；Python 使用 `py -3`；Godot 路径通过环境变量 `GODOT_BIN` 传入。

```bash
# 1) 任务级统一评审入口（串联 test + acceptance_check + llm_review）
py -3 scripts/sc/run_review_pipeline.py --task-id 43 --security-profile host-safe

# 2) TDD 绿灯阶段（含覆盖率门禁与产物汇总）
py -3 scripts/sc/build.py tdd --task-id 43 --stage green

# 3) 依赖护栏（本地可先跑；CI 会强制）
py -3 scripts/python/dependency_guard.py

# 4) 全量管线（本地复现 CI 的聚合跑法）
py -3 scripts/python/ci_pipeline.py all --solution Game.sln --configuration Debug --godot-bin "%GODOT_BIN%" --build-solutions
```

## 7.x 性能门禁（Godot Headless / DB）

本项目性能门禁预算由 ADR-0015 定义；CI 中的“DB perf smoke”会生成 `logs/perf/<YYYY-MM-DD>/db/db-perf-summary.json`（或 `logs/perf/<YYYY-MM-DD>/summary.json`），随后用 `scripts/python/validate_perf.py` 校验并产出报告到 `logs/ci/<YYYY-MM-DD>/`。

## G) 合并前验收清单（最小）

- [ ] `py -3 scripts/sc/run_review_pipeline.py --task-id <id> --security-profile host-safe` 通过（统一证据落盘 `logs/ci/<date>/sc-review-pipeline/`）
- [ ] `py -3 scripts/python/dependency_guard.py` 通过（硬门禁；工件落盘 `logs/ci/<date>/dependency-guard.*`）
- [ ] `py -3 scripts/sc/build.py tdd --task-id <id> --stage green` 通过且覆盖率门禁满足 `AGENTS.md` 6.2
- [ ] `py -3 scripts/sc/build.py tdd --task-id <id> --stage refactor` 通过（若本次变更涉及 Godot 则同时验证 headless 冒烟）
- [ ] 性能/安全/发布健康相关变更：引用并遵循对应 ADR（ADR-0015/ADR-0002/ADR-0003）

## 7.y 架构依赖矩阵与依赖护栏（Dependency Guard）

本小节是“架构依赖”的 SSoT：用于防止依赖方向漂移、跨层耦合回流，确保可测试架构长期可持续（ADR-0007）并可被门禁自动化校验（ADR-0005）。

### 7.y.1 项目级依赖矩阵（csproj / assembly）

> 说明：本项目的 Godot 层 csproj 文件名为 `GodotGame.csproj`，概念上对应“Game.Godot（运行时/适配层）”。下表按“概念层”描述依赖方向。

| From（源） | Allowed（允许引用） | Forbidden（禁止引用） | 备注 |
| --- | --- | --- | --- |
| Game.Core | BCL/.NET 标准库 | Godot / GodotSharp / GodotGame / Tests.Godot / Game.Core.Tests | Core 必须保持纯 C#（零 Godot 依赖） |
| Game.Godot（GodotGame.csproj） | Game.Core + BCL + Godot | Tests.* | 运行时可以依赖 Core，但不能依赖测试 |
| Tests（Game.Core.Tests / Tests.Godot） | 对应生产项目 | （无生产反向依赖） | 测试可依赖生产，生产不得依赖测试 |

### 7.y.2 目录级依赖矩阵（源码分层）

| From（源目录/层） | Allowed（允许依赖） | Forbidden（禁止依赖） | 备注 |
| --- | --- | --- | --- |
| `Game.Core/**` | 仅 BCL/.NET 标准库 | `Godot.*` 命名空间与 Godot 相关 SDK/程序集 | 单元测试必须可在无 Godot 环境运行 |
| `Scripts/Core/**` | 仅 BCL/.NET 标准库 | `Godot.*` 命名空间 | 领域/核心逻辑层（仅 .NET） |
| `Scripts/Adapters/**` | `Godot.*` + `Game.Core/**` | （按需收敛） | 适配层允许触达 Godot API，负责端口注入 |
| `Scenes/**` | 资源引用 + 绑定脚本 | （禁止业务逻辑堆积） | 场景应保持装配/路由/信号胶水 |

### 7.y.3 自动化依赖护栏（硬门禁）

- 脚本：`scripts/python/dependency_guard.py`
- 触发：GitHub Actions `windows-quality-gate`（Pull Request 为硬门禁）
- 工件输出（可追溯）：`logs/ci/<YYYY-MM-DD>/dependency-guard.json` 与 `logs/ci/<YYYY-MM-DD>/dependency-guard-summary.txt`
- 行为：检测到违规依赖时退出码非 0，并在 Step Summary 输出违规列表

为 Godot 运行时提供最小可执行的帧时间 P95 门禁：通过 Autoload `PerformanceTracker` 输出 `[PERF] ... p95_ms=...`，CI 侧解析 `headless.log` 并对比预算阈值。

- 前置：`Game.Godot/Scripts/Perf/PerformanceTracker.cs` 已启用（Autoload，默认按窗口采样并周期性输出 `[PERF]` 标记）。
- 运行与产物（Windows）：
  - 生成 headless 日志：`pwsh -File scripts/ci/smoke_headless.ps1 -GodotBin "$env:GODOT_BIN" -Scene "res://Game.Godot/Scenes/Main.tscn" -TimeoutSec 5`
  - 门禁判定（直接脚本）：`pwsh -File scripts/ci/check_perf_budget.ps1 -MaxP95Ms <ms>`
  - 门禁判定（质量门禁入口）：`pwsh -File scripts/ci/quality_gate.ps1 -GodotBin "$env:GODOT_BIN" -PerfP95Ms <ms>`
- 说明：
  - `check_perf_budget.ps1` 自动寻找 `logs/ci/**/smoke/headless.log` 的最新一份，并使用最后一次 `[PERF]` 刷新的 `p95_ms` 做比较。
  - 阈值口径与环境策略以 `docs/adr/ADR-0015-performance-budgets-and-gates.md` 为准（本节不重复阈值表）。
