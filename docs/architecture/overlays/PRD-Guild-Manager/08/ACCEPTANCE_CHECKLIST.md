---
PRD-ID: PRD-Guild-Manager
PRD-Refs:
  - docs/prd.txt
Title: PRD-Guild-Manager 功能纵切实现验收清单（Godot + C# 变体）
Status: Active
Arch-Refs:
  - CH01
  - CH03
ADR-Refs:
  - ADR-0018  # Godot 4.5 + C# 技术栈
  - ADR-0019  # Godot 安全基线
  - ADR-0004  # 事件总线与契约
  - ADR-0005  # 质量门禁
  - ADR-0003  # 可观测性与发布健康
  - ADR-0015  # 性能预算
  - ADR-0011  # Windows-only 平台策略
  - ADR-0020  # Contracts SSoT 位置标准化
  - ADR-0023  # Settings SSoT = ConfigFile（user://）
Test-Refs:
  - Tests.Godot/tests/Scenes/test_task46_acceptance.gd
  - Tests.Godot/tests/Scenes/test_task46_button_audit_acceptance.gd
  - Tests.Godot/tests/Scenes/test_task47_acceptance.gd
  - Game.Core.Tests/Services/DemoGatePolicyTests.cs
  - Game.Core.Tests/Tasks/Task47AcceptanceTests.cs
  # 当前仓库真实存在的测试文件（用于存在性校验）
  - Game.Core.Tests/Docs/Task22DocsLinksAcceptanceTests.cs
  - Game.Core.Tests/Domain/GuildCoreTests.cs
  - Game.Core.Tests/Domain/EventEngineTests.cs
  - Game.Core.Tests/Domain/GameTurnSystemTests.cs
  - Tests.Godot/tests/Scenes/test_main_scene_smoke.gd
  - Tests.Godot/tests/Scenes/Guild/T2PlayableSceneTests.gd
  - Tests.Godot/tests/Integration/test_guild_vertical_slice.gd
Artifact-Refs:
  # 产物/门禁锚点：允许占位；不参与“测试文件必须存在”的硬规则
  - logs/ci/<YYYY-MM-DD>/ci-pipeline-summary.json
  - logs/e2e/<YYYY-MM-DD>/smoke/selfcheck-summary.json
---

