---
PRD-ID: PRD-Guild-Manager
Title: PRD-Guild-Manager 功能纵切实现验收清单（Godot + C# 变体）
Status: Template
ADR-Refs:
  - ADR-0018  # Godot 4.5 + C# 技术栈
  - ADR-0019  # Godot 安全基线
  - ADR-0004  # 事件总线与契约
  - ADR-0005  # 质量门禁
  - ADR-0015  # 性能预算
Test-Refs:
  # 具体项目应将占位路径替换为真实测试文件
  - Game.Core.Tests/Domain/GuildCoreTests.cs
  - Game.Core.Tests/Domain/EventEngineTests.cs
  - Game.Core.Tests/Domain/GameTurnSystemTests.cs
  - Tests.Godot/tests/Scenes/test_guild_main_scene.gd
  - Tests.Godot/tests/Scenes/test_main_scene_smoke.gd
  - Tests.Godot/tests/Integration/test_guild_workflow.gd
  - logs/ci/<date>/ci-pipeline-summary.json
  - logs/e2e/<date>/smoke/selfcheck-summary.json
---

> 说明：本清单用于 **newguild（Godot 4.5 + C# 模板）** 下的 “PRD-Guild-Manager 公会管理器” 功能纵切验收骨架。
> 旧版 Electron + React + TypeScript 的完整验收内容请参考迁移文档（docs/migration/**）与原 Electron 仓库，本文件不再作为 Electron 版本的 SSoT。

本清单只做 **结构与对齐检查**，所有阈值/策略/门禁的具体口径一律引用：

- Base 文档：docs/architecture/base/01–03 章
- ADR：ADR‑0018（Godot 技术栈）、ADR‑0019（Godot 安全基线）、ADR‑0004（事件与契约）、ADR‑0005（质量门禁）、ADR‑0015（性能预算）

不在本清单中重复具体数值或策略，避免与 Base/ADR 口径漂移。

---

## 一、文档完整性验收

- [ ] 功能纵切文档存在且 Front‑Matter 完整：
  - `docs/architecture/overlays/PRD-Guild-Manager/08/08-功能纵切-公会管理器.md`
  - `_index.md` 中已收录公会管理器相关条目
- [ ] 08 章仅作“引用”，不复制 01/02/03 章中的阈值/策略/门禁具体数值：
  - 安全：引用 CH02 + ADR‑0019
  - 可观测性与发布健康：引用 CH03 + ADR‑0003/0005/0015
  - 性能预算：引用 CH09 + ADR‑0015
- [ ] PRD 与 Overlay 对齐：
  - `docs/prd.txt` 中的公会管理核心模块在 08 章有对应小节或引用
  - Overlay 中引用的 Contracts/Tests 均指向当前 Godot+C# 代码与测试路径

---

## 二、架构设计验收（Arc42/三层结构对齐）

- [ ] 三层结构落地：
  - Game.Core：纯 C# 域模型与服务（无 Godot 依赖）
  - Game.Godot：Godot 适配层与场景/脚本，仅通过接口依赖 Core
  - Tests.Godot：GdUnit4 场景与集成测试工程
- [ ] 事件与契约：
  - 领域事件与 UI 事件命名遵循 `${DOMAIN_PREFIX}.<entity>.<action>`（见 ADR‑0004）
  - Contracts SSoT 存在于 `Game.Core` 或专门的 Contracts 项目（不依赖 Godot）
- [ ] 数据与存储：
  - SQLite 访问通过适配层封装（SqliteDataStore 或等价组件），仅使用 `user://` 路径，符合 ADR‑0006/0019 要求
  - Settings SSoT 为 ConfigFile（`user://settings.cfg`，见 ADR‑0023），DB 不再承载设置 SSoT 职责

---

## 三、代码实现验收（Godot + C#）

### 3.1 Core 层（Game.Core）

- [ ] 存在公会核心域模型与服务：
  - Guild、GuildMember、Raid、Event 等核心类型
  - 回合/周循环（Resolution/Player/AI Simulation）的领域接口
- [ ] Event Engine / AI Coordinator 等核心逻辑均在 Game.Core 内实现：
  - 不直接依赖 Godot API 或场景树
  - 通过接口（Ports）向适配层暴露能力

### 3.2 适配层与场景（Game.Godot）

- [ ] Godot 场景结构与 08 章设计一致：
  - 主场景（Main 或等价节点）
  - HUD/菜单/公会管理面板等 UI 节点
- [ ] 适配层（Adapters）封装：
  - 事件总线与 Signals（EventBusAdapter 等）
  - SQLite/ConfigFile 安全访问（SqliteDataStore/SafeConfig 等）
  - FeatureFlags 与 PerfTracker（如适用）

---

## 四、测试框架验收（xUnit + GdUnit4）

- [ ] xUnit 单元测试覆盖核心域逻辑：
  - Game.Core.Tests 中存在 Guild/事件引擎/AI 等模块的测试
  - 覆盖率门禁参数与阈值由 ADR‑0005/0015 所引用的脚本负责，本清单只要求“存在且可运行”
- [ ] GdUnit4 场景/集成测试：
  - Tests.Godot 中有针对主场景、公会管理 UI、关键 Signals 的测试
  - 至少包含一条完整的“启动 → 主菜单 → 进入公会场景 → 简单操作 → 退出”冒烟用例
  - 至少包含一条覆盖 PRD 3.0.3 T2 可玩性场景流的端到端用例：从启动主场景进入首周公会管理界面，执行一次完整的 Resolution→Player→AI Simulation 一周循环，并安全返回主菜单或结束会话，对应的 xUnit 与 GdUnit4/headless 测试文件挂接在 NG-0021/GM-0103 的 Test-Refs 中
- [ ] Smoke/CI 流程：
  - `scripts/python/dev_cli.py run-ci-basic` 在当前仓库可成功运行
  - `ci-windows.yml` 与 `windows-quality-gate.yml` 已集成基础单元测试与 Smoke/GdUnit 流程

---

## 五、性能与监控验收（引用 ADR‑0015/0003）

- [ ] PerfTracker 与性能采集：
  - Godot 侧有性能采集组件（PerfTracker 或等价），在关键场景中输出性能数据到 `user://logs/perf/perf.json`
  - 性能预算与 P95 等指标的具体阈值不在本清单重复，只需确认采集管线按 ADR‑0015/CH09 设计存在
- [ ] 监控与日志：
  - Logger/ObservabilityClient（如已实现）能够针对关键事件/错误输出结构化日志
  - 日志与审计算法遵循 ADR‑0003/0019 的隐私与安全要求

---

## 六、CI / 发布与平台约束验收

- [ ] Windows-only CI 与构建：
  - Windows CI（ci-windows.yml）在 main 分支可整体通过
  - Windows Release (Manual/Tag) 工作流可导出并运行 Game.exe（不依赖安装 Godot）
- [ ] 质量门禁：
  - quality_gates.py/ci_pipeline.py 脚本存在且可运行，汇总 dotnet/selfcheck/编码/Smoke/GdUnit 等结果
  - 具体阈值由 ADR‑0005/0015/CH07/CH09 负责，本清单只检查“门禁存在且已集成到 CI”

---

## 七、最终验收状态（模板级）

- [ ] 架构对齐：
  - 三层结构、本地/CI 流程、安全/性能/可观测性均与 ADR/CH 口径一致
- [ ] 文档对齐：
  - PRD、Base、ADR、Overlay/08 之间有清晰回链
- [ ] 测试与门禁：
  - 最小 xUnit/GdUnit/Smoke 流程跑通
  - Windows CI/Release 工作流可用于派生项目

> 注：本清单是模板级 DoD 骨架，不强制具体游戏玩法完全实现，仅要求“当基于 newguild 开发公会管理器游戏时，有一条清晰、可执行的验收路线”，并确保所有跨切面约束来自 Base/ADR，而非散落在实现或文档中。
