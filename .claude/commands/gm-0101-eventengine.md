## GM‑0101：EventEngine Core 实现说明（SuperClaude 专用）

> 任务 ID：`GM-0101`  
> 任务来源：`.taskmaster/tasks/tasks_gameplay.json`  
> PRD 参考：`docs/prd.txt` 中核心循环（Resolution / Player / AI Simulation）与 PRD‑Guild‑Manager 相关段落  
> Overlay 参考：  
> - `docs/architecture/overlays/PRD-Guild-Manager/08/08-功能纵切-公会管理器.md`  
> - `docs/architecture/overlays/PRD-Guild-Manager/08/08-Contracts-Guild-Manager-Events.md`  
> - `docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md`  

### 1. 目标与范围

在 **不破坏现有三层架构** 的前提下，为 Guild Manager 的核心回合循环实现 EventEngine 的最小可用版本：

- 聚焦 **Game.Core 层** 的事件引擎逻辑，不接触 Godot API。
- 支持在 `Resolution / Player / AI Simulation` 三个阶段内，对 Guild 相关领域事件进行基本调度。
- 至少对以下 3 个 Guild 领域事件提供调度/发布能力（参考 Overlay 08）：
  - `core.guild.created`（GuildCreated）
  - `core.guild.member.joined`（GuildMemberJoined）
  - `core.guild.member.left`（GuildMemberLeft）

### 2. 允许修改的文件（白名单）

只在以下文件内修改/扩展逻辑和测试；**不要新建额外的测试入口文件**：

- 核心实现：
  - `Game.Core/Engine/EventEngine.cs`
- 单元测试：
  - `Game.Core.Tests/Domain/EventEngineTests.cs`
- 如确有需要调整契约（需遵守契约先行规范）：
  - `Game.Core/Contracts/Guild/GuildMemberJoined.cs`
  - `Game.Core/Contracts/Guild/GuildCreated.cs`（如不存在，可按模板新增）
  - `Game.Core/Contracts/Guild/GuildMemberLeft.cs`（如不存在，可按模板新增）

禁止修改（除非人工明确同意）：

- 其他 `Game.Core` 域服务与状态机文件（如 `GameTurnSystem` 等）。
- `Game.Godot/**`、`Tests.Godot/**`。
- `.taskmaster/tasks/*.json`。
- CI 脚本与质量门禁脚本（`scripts/python/*.py`）。

### 3. 设计约束（必须遵守）

1. **架构和依赖约束**

   - `EventEngine` 必须保持在 **Game.Core** 内，禁止引用任何 Godot 类型或 API。
   - 与外界的交互通过既有抽象（如 `IEventBus`、契约类型）完成。
   - 事件类型命名遵守 ADR‑0004：`${DOMAIN_PREFIX}.<entity>.<action>`，Guild 相关事件使用 `core.guild.*`。

2. **契约约束**

   - Guild 事件契约是 SSoT，位置：`Game.Core/Contracts/Guild/*.cs`。
   - 如需要新增或调整 Guild 事件契约，必须：
     - 使用统一模板（record + `EventType` 常量 + XML 注释），保持 **纯 C#**，不依赖 Godot。
     - EventType 常量必须等于 Overlay 中声明的类型字符串。
     - 字段至少覆盖 Overlay 描述中的核心信息（GuildId / UserId / 时间戳 / 角色等）。

3. **行为范围（最小目标）**

   - 在 **Resolution** 阶段，能够消费上一周/上一阶段挂起的 Guild 事件，并准备下一阶段需要的输入（暂时可以是简单列表或计数）。
   - 在 **Player** 阶段，能够接收来自玩家操作的 Guild 事件（如加入公会），并按契约转交给 EventBus / 下游处理。
   - 在 **AI Simulation** 阶段，为 GM‑0102 预留扩展点：可以先用简单占位行为（例如透传、记录调用次数），但接口形态要稳定。

### 4. TDD 与测试约束

1. **测试入口限定**

   - 只使用 `Game.Core.Tests/Domain/EventEngineTests.cs` 作为本任务的单元测试入口。
   - 如需新增测试用例，请在该文件内追加 `[Fact]`/`[Theory]`，不要新建其他测试类/文件。

2. **推荐 TDD 步骤**

   1. 先在 `EventEngineTests.cs` 中根据 PRD/Overlay 的 T2 描述，补充 **失败测试**：
      - 验证不同 Phase 调用后，状态中的事件列表/标记是否符合预期。
      - 验证 GuildCreated / GuildMemberJoined / GuildMemberLeft 事件在各阶段的处理路径。
   2. 在 `EventEngine.cs` 中实现最小逻辑，直到上述测试通过。
   3. 在确保新逻辑通过 tests 后，再考虑小幅重构（不调整公共 API）。

3. **必须保持通过的测试**

   - `dotnet test Game.Core.Tests/Game.Core.Tests.csproj` 必须全绿。
   - 不得通过“注释掉/删除现有测试”来让任务看起来完成。

### 5. MCP 使用建议（上下文检索，而非自动改架构）

在开始写代码前，先用 MCP 把上下文补全：

- 使用 **Serena MCP**：
  - 检索当前仓库中 `EventEngine` 的定义与调用点。
  - 检查 `Game.Core/Contracts/Guild/GuildMemberJoined.cs` 的现状（确认 EventType 名称和字段）。
  - 搜索是否已有 `GuildCreated`、`GuildMemberLeft` 相关类型或 TODO。
- 使用 **Context7 MCP**：
  - 检索 ADR‑0004 和 Overlay 08 中关于 Guild 事件的约束。
  - 如需 CloudEvents 结构参考，可查询标准事件建模示例。

约束：

- MCP 只用于“查上下文”和“看文档/现有契约”，**不**用于自动大规模重构架构。
- 最终落盘的 Contracts / EventEngine 逻辑要与 ADR + Overlay 一致，由你（SuperClaude）在当前文件中明确写出。

### 6. 验收标准（与任务 acceptance 对齐）

完成 GM‑0101 时，至少要满足以下条件：

1. **契约层**

   - `Game.Core/Contracts/Guild/GuildCreated.cs`（如存在）、`GuildMemberJoined.cs`、`GuildMemberLeft.cs` 三个契约文件的 `EventType` 常量符合 Overlay 与 ADR‑0004 的命名。
   - 至少一个专门的 xUnit 契约测试类验证这些常量与关键字段（可在本轮先不写，若该部分已规划到 NG‑0020，则按 NG‑0020 节奏执行）。

2. **EventEngine 行为**

   - `EventEngine` 至少支持上述三类 Guild 事件的基本调度：
     - 在 Resolution/Player/AI Simulation 周期内能够消费和发布这些事件；
     - 具体行为由 `Game.Core.Tests/Domain/EventEngineTests.cs` 内的用例覆盖。
   - `EventEngine` 不直接依赖 Godot API 或场景树，只处理纯 C# 状态和契约类型。

3. **测试状态**

   - `Game.Core.Tests/Domain/EventEngineTests.cs` 下新增或修改的测试全部通过；
   - `dotnet test Game.Core.Tests/Game.Core.Tests.csproj` 总体为成功状态。

