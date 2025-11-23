创建 Godot 场景文件: $ARGUMENTS

参数格式: <场景名称> [节点类型] [目标目录]
- 场景名称: 必填,如 GuildPanel
- 节点类型: 可选,默认 Control (可选: Control, Node2D, Node3D, CanvasLayer, VBoxContainer, HBoxContainer 等)
- 目标目录: 可选,默认 Scenes/ (相对于项目根目录)

步骤:

1. 解析参数:
   - 场景名称 = 第一个参数
   - 节点类型 = 第二个参数(如果提供),否则使用 Control
   - 目标目录 = 第三个参数(如果提供),否则使用 Scenes/

2. 读取场景模板:
   - 使用 Read 工具读取 templates/scene_template.tscn

3. 生成场景文件:
   - 替换模板中的 {{SCENE_NAME}} 为实际场景名称
   - 替换模板中的 {{NODE_TYPE}} 为实际节点类型
   - 使用 Write 工具写入 <目标目录>/<场景名称>.tscn

4. 创建对应的 C# 脚本:
   - 根据 ADR-0007 (端口适配器模式)确定脚本位置
   - 如果是 UI 组件 → Scripts/UI/<场景名称>.cs
   - 如果是游戏逻辑 → Scripts/Core/<场景名称>.cs
   - 如果是适配层 → Scripts/Adapters/<场景名称>.cs
   - 生成基础 C# 脚本框架,继承自对应的 Godot 类型

5. 关联脚本到场景(手动步骤提示):
   - 输出提示: "场景文件已创建,需要在 Godot 编辑器中手动附加脚本,或使用 headless 模式:"
   - 提供命令: `godot --headless --path . --script attach_script.gd <场景路径> <脚本路径>`

6. 更新 ADR 引用(如果相关):
   - 如果涉及事件系统 → 引用 ADR-0004 (事件总线和契约)
   - 如果涉及数据存储 → 引用 ADR-0006 (数据存储)
   - 如果涉及端口适配 → 引用 ADR-0007 (端口适配器模式)

7. Git 提交建议:
   - 使用 SuperClaude /sc:git 自动提交
   - 或手动提交: `git add <场景文件> <脚本文件> && git commit -m "feat: add <场景名称> scene (ADR-xxxx)"`

示例用法:
- `/godot-scene GuildPanel` → 创建 Scenes/GuildPanel.tscn + Scripts/UI/GuildPanel.cs (Control)
- `/godot-scene Player Node2D` → 创建 Scenes/Player.tscn + Scripts/Core/Player.cs (Node2D)
- `/godot-scene MainMenu Control UI/Menus` → 创建 UI/Menus/MainMenu.tscn + Scripts/UI/MainMenu.cs

注意事项:
- 场景文件使用 .tscn 文本格式,便于版本控制
- C# 脚本遵循项目命名约定 (PascalCase)
- 所有脚本必须包含命名空间: `namespace Game.UI;` / `namespace Game.Core;` / `namespace Game.Adapters;`
- 遵循三层架构原则 (Core/Adapter/Scene)
创建 Godot 场景文件: $ARGUMENTS

参数格式: <场景名称> [根节点类型] [目标目录]

- 场景名称: 必填，例如 `GuildPanel`
- 根节点类型: 可选，默认 `Control`
  - 典型取值: `Control`, `Node2D`, `Node3D`, `CanvasLayer`, `VBoxContainer`, `HBoxContainer` 等
- 目标目录: 可选，默认 `Game.Godot/Scenes/`
  - 建议按功能分子目录，例如 `Game.Godot/Scenes/UI/`、`Game.Godot/Scenes/Game/`

约束（对齐当前 newguild 项目结构）:

- 场景文件统一放在 `Game.Godot/Scenes/**`
- Godot C# 脚本放在 `Game.Godot/Scripts/**` 下按功能分层:
  - UI 场景脚本: `Game.Godot/Scripts/UI/<Name>.cs`
  - 主界面/流程场景脚本: `Game.Godot/Scripts/Screens/<Name>.cs`
  - 适配器类（非 Node 时）: `Game.Godot/Adapters/**` 或 `Game.Godot/Scripts/Adapters/**`
- 纯领域逻辑不要放在这里，而是放到 `Game.Core/**`（参见 ADR-0018 技术栈与三层架构）

步骤:

1. 解析参数:
   - `scene_name` = 第一个参数
   - `node_type` = 第二个参数（如未提供则用 `Control`）
   - `target_dir` = 第三个参数（如未提供则用 `Game.Godot/Scenes/`）

2. 读取场景模板:
   - 使用 Read 工具读取 `templates/scene_template.tscn`

3. 生成场景文件:
   - 将模板中的 `{{SCENE_NAME}}` 替换为实际场景名称
   - 将 `{{NODE_TYPE}}` 替换为实际根节点类型
   - 计算输出路径: `<target_dir>/<SceneName>.tscn`
   - 使用 Write 工具写入文件（UTF-8 编码）

4. 生成对应的 C# 脚本（可选，交给 `/godot-script` 命令）:
   - UI 场景: 建议调用 `/godot-script <SceneName> Control UI`
     - 路径: `Game.Godot/Scripts/UI/<SceneName>.cs`
   - 游戏主场景/Screen: 建议调用 `/godot-script <SceneName> Control Scene`
     - 路径: `Game.Godot/Scripts/Screens/<SceneName>.cs`
   - 纯领域逻辑: 不在此命令内生成，改用 `/godot-script` 直接落到 `Game.Core/**`

5. 在 Godot 中挂接脚本:
   - 提示用户: "场景文件已创建，请在 Godot Editor 中为根节点手动 Attach C# 脚本"
   - 如需自动化，可在后续增加专用脚本（当前模板中不存在 attach_script.gd，不要引用不存在的文件）

6. ADR 引用与安全/架构对齐:

   - 若场景中引入领域事件或信号命名规则:
     - 引用 ADR-0004（事件总线与信号命名），事件类型遵循 `${DOMAIN_PREFIX}.<entity>.<action>`
   - 若场景依赖数据存储（例如通过 Adapter 调 DB）:
     - 引用 ADR-0006（数据存储）和 ADR-0007（端口适配器模式），核心访问逻辑放在 `Game.Core`，场景脚本只调用接口
   - 若场景涉及文件/网络/外链或安全相关行为:
     - 引用 ADR-0019（Godot 安全基线），并通过 `Game.Godot/Adapters/Security/**` 等适配层访问

7. Git 提交建议:

   - 建议使用清晰的提交信息并引用相关 ADR:
     - 例如: `git add Game.Godot/Scenes/<Name>.tscn Game.Godot/Scripts/UI/<Name>.cs`
     - `git commit -m "feat: add <Name> scene (ADR-0004, ADR-0018)"`

示例用法:

- `/godot-scene GuildPanel`
  - 生成: `Game.Godot/Scenes/GuildPanel.tscn`（根节点 `Control`）
- `/godot-scene Player Node2D`
  - 生成: `Game.Godot/Scenes/Player.tscn`（根节点 `Node2D`）
- `/godot-scene MainMenu Control Game.Godot/Scenes/UI`
  - 生成: `Game.Godot/Scenes/UI/MainMenu.tscn`

注意事项:

- 场景统一使用 `.tscn` 文本格式，便于 Git diff 与审查
- 根节点名称与场景文件名保持一致（PascalCase），方便在 Godot Inspector 中识别
- 不要在场景脚本中直接写业务规则和数据库访问逻辑，这些属于 `Game.Core` 的责任
