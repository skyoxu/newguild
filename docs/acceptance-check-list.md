# /acceptance-check 完整检查清单

本文档用于把 `scripts/sc/acceptance_check.py` 的门禁行为变成“可执行的文字规范”，便于本地复跑与 CI 取证。

- 实现脚本：`scripts/sc/acceptance_check.py`
- 产物目录：`logs/ci/<YYYY-MM-DD>/sc-acceptance-check/`
- 适用范围：Windows only（Godot + C# 模板）

## 1. 命令与参数（Windows）

```powershell
# 默认：跑全量（含 security soft、tests hard、perf 取决于参数）
py -3 scripts/sc/acceptance_check.py --task-id 10 --godot-bin "$env:GODOT_BIN"

# CI 推荐：只跑硬门禁子集（避免重复跑 tests/perf）
py -3 scripts/sc/acceptance_check.py --task-id 10 --only adr,links,overlay,contracts,arch,build

# 启用 perf hard gate（从 logs/ci/**/headless.log 抽取 P95）
py -3 scripts/sc/acceptance_check.py --task-id 10 --only perf --perf-p95-ms 20
```

参数说明（摘要）：

- `--task-id`：Taskmaster id（例如 `10` 或 `10.3`），默认取 `tasks.json` 中第一个 `in-progress`。
- `--godot-bin`：Godot mono console 路径（或环境变量 `GODOT_BIN`）。
- `--only`：逗号分隔的步骤过滤器：`adr,links,overlay,contracts,arch,build,security,tests,perf`。
- `--strict-adr-status`：若引用 ADR 非 Accepted 则直接失败（hard）。
- `--perf-p95-ms`：启用 perf hard gate，`0` 表示禁用。

退出码：

- `0`：所有 hard gate 通过
- `1`：至少一个 hard gate 失败
- `2`：参数/前置条件缺失导致无法运行

## 2. 产物（SSoT：logs/）

所有产物写入 `logs/ci/<YYYY-MM-DD>/sc-acceptance-check/`：

- `summary.json`：结构化汇总（机器可读）
- `report.md`：人工摘要（人类可读）
- `adr-compliance.json`：ADR 合规检查
- `task-links-validate.log`：任务回链校验日志
- `validate-task-overlays.log`：overlay 校验日志
- `overlay-validate.json`：overlay 校验结构化结果（包含 test-refs 校验结果）
- `validate-contracts.log`：契约校验日志
- `architecture-boundary.json`：架构边界校验结果
- `dotnet-build-warnaserror.log`：`dotnet build -warnaserror` 日志
- `security-soft.json`：安全软扫描结果（不阻断）
- `check-encoding-since-today.log`：编码检查日志（不阻断）
- `tests-all.log`：测试执行日志（当 `tests` 步骤启用）
- `perf-budget.json`：性能预算（当 `perf` 步骤启用）

## 3. 步骤清单（Hard/Soft）

### 3.1 `adr`（Hard）

- [ ] Task 必须有 `adrRefs` 且至少包含 1 个 `Accepted` ADR
- [ ] 每个 ADR 文件必须存在于 `docs/adr/`
- [ ] 记录输出：`adr-compliance.json`

### 3.2 `links`（Hard）

- [ ] 任务回链校验通过（Taskmaster/ADR/CH/Overlay 口径一致）
- [ ] 记录输出：`task-links-validate.log`

### 3.3 `overlay`（Hard）

- [ ] `validate_task_overlays.py` 通过
- [ ] 若当前任务存在 `overlay` 路径：同时校验 overlay 文档中的 `Test-Refs` 指向的文件是否存在（见 `validate_overlay_test_refs.py`）
- [ ] 记录输出：`validate-task-overlays.log`、`overlay-validate.json`

### 3.4 `contracts`（Hard）

- [ ] `validate_contracts.py` 通过（契约落盘路径/命名/一致性）
- [ ] 记录输出：`validate-contracts.log`

### 3.5 `arch`（Hard）

- [ ] `Game.Core/**` 不得引用 Godot API（`using Godot` / `Godot.`）
- [ ] `Game.Core.csproj` 不得引用 Godot 相关包或上层项目
- [ ] 记录输出：`architecture-boundary.json`

### 3.6 `build`（Hard）

- [ ] `dotnet build -warnaserror` 通过（对应脚本内实现）
- [ ] 记录输出：`dotnet-build-warnaserror.log`

### 3.7 `security`（Soft）

软门禁（不阻断合并，仅用于止损/提示）：

- [ ] Sentry secrets 检查（`check_sentry_secrets.py`）
- [ ] GameLoop/Sanguo contracts 检查（`check_sanguo_gameloop_contracts.py`）
- [ ] 安全模式关键 API 软扫描（`security_soft_scan.py`）
- [ ] 编码检查（`check_encoding.py --since-today`）

### 3.8 `tests`（Hard）

- [ ] 根据项目约定运行单测/引擎测试（需要 `--godot-bin` 或 `GODOT_BIN`）
- [ ] 记录输出：`tests-all.log`

### 3.9 `perf`（Hard，当启用时）

- [ ] 从最近一次 `logs/ci/**/headless.log` 读取 `[PERF] p95_ms` 并与阈值比较
- [ ] 输出：`perf-budget.json`

