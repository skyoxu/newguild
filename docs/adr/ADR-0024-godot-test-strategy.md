# ADR-0024: Godot 测试策略（xUnit + GdUnit4）

- Status: Accepted
- Date: 2025-12-15
- Context: 本仓库是 Windows-only 的 Godot 4.5 + C# 模板，需要一套可在本机与 CI 复现的测试策略，用于支撑 TDD 与质量门禁，并将所有测试产物统一归档到 `logs/` 便于取证与排障。
- Decision:
  - 领域层（不依赖引擎）：使用 xUnit（可配合 FluentAssertions、NSubstitute）为 `Game.Core` 编写纯 C# 单元测试；覆盖率门禁按 SSoT 执行（lines >= 90%，branches >= 85%）。
  - 场景/节点层（依赖引擎）：使用 GdUnit4 编写集成测试，支持 headless 运行；产出 JUnit/XML 与 JSON 摘要，统一落盘到 `logs/e2e/`。
  - 统一工件目录：单元/集成/安全审计相关输出均落盘 `logs/`（目录规范见 `docs/testing-framework.md` 与仓库 Rulebook）。
  - 稳定 headless 环境：建议通过 `GODOT_USERDIR`/`GODOT_USER_DIR` 将 Godot `user://` 重定向到仓库内目录（例如 `logs/_godot_userdir/**`），避免污染用户目录与无限增长的 Godot 日志；该值必须是文件系统路径，禁止填写 `user://`/`res://`。
- Consequences:
  - `Game.Core` 逻辑不得依赖 Godot 类型；与引擎交互通过 Adapter/Ports 隔离，以便毫秒级测试循环与稳定覆盖率门禁。
  - GdUnit4 测试只覆盖 Scene/Node 生命周期、信号连通、资源路径与关键装配；领域算法仍应由 xUnit 覆盖。
  - CI 默认按“单元测试 -> Godot self-check -> GdUnit4 冒烟/安全/性能子集 -> 审计日志验证”顺序执行，并将报告作为工件上传归档。
- Supersedes: None
- References:
  - ADR-0005-quality-gates.md
  - ADR-0019-godot-security-baseline.md
  - ADR-0020-contract-location-standardization.md
  - docs/testing-framework.md
  - docs/migration/Phase-10-Unit-Tests.md
  - docs/migration/Phase-11-Scene-Integration-Tests.md
  - docs/migration/Phase-12-Headless-Smoke-Tests.md

## Verification（就地验收）

以下命令在 Windows PowerShell 下可执行；所有产物应落盘到 `logs/`。

1. 单元测试 + 覆盖率（领域层）
   - 命令：`py -3 scripts/python/run_dotnet.py --solution Game.sln --configuration Debug`
   - 产物：`logs/unit/<YYYY-MM-DD>/summary.json`、`logs/unit/<YYYY-MM-DD>/coverage.cobertura.xml`、`logs/unit/<YYYY-MM-DD>/tests.trx`

2. Godot 自检（装配/启动骨干）
   - 命令：`py -3 scripts/python/godot_selfcheck.py run --godot-bin "%GODOT_BIN%" --project project.godot --build-solutions`
   - 产物：`logs/e2e/<YYYY-MM-DD>/selfcheck-summary.json`（以及 `logs/ci/<YYYY-MM-DD>/selfcheck-stdout.txt` 等诊断输出）

3. GdUnit4 headless 集成测试（场景/节点层）
   - 命令：`py -3 scripts/python/run_gdunit.py --prewarm --godot-bin "%GODOT_BIN%" --project Tests.Godot --add tests/Adapters/Config --add tests/Security/Hard --timeout-sec 480`
   - 产物：`logs/e2e/<YYYY-MM-DD>/gdunit-reports/**`

4. 一键门禁（推荐）
   - 命令：`py -3 scripts/python/quality_gates.py all --godot-bin "%GODOT_BIN%" --build-solutions --gdunit-hard --smoke --validate-audit`
   - 产物：`logs/ci/<YYYY-MM-DD>/ci-pipeline-summary.json` 及各子门禁输出
