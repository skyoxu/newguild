创建完整的 Godot 组件(场景 + C# 脚本): $ARGUMENTS

参数格式: <组件名称> [节点类型] [层级]
- 组件名称: 必填,如 GuildPanel
- 节点类型: 可选,默认 Control (Control, Node2D, Node3D, VBoxContainer, HBoxContainer 等)
- 层级: 可选,默认 UI (Core, Adapter, UI, Scene)

此命令是 /godot-scene 和 /godot-script 的组合,会自动:
1. 创建场景文件 (.tscn)
2. 创建对应的 C# 脚本
3. 在场景中引用脚本
4. 创建测试占位符
5. 建议 Git 提交

步骤:

1. 确定组件位置:
   - UI 组件 → Scenes/UI/ + Scripts/UI/
   - Core 逻辑 → Scenes/Core/ + Scripts/Core/
   - Adapter 层 → Scenes/Adapters/ + Scripts/Adapters/
   - Scene 特定 → Scenes/Game/ + Scripts/Scenes/

2. 创建场景文件:
   使用 /godot-scene 命令创建 .tscn 文件

3. 创建 C# 脚本:
   使用 /godot-script 命令创建对应的 .cs 文件

4. 在场景中附加脚本:
   修改 .tscn 文件,添加脚本引用:
   ```
   [ext_resource type="Script" path="res://Scripts/UI/{{组件名称}}.cs" id="1"]
   [node name="{{组件名称}}" type="{{节点类型}}" script=ExtResource("1")]
   ```

5. 创建测试文件占位符:
   - xUnit 测试 (Core/Adapter 层):
     ```csharp
     // Game.Core.Tests/{{组件名称}}Tests.cs
     using Xunit;
     using FluentAssertions;
     using Game.Core;

     public class {{组件名称}}Tests
     {
         [Fact]
         public void Should_TODO_describe_test()
         {
             // Arrange
             // Act
             // Assert
         }
     }
     ```

   - GdUnit4 测试 (UI/Scene 层):
     ```gdscript
     # Tests/Scenes/test_{{组件名称}}.gd
     extends "res://addons/gdUnit4/src/core/GdUnitTestSuite.gd"

     func test_{{组件名称}}_visible_on_ready() -> void:
         var scene = preload("res://Scenes/UI/{{组件名称}}.tscn").instantiate()
         add_child_autofree(scene)
         assert_bool(scene.visible).is_true()
     ```

6. 生成文件清单:
   显示所有创建的文件:
   - [ ] Scenes/UI/{{组件名称}}.tscn
   - [ ] Scripts/UI/{{组件名称}}.cs
   - [ ] Game.Core.Tests/{{组件名称}}Tests.cs (如果是 Core 层)
   - [ ] Tests/Scenes/test_{{组件名称}}.gd (如果是 UI/Scene 层)

7. 下一步建议:
   a) 运行测试验证:
      ```bash
      # xUnit 测试 (Core/Adapter 层)
      dotnet test --filter "{{组件名称}}Tests"

      # GdUnit4 测试 (UI/Scene 层)
      py -3 scripts/python/godot_tests.py --headless --suite {{组件名称}}
      ```

   b) 使用 SuperClaude 提交:
      ```bash
      /sc:git --message "feat: add {{组件名称}} component (ADR-xxxx)"
      ```

   c) 更新 Task Master:
      ```bash
      task-master update-subtask --id=<id> --prompt="Created {{组件名称}} component with tests"
      ```

8. ADR 合规检查:
   - [ ] 遵循三层架构 (ADR-0007)
   - [ ] 事件命名符合规范 (ADR-0004)
   - [ ] 数据访问通过端口适配 (ADR-0006)
   - [ ] 测试覆盖率满足门禁 (ADR-0005)

示例用法:
- `/godot-component GuildPanel` → 完整 UI 组件(场景+脚本+测试)
- `/godot-component Player Node2D Scene` → 游戏对象组件
- `/godot-component GuildService Node Core` → 核心服务组件(虽然 Core 通常不需要场景)

特殊情况:
- Core 层组件: 只创建 C# 脚本和 xUnit 测试,跳过场景文件
- Resource 类型: 创建 .tres 资源文件而不是 .tscn 场景文件

输出格式:
```
✅ 已创建组件: {{组件名称}}
📁 文件清单:
   - Scenes/UI/{{组件名称}}.tscn
   - Scripts/UI/{{组件名称}}.cs
   - Tests/Scenes/test_{{组件名称}}.gd

🎯 下一步:
   1. 运行测试验证组件
   2. 使用 /sc:git 提交更改
   3. 更新 Task Master 进度

📋 ADR 引用: ADR-0007 (端口适配器模式)
```
创建完整的 Godot 组件（场景 + C# 脚本 + 测试）: $ARGUMENTS

参数格式: <组件名称> [节点类型] [层级]

- 组件名称: 必填，例如 `GuildPanel`
- 节点类型: 可选，默认 `Control`
  - 典型取值: `Control`, `Node2D`, `Node3D`, `VBoxContainer`, `HBoxContainer` 等
- 层级: 可选，默认 `UI`
  - `UI`      → UI 组件（Game.Godot/Scenes/UI + Game.Godot/Scripts/UI）
  - `Core`    → 纯领域逻辑组件（仅生成 Game.Core 代码与测试，不生成场景）
  - `Adapter` → 适配层组件（Game.Godot/Adapters 或 Game.Godot/Scripts/Adapters）
  - `Scene`   → 顶层场景组件（Game.Godot/Scenes/Game + Game.Godot/Scripts/Screens）

本命令实质上是 `/godot-scene` 和 `/godot-script` 的组合封装:

1. 创建场景文件（.tscn）
2. 创建对应的 C# 脚本
3. 在场景中挂接脚本
4. 创建测试文件骨架（xUnit / GdUnit4）
5. 提示 Git 提交与 Taskmaster 更新

1. 确定组件路径（对齐当前项目结构）:

   - UI 组件:
     - 场景: `Game.Godot/Scenes/UI/<组件名称>.tscn`
     - 脚本: `Game.Godot/Scripts/UI/<组件名称>.cs`
   - Core 组件:
     - 不生成场景
     - 代码: `Game.Core/Domain/<组件名称>.cs` 或 `Game.Core/Services/<组件名称>.cs`
     - 若为 Contracts/Events/DTO，则使用 `Scripts/Core/Contracts/**`
   - Adapter 组件:
     - 视情况决定是否需要场景（多数适配器无需 Node 场景）
     - 推荐脚本路径: `Game.Godot/Adapters/<组件名称>.cs` 或 `Game.Godot/Scripts/Adapters/<组件名称>.cs`
   - Scene 组件:
     - 场景: `Game.Godot/Scenes/Game/<组件名称>.tscn`
     - 脚本: `Game.Godot/Scripts/Screens/<组件名称>.cs`

2. 创建场景文件:

   - 调用 `/godot-scene <组件名称> [节点类型] [目标场景目录]`
   - 默认 UI 组件使用: `Game.Godot/Scenes/UI`
   - Scene 组件使用: `Game.Godot/Scenes/Game`

3. 创建 C# 脚本:

   - 调用 `/godot-script <组件名称> <基类> <层级>`
   - UI 组件示例: `/godot-script GuildPanel Control UI`
   - Core 组件示例: `/godot-script GuildService class Core`
   - Adapter 组件示例: `/godot-script TimeAdapter ITime Adapter`
   - Scene 组件示例: `/godot-script MainScreen Control Scene`

4. 在场景中挂接脚本:

   - 修改生成的 `.tscn` 文件，增加脚本引用:

   ```
   [ext_resource type="Script" path="res://Game.Godot/Scripts/UI/{{组件名称}}.cs" id="1"]
   [node name="{{组件名称}}" type="{{节点类型}}" script=ExtResource("1")]
   ```

   - 对于 Scene 组件，将路径替换为 `res://Game.Godot/Scripts/Screens/{{组件名称}}.cs`

5. 创建测试文件骨架:

   - Core/Adapter 组件（xUnit）:

   ```csharp
   // Game.Core.Tests/{{组件名称}}Tests.cs
   using Xunit;
   using FluentAssertions;
   using Game.Core;

   public class {{组件名称}}Tests
   {
       [Fact]
       public void Should_TODO_describe_test()
       {
           // Arrange
           // Act
           // Assert
       }
   }
   ```

   - UI/Scene 组件（GdUnit4）:

   ```gdscript
   # Tests.Godot/tests/Scenes/test_{{组件名称}}.gd
   extends "res://addons/gdUnit4/src/core/GdUnitTestSuite.gd"

   func test_{{组件名称}}_visible_on_ready() -> void:
       var scene = preload("res://Game.Godot/Scenes/UI/{{组件名称}}.tscn").instantiate()
       add_child_autofree(scene)
       assert_bool(scene.visible).is_true()
   ```

6. 生成文件清单提示:

   - 列出本次生成的所有文件:
     - [ ] `Game.Godot/Scenes/UI/{{组件名称}}.tscn`（或 Scenes/Game/...）
     - [ ] `Game.Godot/Scripts/UI/{{组件名称}}.cs`（或 Scripts/Screens/... / Adapters/...）
     - [ ] `Game.Core.Tests/{{组件名称}}Tests.cs`（如果是 Core/Adapter 组件）
     - [ ] `Tests.Godot/tests/Scenes/test_{{组件名称}}.gd`（如果是 UI/Scene 组件）

7. 下一步建议:

   a) 运行测试验证:

   ```bash
   # xUnit 测试 (Core/Adapter)
   dotnet test Game.Core.Tests/Game.Core.Tests.csproj --filter "{{组件名称}}Tests"

   # GdUnit4 测试 (UI/Scene)
   py -3 scripts/python/godot_tests.py --headless --suite {{组件名称}}
   ```

   b) 使用 Git 提交:

   ```bash
   git add Game.Godot/Scenes/** Game.Godot/Scripts/** Game.Core.Tests/** Tests.Godot/tests/**
   git commit -m "feat: add {{组件名称}} component (ADR-0004, ADR-0007, ADR-0018)"
   ```

   c) 更新 Taskmaster 任务（如有）:

   - 在 `.taskmaster/tasks/tasks_back.json` 或相关 tasks 文件中，将对应 Story 的状态
     更新为 `in_progress` / `completed`，并在 `acceptance` 中引用新创建的测试路径。

8. ADR 合规自检:

   - [ ] 三层结构符合 ADR-0007（Core / Adapter / Scene 分离）
   - [ ] 事件命名符合 ADR-0004（`${DOMAIN_PREFIX}.<entity>.<action>`）
   - [ ] 数据访问通过端口 + 适配层（ADR-0006）
   - [ ] 测试覆盖与质量门禁引用 ADR-0005（质量门禁）
   - [ ] 涉及安全/外链/文件访问时引用 ADR-0019（Godot 安全基线）

示例用法:

- `/godot-component GuildPanel` → 生成 UI 组件（场景 + 脚本 + GdUnit4 测试）
- `/godot-component Player Node2D Scene` → 生成游戏场景组件（Game 场景 + Screens 脚本）
- `/godot-component GuildService Node Core` → 仅生成 Core 代码和 xUnit 测试（不生成场景）