> 说明：本清单用于 **newguild（Godot 4.5 + C# 模板）** 下的 “PRD-Guild-Manager 公会管理器” 功能纵切验收骨架。
> 旧版 旧桌面壳 + 旧前端框架 + TypeScript 的完整验收内容请参考迁移文档（docs/migration/**）与原 旧桌面壳 仓库，本文件不再作为 旧桌面壳 版本的 SSoT。

本清单只做 **结构与对齐检查**，所有阈值/策略/门禁的具体口径一律引用：

- Base 文档：docs/architecture/base/01–03 章
- ADR：ADR‑0018（Godot 技术栈）、ADR‑0019（Godot 安全基线）、ADR‑0004（事件与契约）、ADR‑0005（质量门禁）、ADR‑0015（性能预算）

不在本清单中重复具体数值或策略，避免与 Base/ADR 口径漂移。

---

## Test-Refs（当前仓库真实存在）

> 说明：此处列出的路径应 **真实存在于仓库**（例如 `.cs`/`.gd`），用于门禁做“存在性”校验；不要把 `logs/**` 这类运行产物写进 Test-Refs。

- `Game.Core.Tests/Docs/Task22DocsLinksAcceptanceTests.cs`
- `Game.Core.Tests/Domain/GuildCoreTests.cs`
- `Game.Core.Tests/Domain/EventEngineTests.cs`
- `Game.Core.Tests/Domain/GameTurnSystemTests.cs`
- `Tests.Godot/tests/Scenes/test_main_scene_smoke.gd`
- `Tests.Godot/tests/Scenes/Guild/T2PlayableSceneTests.gd`
- `Tests.Godot/tests/Integration/test_guild_vertical_slice.gd`

- `Tests.Godot/tests/Scenes/test_task46_acceptance.gd`
- `Tests.Godot/tests/Scenes/test_task46_button_audit_acceptance.gd`
- `Game.Core.Tests/Services/DemoGatePolicyTests.cs`
## Artifact-Refs（允许占位，不参与存在性校验）

> 说明：产物/门禁锚点允许占位（例如 `<YYYY-MM-DD>`），用于帮助定位 CI 输出与取证归档；不参与“测试文件必须存在”的硬规则。

- `logs/ci/<YYYY-MM-DD>/ci-pipeline-summary.json`
- `logs/e2e/<YYYY-MM-DD>/smoke/selfcheck-summary.json`

## 一、文档完整性验收

- [ ] 功能纵切文档存在且 Front‑Matter 完整：
  - `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Guild-Manager.md`
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
  - Contracts SSoT 位于 `Game.Core/Contracts/**`（纯 C#，不依赖 Godot）
  - 示例契约文件：`Game.Core/Contracts/Guild/GuildMemberJoined.cs`（per ADR-0020）
  - 当前 T2 最小事件集合（已落盘）：GuildCreated / GuildMemberJoined / GuildMemberLeft 已在 Overlay 08 登记，并已落盘到 Game.Core/Contracts/Guild/GuildCreated.cs、Game.Core/Contracts/Guild/GuildMemberJoined.cs、Game.Core/Contracts/Guild/GuildMemberLeft.cs
- [ ] 事件命名规范验证（ADR-0004）：
  - 所有事件常量必须匹配正则：`^(core|security|ui|screen)\.[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$`
  - 验证命令（扫描所有 EventType 常量定义）：
    ```bash
    # Windows PowerShell
    Get-ChildItem -Recurse -Include *.cs Game.Core/Contracts |
    Select-String 'EventType\s*=\s*"([^"]+)"' |
    ForEach-Object {
      if ($_.Matches.Groups[1].Value -notmatch '^(core|security|ui|screen)\.[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$') {
        Write-Host "FAIL: Invalid event name: $($_.Matches.Groups[1].Value) in $($_.Path)"
        exit 1
      }
    }
    Write-Host "PASS: All event names valid"
    ```
  - 前缀一致性：领域事件必须以 `core.` 开头；安全/审计事件以 `security.` 开头；`ui.` / `screen.` 仅在明确作为跨层契约时使用
  - 禁止模式示例：
    - [FAIL] CamelCase：`Core.GuildCreated`
    - [FAIL] 混合分隔符：`core.guild-created`
    - [FAIL] 缺少前缀：`member.joined`
    - [PASS] 正确格式：`core.guild.created`、`core.guild.member.joined`
- [ ] 数据与存储：
  - SQLite 访问通过适配层封装（SqliteDataStore 或等价组件），仅使用 `user://` 路径，符合 ADR‑0006/0019 要求
  - Settings SSoT 为 ConfigFile（`user://settings.cfg`，见 ADR‑0023），DB 不再承载设置 SSoT 职责

### 2.3 安全基线验证（ADR-0019）

- [ ] 路径与网络安全检查：
  - 扫描脚本：`py -3 scripts/python/godot_selfcheck.py`
  - 代码禁用检查：`py -3 scripts/python/scan_code_disables.py`
  - 乱码检测：`py -3 scripts/python/scan_garbled.py`
  - 绝对路径检测（PowerShell）：`Get-ChildItem -Recurse Game.Core,Game.Godot -Include *.cs | Select-String -Pattern "[A-Za-z]:\\\\"`
  - HTTP 外链检测（PowerShell）：`Get-ChildItem -Recurse Game.Core,Game.Godot -Include *.cs | Select-String -Pattern "http://"`
- [ ] 配置开关验证：
  - GD_SECURE_MODE=1 已设置
  - ALLOWED_EXTERNAL_HOSTS 白名单已定义

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
  - 覆盖率门禁：`py -3 scripts/python/run_dotnet.py test --coverage`（阈值见 ADR-0005）
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
  - Godot 侧有性能采集组件（PerfTracker 或等价），在关键场景中输出性能数据
    - 开发环境：Godot 运行时输出到 `user://logs/perf/perf.json`
    - CI 环境：归档到项目相对路径 `logs/perf/<YYYY-MM-DD>/summary.json`
  - 性能预算与分位数等指标的具体阈值不在本清单重复，只需确认采集管线按 ADR‑0015/CH09 设计存在
- [ ] 监控与日志：
  - Logger/ObservabilityClient（如已实现）能够针对关键事件/错误输出结构化日志
  - 日志与审计算法遵循 ADR‑0003/0019 的隐私与安全要求

---

## 六、CI / 发布与平台约束验收

- [ ] Windows-only CI 与构建（ADR-0011）：
  - Windows CI（ci-windows.yml）在 main 分支可整体通过
  - Shell 策略验证：
    - 所有 Windows Job 使用 PowerShell（通过 `defaults.run.shell: pwsh` 或 step 级 `shell: pwsh`）
    - 工作流 lint 检查：`pwsh lint_workflows.ps1` 应通过（防止 bash/cmd 混入）
  - Windows Release (Manual/Tag) 工作流可导出并运行 Game.exe（不依赖安装 Godot）
- [ ] 质量门禁：
  - quality_gates.py/ci_pipeline.py 脚本存在且可运行，汇总 dotnet/selfcheck/编码/Smoke/GdUnit 等结果
  - 具体阈值由 ADR‑0005/0015/CH07/CH09 负责，本清单只检查"门禁存在且已集成到 CI"
- [ ] 分支保护策略验收（ADR-0011/0005）：
  - **main/master 分支保护规则**（Repository → Settings → Branches → Branch protection rules）：
    - [ ] 启用 "Require a pull request before merging"
      - [ ] Require approvals (至少 1 个审批)
    - [ ] 启用 "Require status checks to pass before merging"
      - [ ] Require branches to be up to date before merging
      - [ ] 必需状态检查清单（Required checks）：
        - `dotnet-typecheck-lint` - C# 类型检查与代码格式
        - `dotnet-unit` - 单元测试 + 覆盖率门禁（阈值见 ADR-0005）
        - `godot-e2e` - Godot headless 冒烟/安全/性能测试
        - `task-links-validate` - ADR/CH/Overlay 回链校验
        - `release-health` - Sentry 发布健康门禁（阈值见 ADR-0003）
    - [ ] 启用 "Do not allow bypassing the above settings"
    - [ ] 启用 "Restrict who can push to matching branches"（仅限 Admins）
  - **验证方法**（需要 repo admin 权限）：
    ```bash
    # GitHub CLI 验证分支保护
    gh api repos/:owner/:repo/branches/main/protection | ConvertFrom-Json | Select-Object `
      @{N='RequiredChecks';E={$_.required_status_checks.contexts}}, `
      @{N='RequireApprovals';E={$_.required_pull_request_reviews.required_approving_review_count}}, `
      @{N='EnforceAdmins';E={$_.enforce_admins.enabled}}
    ```
  - **发布工作流门禁**：
    - [ ] Manual/Tag 触发的 Release 工作流必须依赖所有 Required checks 通过
    - [ ] Release 分支（如 release/*）应用相同保护规则
    - [ ] 禁止直接 push 到受保护分支（force-push 永久禁用）

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

## Test-Refs

- T31: `Tests.Godot/tests/UI/test_ui_components_smoke.gd`
- T31: `Tests.Godot/tests/UI/test_guild_panel_scene.gd`
- T31: `Game.Core.Tests/CI/Task31UiComponentsAcceptanceTests.cs`
- T46: `Tests.Godot/tests/Scenes/test_task46_acceptance.gd`
- T46: `Tests.Godot/tests/Scenes/test_task46_button_audit_acceptance.gd`
- T46: `Game.Core.Tests/Services/DemoGatePolicyTests.cs`
- T47: `Tests.Godot/tests/Scenes/test_task47_acceptance.gd`
- T47: `Game.Core.Tests/Tasks/Task47AcceptanceTests.cs`

<!-- V11_ACCEPTANCE_START -->
## 八、V1.1 阶段验收增量（T53-T102）

- [ ] `T53-T102` 与视图任务映射一致：每个任务仅落在一个视图（治理到 back，玩法到 gameplay）
- [ ] 从 `T53` 起保持严格串行依赖（`T53 -> T52`，其余任务依赖前一任务）
- [ ] 所有阶段任务标题不再使用 `分解A/B/C` 或 `R2-A/B`，统一阶段命名
- [ ] `08-FeatureSlice-V11-Governance-Stabilization.md` 与 `08-FeatureSlice-V11-Gameplay-Depth.md` 已更新并可作为审计入口
- [ ] 阶段任务的 `ACC:T<id>.<n>` 在测试与工件中可回放（`logs/ci/**`、`logs/e2e/**`）
<!-- V11_ACCEPTANCE_END -->

<!-- BEGIN:T53-T102-MAP -->
## T53-T102 ????????????

> ???? `tasks.json` ? `tasks_back/tasks_gameplay` ???? overlay ????????????????????

| Task | Title | Status | Back IDs | Gameplay IDs | Overlay |
|---|---|---|---|---|---|
| T53 | V1.1 治理：稳定性门禁基线统一 | pending | NG-0053 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T54 | V1.1 治理：Headless Flaky 采集与根因分类 | pending | NG-0054 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T55 | V1.1 治理：测试编排与证据消费规范 | pending | NG-0055 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T56 | V1.1 治理：性能预算硬门与场景基准 | pending | NG-0056 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/_index.md` |
| T57 | V1.1 安全治理：Release Health 门禁固化 | pending | NG-0057 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T58 | V1.1 玩法：招募到入会闭环调参与反馈（阶段1） | pending | - | GM-0322 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Recruitment.md` |
| T59 | V1.1 玩法：Raid 结果驱动成长与排名联动（阶段1） | pending | - | GM-0323 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-PVE-Raid.md` |
| T60 | V1.1 玩法：两周循环与存档回放一致性（阶段1） | pending | - | GM-0324 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Core-Game-Loop.md` |
| T61 | V1.1 数据治理：schemaVersion 与迁移矩阵 | pending | NG-0058 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T62 | V1.1 数据治理：跨版本存档回放与损坏恢复（阶段1） | pending | NG-0059 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T63 | V1.1 治理：观测与审计日志字段统一 | pending | NG-0060 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/_index.md` |
| T64 | V1.1 治理：CI 诊断摘要与止损模板 | pending | NG-0061 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T65 | V1.1 治理：Task/ADR/Overlay/Test-Refs 自动体检 | pending | NG-0062 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/_index.md` |
| T66 | V1.1 收口：RC 准入评审包（阶段1） | pending | NG-0063 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T67 | V1.1 玩法深度：招募链路边界与异常分支（阶段1） | pending | - | GM-0325 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Recruitment.md` |
| T68 | V1.1 玩法深度：成员管理状态机与角色变更（阶段1） | pending | - | GM-0326 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Member-Management.md` |
| T69 | V1.1 玩法深度：Raid 进阶规则与结果可解释性（阶段1） | pending | - | GM-0327 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-PVE-Raid.md` |
| T70 | V1.1 玩法深度：经济与后勤联动闭环（阶段1） | pending | - | GM-0328 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Tactical-Rewards.md` |
| T71 | V1.1 玩法深度：媒体事件与声望反馈闭环（阶段1） | pending | - | GM-0329 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Media-Reputation.md` |
| T72 | V1.1 玩法扩展：WorldBoss 从入口到可玩闭环（阶段1） | pending | - | GM-0330 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Guild-Manager.md` |
| T73 | V1.1 玩法扩展：PVP 从入口到匹配结算（阶段1） | pending | - | GM-0331 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Social.md` |
| T74 | V1.1 平衡治理：玩法遥测与数值调参闭环（阶段1） | pending | - | GM-0332 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Core-Observability.md` |
| T75 | V1.1 收口：玩法全链路回归与发布前验收（阶段1） | pending | - | GM-0333 | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T76 | V1.1 玩法：招募到入会闭环调参与反馈（阶段2） | pending | - | GM-0334 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Recruitment.md` |
| T77 | V1.1 玩法：Raid 结果驱动成长与排名联动（阶段2） | pending | - | GM-0335 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-PVE-Raid.md` |
| T78 | V1.1 玩法：两周循环与存档回放一致性（阶段2） | pending | - | GM-0336 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Core-Game-Loop.md` |
| T79 | V1.1 数据治理：跨版本存档回放与损坏恢复（阶段2） | pending | NG-0064 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T80 | V1.1 收口：RC 准入评审包（阶段2） | pending | NG-0065 | - | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T81 | V1.1 玩法深度：招募链路边界与异常分支（阶段2） | pending | - | GM-0337 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Recruitment.md` |
| T82 | V1.1 玩法深度：成员管理状态机与角色变更（阶段2） | pending | - | GM-0338 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Member-Management.md` |
| T83 | V1.1 玩法深度：Raid 进阶规则与结果可解释性（阶段2） | pending | - | GM-0339 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-PVE-Raid.md` |
| T84 | V1.1 玩法深度：经济与后勤联动闭环（阶段2） | pending | - | GM-0340 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Tactical-Rewards.md` |
| T85 | V1.1 玩法深度：媒体事件与声望反馈闭环（阶段2） | pending | - | GM-0341 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Media-Reputation.md` |
| T86 | V1.1 玩法扩展：WorldBoss 从入口到可玩闭环（阶段2） | pending | - | GM-0342 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Guild-Manager.md` |
| T87 | V1.1 玩法扩展：WorldBoss 从入口到可玩闭环（阶段3） | pending | - | GM-0343 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Guild-Manager.md` |
| T88 | V1.1 玩法扩展：PVP 从入口到匹配结算（阶段2） | pending | - | GM-0344 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Social.md` |
| T89 | V1.1 玩法扩展：PVP 从入口到匹配结算（阶段3） | pending | - | GM-0345 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Social.md` |
| T90 | V1.1 平衡治理：玩法遥测与数值调参闭环（阶段2） | pending | - | GM-0346 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Core-Observability.md` |
| T91 | V1.1 平衡治理：玩法遥测与数值调参闭环（阶段3） | pending | - | GM-0347 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Core-Observability.md` |
| T92 | V1.1 收口：玩法全链路回归与发布前验收（阶段2） | pending | - | GM-0348 | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T93 | V1.1 收口：玩法全链路回归与发布前验收（阶段3） | pending | - | GM-0349 | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T94 | V1.1 玩法：两周循环与存档回放一致性（阶段3） | pending | - | GM-0350 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Core-Game-Loop.md` |
| T95 | V1.1 玩法深度：招募链路边界与异常分支（阶段3） | pending | - | GM-0351 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Recruitment.md` |
| T96 | V1.1 玩法深度：成员管理状态机与角色变更（阶段3） | pending | - | GM-0352 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Member-Management.md` |
| T97 | V1.1 玩法深度：Raid 进阶规则与结果可解释性（阶段3） | pending | - | GM-0353 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-PVE-Raid.md` |
| T98 | V1.1 玩法深度：经济与后勤联动闭环（阶段3） | pending | - | GM-0354 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Tactical-Rewards.md` |
| T99 | V1.1 玩法深度：媒体事件与声望反馈闭环（阶段3） | pending | - | GM-0355 | `docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Media-Reputation.md` |
| T100 | V1.1 收口：玩法全链路回归与发布前验收（阶段4） | pending | - | GM-0356 | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T101 | V1.1 收口：玩法全链路回归与发布前验收（阶段5） | pending | - | GM-0357 | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |
| T102 | V1.1 收口：玩法全链路回归与发布前验收（阶段6） | pending | - | GM-0358 | `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md` |

- ?????`.taskmaster/tasks/tasks.json` + `.taskmaster/tasks/tasks_back.json` + `.taskmaster/tasks/tasks_gameplay.json`
- ?????2026-03-09
<!-- END:T53-T102-MAP -->
