创建 Godot C# 脚本: $ARGUMENTS

参数格式: <脚本名称> [基类] [层级] [命名空间]
- 脚本名称: 必填,如 GuildPanel
- 基类: 可选,默认 Node (可选: Control, Node2D, Node3D, Resource, RefCounted 等)
- 层级: 可选,默认 Core (可选: Core, Adapter, UI, Scene)
- 命名空间: 可选,根据层级自动推断 (Game.Core, Game.Adapters, Game.UI)

步骤:

1. 确定文件路径:
   - Core 层 → Scripts/Core/<脚本名称>.cs (纯 C#,无 Godot 依赖)
   - Adapter 层 → Scripts/Adapters/<脚本名称>.cs (封装 Godot API)
   - UI 层 → Scripts/UI/<脚本名称>.cs (UI 组件)
   - Scene 层 → Scripts/Scenes/<脚本名称>.cs (场景脚本)

2. 生成 C# 脚本框架:

```csharp
using Godot;
using System;

namespace {{NAMESPACE}};

/// <summary>
/// {{DESCRIPTION}}
/// 关联 ADR: {{ADR_REFS}}
/// </summary>
public partial class {{CLASS_NAME}} : {{BASE_CLASS}}
{
    // Signals (如果是 UI 或 Scene 层)
    {{SIGNALS}}

    // Export 属性 (可在编辑器中配置)
    {{EXPORTS}}

    // 私有字段
    {{FIELDS}}

    // Godot 生命周期方法
    public override void _Ready()
    {
        // 初始化逻辑
    }

    public override void _Process(double delta)
    {
        // 每帧更新逻辑(如需要)
    }

    // 公共方法
    {{PUBLIC_METHODS}}

    // 私有方法
    {{PRIVATE_METHODS}}
}
```

3. 根据层级添加特定内容:

   **Core 层** (纯业务逻辑):
   - 不继承 Godot 类
   - 使用接口定义依赖 (ITime, IInput, IResourceLoader)
   - 100% 可单元测试
   - 示例:
   ```csharp
   namespace Game.Core;
   public class GuildService
   {
       private readonly IGuildRepository _repository;
       public GuildService(IGuildRepository repository) { }
   }
   ```

   **Adapter 层** (Godot API 封装):
   - 实现 Core 层定义的接口
   - 封装 Godot API 调用
   - 示例:
   ```csharp
   namespace Game.Adapters;
   public class TimeAdapter : ITime
   {
       public double GetTimeSinceStartup() => Time.GetTicksMsec() / 1000.0;
   }
   ```

   **UI/Scene 层** (场景脚本):
   - 继承 Godot 节点类型
   - 定义 Signals 用于事件通信
   - 使用 [Export] 暴露编辑器属性
   - 示例:
   ```csharp
   namespace Game.UI;
   public partial class GuildPanel : Control
   {
       [Signal] public delegate void GuildCreatedEventHandler(string guildName);
       [Export] public string DefaultGuildName { get; set; } = "";
   }
   ```

4. 添加 XML 文档注释:
   - 类级别: 描述功能和关联的 ADR
   - 公共方法: <summary>, <param>, <returns>
   - Signals: 说明触发时机和参数

5. 遵循命名约定:
   - 类名: PascalCase (GuildPanel)
   - 方法名: PascalCase (CreateGuild)
   - 私有字段: _camelCase (_guildRepository)
   - 常量: UPPER_SNAKE_CASE (MAX_GUILD_SIZE)

6. 添加测试占位符提示:
   - Core 层: 提示创建 xUnit 测试
   - Adapter 层: 提示创建契约测试
   - UI/Scene 层: 提示创建 GdUnit4 测试

7. ADR 引用建议:
   - 数据访问 → ADR-0006 (数据存储)
   - 端口适配 → ADR-0007 (端口适配器模式)
   - 事件发布 → ADR-0004 (事件总线和契约)
   - 安全相关 → ADR-0002 (安全基线)

示例用法:
- `/godot-script GuildService` → Scripts/Core/GuildService.cs (纯 C# 类)
- `/godot-script TimeAdapter ITime Adapter` → Scripts/Adapters/TimeAdapter.cs
- `/godot-script GuildPanel Control UI` → Scripts/UI/GuildPanel.cs
- `/godot-script Player Node2D Scene` → Scripts/Scenes/Player.cs

输出格式:
- 生成完整的 C# 文件
- 显示文件路径
- 提示下一步操作(创建测试、附加到场景等)
- 建议 Git 提交命令
创建 Godot C# 脚本: $ARGUMENTS

参数格式: <脚本名称> [基类] [层级] [命名空间]

- 脚本名称: 必填，例如 `GuildPanel`
- 基类: 可选，默认 `Node`
  - 典型取值: `Node`, `Control`, `Node2D`, `Node3D`, `Resource`, `RefCounted` 等
- 层级: 可选，默认 `Core`
  - `Core`    → 纯领域逻辑（Game.Core）
  - `Adapter` → Godot 适配层（Game.Godot/Adapters/** 或 Scripts/Adapters/**）
  - `UI`      → UI 组件脚本（Game.Godot/Scripts/UI/**）
  - `Scene`   → 场景脚本（Game.Godot/Scripts/Screens/**）
- 命名空间: 可选，默认根据层级选择:
  - Core: `Game.Core`
  - Adapter: `Game.Adapters`
  - UI/Scene: `Game.UI`

1. 确定文件路径（对齐当前项目结构）:

   - Core 层:
     - 推荐路径: `Game.Core/Domain/<脚本名称>.cs` 或 `Game.Core/Services/<脚本名称>.cs`
     - 若是纯契约/事件/DTO，则使用 `Scripts/Core/Contracts/**`，该目录是 Contracts 的 SSoT
     - 要求: 不引用任何 Godot 类型，保持可单元测试
   - Adapter 层:
     - 推荐路径: `Game.Godot/Adapters/<脚本名称>.cs` 或子目录（例如 Security/Db 等）
     - 功能: 把 Game.Core 的接口对接到 Godot API
   - UI 层:
     - 路径: `Game.Godot/Scripts/UI/<脚本名称>.cs`
     - 功能: 控件脚本、HUD、面板等
   - Scene 层:
     - 路径: `Game.Godot/Scripts/Screens/<脚本名称>.cs`
     - 功能: 对应场景根节点的控制脚本

2. 生成 C# 脚本骨架:

```csharp
using Godot;
using System;

namespace {{NAMESPACE}};

/// <summary>
/// {{DESCRIPTION}}
/// 相关 ADR: {{ADR_REFS}}
/// </summary>
public partial class {{CLASS_NAME}} : {{BASE_CLASS}}
{
    // Signals (如果是 UI/Scene 层)
    {{SIGNALS}}

    // Export 属性 (可在编辑器中配置)
    {{EXPORTS}}

    // 私有字段
    {{FIELDS}}

    // Godot 生命周期方法
    public override void _Ready()
    {
        // 初始化逻辑
    }

    public override void _Process(double delta)
    {
        // 每帧更新逻辑（若需要）
    }

    // 公共方法
    {{PUBLIC_METHODS}}

    // 私有方法
    {{PRIVATE_METHODS}}
}
```

3. 按层级补充特定内容:

   **Core 层**（业务逻辑）:
   - 不依赖 Godot 类型
   - 通过接口注入依赖（ITime, IInput, IResourceLoader 等）
   - 必须 100% 可在 xUnit 中单测

   **Adapter 层**（Godot API 封装）:
   - 实现 Core 层定义的接口
   - 封装 Godot API 调用

   **UI/Scene 层**:
   - 继承 Godot 节点类型（Control / Node2D ...）
   - 使用 `[Signal]` 定义对外事件
   - 使用 `[Export]` 暴露配置项

4. XML 注释规范:

   - 类注释: 描述职责与关联 ADR
   - 公共方法: `<summary>`, `<param>`, `<returns>`
   - Signals: 描述触发时机与参数含义

5. 命名规范:

   - 类名: PascalCase（如 `GuildPanel`）
   - 方法名: PascalCase（如 `CreateGuild`）
   - 私有字段: `_camelCase`（如 `_guildRepository`）
   - 常量: `UPPER_SNAKE_CASE`（如 `MAX_GUILD_SIZE`）

6. 测试建议:

   - Core 层: 添加 xUnit + FluentAssertions 测试（Game.Core.Tests/**）
   - Adapter 层: 添加 xUnit 或 GdUnit4 集成测试，验证对 Godot API 的封装行为
   - UI/Scene 层: 添加 GdUnit4 测试（Tests.Godot/tests/**），验证可见性与信号触发

7. ADR 引用建议:

   - 数据访问: ADR-0006（数据存储）
   - 端口适配: ADR-0007（Ports/Adapters 模式）
   - 事件发布: ADR-0004（事件总线与信号命名）
   - 安全相关: ADR-0019（Godot 安全基线）

示例用法:

- `/godot-script GuildService` → 在 Game.Core 中生成纯 C# 业务类
- `/godot-script TimeAdapter ITime Adapter` → `Game.Godot/Adapters/TimeAdapter.cs`
- `/godot-script GuildPanel Control UI` → `Game.Godot/Scripts/UI/GuildPanel.cs`
- `/godot-script Player Node2D Scene` → `Game.Godot/Scripts/Screens/Player.cs`

输出格式:

- 生成完整的 C# 文件
- 显示文件路径
- 提示下一步操作（创建测试、挂接到场景等）
- 建议的 Git 提交命令
