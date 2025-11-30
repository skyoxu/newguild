---
Story-ID: PRD-NEWGUILD-VS-0001
Title: newguild 首个垂直切片 - 公会管理循环
Status: Active
ADR-Refs:
  - ADR-0018
  - ADR-0004
  - ADR-0005
  - ADR-0023
  - ADR-0011
Chapter-Refs:
  - CH01
  - CH04
  - CH05
  - CH06
  - CH07
  - CH10
Overlay-Refs:
  - docs/architecture/overlays/PRD-Guild-Manager/08/_index.md
  - docs/architecture/overlays/PRD-Guild-Manager/08/08-功能纵切-公会管理器.md
  - docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md
Test-Refs:
  - Game.Core.Tests/Domain/GuildCoreTests.cs
  - Game.Core.Tests/Domain/GuildMemberTests.cs
  - Tests.Godot/tests/Scenes/test_guild_main_scene.gd
  - Tests.Godot/tests/Integration/test_guild_workflow.gd
---

# PRD-NEWGUILD-VS-0001：首个垂直切片 - 公会管理循环

## 1. 引言与目标（引用 CH01）

### 1.1 Story ID
PRD-NEWGUILD-VS-0001

### 1.2 目标
定义 newguild 首个可玩的垂直切片：基础公会管理循环（创建公会 → 成员管理 → 解散公会），作为后续功能迭代的基线。

### 1.3 技术栈（引用 ADR-0018）
- 游戏引擎：Godot 4.5（Forward Plus 渲染器）
- 主语言：C# .NET 8
- 三层架构：
  - **Game.Core**：纯 C# 领域层（不依赖 Godot API）
  - **Game.Godot**：适配层（封装 Godot API，通过接口注入至 Core）
  - **Tests**：xUnit（Core 单测）+ GdUnit4（场景集成测试）

### 1.4 平台约束（引用 ADR-0011）
- 运行时：Windows-only（不支持跨平台）
- CI/CD：windows-latest 运行器，PowerShell (pwsh) shell

---

## 2. 输入/输出/状态

### 2.1 输入
- **玩家操作**：
  - 创建公会（输入：公会名称）
  - 邀请成员（输入：用户 ID）
  - 变更成员角色（输入：成员 ID, 新角色）
  - 解散公会（输入：确认操作）
- **数据源**：Guild 领域模型（持久化至 godot-sqlite DB）

### 2.2 输出
- **领域事件**（引用 ADR-0004 Godot 变体）：
  - `core.guild.created` — 公会创建成功
  - `core.guild.member.joined` — 成员加入公会
  - `core.guild.member.left` — 成员离开公会
  - `core.guild.member.role_changed` — 成员角色变更
  - `core.guild.disbanded` — 公会解散
- **UI 反馈**：Godot Scene/Control 体系，通过 Signals 与 Core 通信

### 2.3 状态
- **Guild 实体**：
  - `GuildId`：string（唯一标识）
  - `CreatorId`：string（创建者用户 ID）
  - `Name`：string（公会名称）
  - `Members`：List<GuildMember>（成员列表）
  - `CreatedAt`：DateTimeOffset（创建时间）
- **GuildMember 实体**：
  - `UserId`：string（用户 ID）
  - `Role`：enum { Member, Admin }（成员角色）

---

## 3. 存储策略（引用 ADR-0023）

### 3.1 领域数据存储
- **Guild 领域数据**：godot-sqlite（user://game.db）
  - 表：`Guilds`, `GuildMembers`
  - 契约：Scripts/Core/Contracts/Guild/**（SSoT）
- **玩家 UI 设置**：ConfigFile（user://settings.cfg）
  - 仅存储 UI 偏好（语言、音量），不存储领域数据

### 3.2 存储分层原则
遵循 ADR-0023：
- DB 专用于领域数据（存档、进度、复杂状态）
- ConfigFile 专用于 Settings
- 禁止将 Settings 存入 DB 或将领域数据存入 ConfigFile

---

## 4. 非功能约束（引用 ADR-0005）

### 4.1 质量门禁
遵循 **ADR-0005** 定义的质量阈值：
- **覆盖率门禁**：见 ADR-0005 § 硬编码质量阈值
  - Lines ≥ 90%
  - Branches ≥ 85%
- **测试工具**：
  - xUnit（Core 领域逻辑）
  - coverlet（覆盖率收集）
  - GdUnit4（Godot 场景测试，headless 支持）
- **执行脚本**：scripts/python/quality_gates.py（见 CH07 § 7.2）

### 4.2 结构性重构原则
遵循 **ADR-0005** 结构性重构优先原则：
- 禁止将行级禁用作为长期解决方案
- 所有行级禁用必须在 Taskmaster 中登记回收任务
- 每处行级禁用必须附带 `adr_refs`（至少包含 ADR-0005）与 `test_refs`

### 4.3 CI 平台（引用 ADR-0011）
- 运行器：windows-latest
- Shell：pwsh（PowerShell）
- 工作流：ci-windows.yml 或 windows-quality-gate.yml

---

## 5. 事件命名策略（引用 ADR-0004）

### 5.1 内部领域事件
- **命名规则**：core.guild.*（简化前缀）
  - 示例：`core.guild.created`, `core.guild.member.joined`
- **CloudEvents 1.0 基础**：
  - 必需字段：`id`, `source`, `type`, `time`
  - 契约 SSoT：Scripts/Core/Contracts/Guild/**
- **适用场景**：
  - 内部私有事件（公会管理、游戏逻辑）
  - 不对外发布，不需要跨组织互操作

### 5.2 外部互操作（未来规划）
如未来需发布 Mod API 或插件系统事件：
- **迁移目标**：反向 DNS 命名（如 `com.newguild.guild.created`）
- **迁移范围**：仅外部发布事件，内部事件保持简化前缀
- **文档要求**：在 Overlay/08 中注明迁移计划与适用范围

### 5.3 Base 文档占位符
Base 文档使用 `${DOMAIN_PREFIX}` 占位符：
- 示例：`${DOMAIN_PREFIX}.guild.created`
- Overlay 绑定具体前缀：`core.guild.created`

---

## 6. Test-Refs 协议（验收标准第 3 条）

### 6.1 xUnit 单元测试（Core）
- **Game.Core.Tests/Domain/GuildCoreTests.cs**
  - 覆盖：Guild 实体构造、业务规则、状态变更
  - 断言：FluentAssertions
  - Mock：NSubstitute（如需）
- **Game.Core.Tests/Domain/GuildMemberTests.cs**
  - 覆盖：GuildMember 实体、角色变更逻辑
  - 验证：成员加入/离开/角色变更的前置条件与后置状态

### 6.2 GdUnit4 场景测试（Scene）
- **Tests.Godot/tests/Scenes/test_guild_main_scene.gd**
  - 覆盖：主场景加载、节点可见性、关键 Signals 连通
  - 验证：场景树结构与 UI 初始化
- **Tests.Godot/tests/Integration/test_guild_workflow.gd**
  - 覆盖：完整垂直切片流程（启动 → 进入公会管理界面 → 执行一次最小操作 → 退出）
  - 验证：Core 与 Godot 适配层集成正常

### 6.3 Headless Smoke 测试
- **日志路径**：logs/ci/<date>/smoke/headless.log
- **判定标记**：`[TEMPLATE_SMOKE_READY]` 或 `[DB] opened`
- **执行脚本**：scripts/python/smoke_headless.py（见 Task #5）

---

## 7. 验收标准

### 7.1 PRD 完整性
- [x] 存在一份针对 newguild 的最小 PRD 片段（本文档）
- [x] 显式引用 ADR-0018/0004/0005/0023/0011（见上文各章节）
- [x] 包含输入/输出/状态/存储与非功能约束

### 7.2 Overlay/08 文档合规
- [ ] 在 overlays/PRD-Guild-Manager/08 下调整功能纵切文档
- [ ] 严格遵守"引用不复制"规则（仅引用 01/02/03 章口径）
- [ ] 不在 08 章复制阈值/策略

### 7.3 Test-Refs 关联
- [ ] 每个 Story 至少关联一条 Test-Ref（xUnit 或 GdUnit4）
- [ ] 在文档 Front-Matter 中列出（见本文档头部 Test-Refs）

### 7.4 文档审阅
- [ ] 由架构负责人确认 PRD 与 ADR/Phase 一致性
- [ ] 链接校验：任务/回链校验脚本通过（task-links-validate）

---

## 8. 依赖与后续任务

### 8.1 前置依赖
- 无（Task #1 是首个任务）

### 8.2 后续任务
- **Task #2**：公会管理器三层架构与核心类型落地
  - 实现 Guild/GuildMember 核心域模型
  - 编写 xUnit 单元测试
  - 建立 Godot 场景与 UI Glue
- **Task #5**：Python 版 headless smoke 封装与 strict 模式开关
- **Task #6**：首个垂直切片的最小 xUnit/GdUnit4/Smoke 流程打通

---

## 附录 A：关键文件路径清单

### A.1 ADR 文件
```
docs/adr/ADR-0004-event-bus-and-contracts.md
docs/adr/ADR-0018-godot-csharp-tech-stack.md
docs/adr/ADR-0005-quality-gates.md
docs/adr/ADR-0023-settings-ssot-configfile.md
docs/adr/ADR-0011-windows-only-platform-and-ci.md
```

### A.2 Guild 契约文件（规划）
```
Scripts/Core/Contracts/Guild/GuildCreated.cs
Scripts/Core/Contracts/Guild/GuildMemberJoined.cs
Scripts/Core/Contracts/Guild/GuildMemberLeft.cs
Scripts/Core/Contracts/Guild/GuildMemberRoleChanged.cs
Scripts/Core/Contracts/Guild/GuildDisbanded.cs
```

### A.3 测试文件（待创建）
```
Game.Core.Tests/Domain/GuildCoreTests.cs
Game.Core.Tests/Domain/GuildMemberTests.cs
Tests.Godot/tests/Scenes/test_guild_main_scene.gd
Tests.Godot/tests/Integration/test_guild_workflow.gd
```

### A.4 Overlay 文档路径
```
docs/architecture/overlays/PRD-Guild-Manager/08/_index.md
docs/architecture/overlays/PRD-Guild-Manager/08/08-功能纵切-公会管理器.md
docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md
```

---

## 附录 B：CloudEvents 合规性说明

### B.1 简化命名 vs 反向 DNS
- **当前选择**：简化前缀（`core.guild.*`）
  - 理由：内部私有事件，不对外发布，无需跨组织互操作
  - 参考：ADR-0004 Godot 变体口径
- **CloudEvents 1.0 推荐**：反向 DNS（`com.github.pull_request.opened`）
  - 适用场景：外部发布事件、跨组织集成、公共 API

### B.2 未来迁移路径（如需）
- **触发条件**：计划发布 Mod API 或插件系统
- **迁移范围**：仅外部发布事件使用反向 DNS
- **保留范围**：内部私有事件继续使用简化前缀
- **迁移步骤**：
  1. 在 Scripts/Core/Contracts/ 新增反向 DNS 事件类型
  2. 在 Overlay/08 中注明迁移计划与适用范围
  3. 更新 EventBusAdapter 支持两种命名约定
  4. 添加 ADR 记录迁移决策

---

**文档版本**：1.0.0
**创建日期**：2025-12-01
**负责人**：architecture
**状态**：Active
