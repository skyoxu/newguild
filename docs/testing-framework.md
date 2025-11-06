# 测试框架完整指南

本文档定义了 Godot 项目模板的测试策略、工具选型和最佳实践。

## 测试策略

### 三层测试金字塔

本模板遵循**测试金字塔原则**，针对 TDD 和 AI 辅助开发进行优化：

```
        ╱╲
       ╱  ╲       E2E Tests (5%)
      ╱────╲      - 关键用户流程冒烟测试
     ╱      ╲     - headless Godot 回放
    ╱────────╲
   ╱  Scene   ╲   Scene Tests (15%)
  ╱   Tests    ╲  - GdUnit4 集成测试
 ╱──────────────╲ - 节点生命周期、信号验证
╱   Unit Tests   ╲ Unit Tests (80%)
╲────────────────╱ - xUnit 纯 C# 逻辑
                   - 毫秒级红绿灯循环
```

**核心理念：**
- 80% 核心逻辑通过快速单元测试覆盖（无需启动 Godot）
- 15% 集成测试验证 Godot 场景和信号交互
- 5% 端到端测试仅保留关键路径冒烟

## 单元测试：xUnit

### 技术栈

| 组件 | 工具 | 用途 |
|------|------|------|
| 测试框架 | **xUnit** | Fact/Theory 模式，TDD 友好 |
| 断言库 | **FluentAssertions** | 语义化断言，可读性强 |
| Mock框架 | **NSubstitute** | 轻量接口模拟 |
| 覆盖率 | **coverlet** | 代码覆盖率收集 |

### 项目结构

```
Game.Core/                      # 纯 C# 类库，零 Godot 依赖
├── Domain/                     # 领域模型
│   ├── Entities/              # 游戏实体
│   └── ValueObjects/          # 值对象
├── Services/                   # 业务逻辑
│   ├── GameStateManager.cs
│   └── EconomySimulator.cs
└── Interfaces/                 # 适配器契约
    ├── ITime.cs
    ├── IInput.cs
    └── IResourceLoader.cs

Game.Core.Tests/                # xUnit 测试项目
├── Domain/
│   └── PlayerTests.cs
├── Services/
│   └── GameStateManagerTests.cs
└── Integration/                # 适配器契约测试
    └── TimeAdapterTests.cs
```

### TDD 工作流

```
1. Red   → 在 Game.Core.Tests/ 写失败测试
2. Green → 在 Game.Core/ 实现最小化代码通过测试
3. Refactor → 优化设计，保持测试绿色
4. Integrate → 通过 Adapters/ 层集成到 Godot
```

### 示例测试

#### 基础单元测试

```csharp
// Game.Core.Tests/Domain/PlayerTests.cs
using Xunit;
using FluentAssertions;

public class PlayerTests
{
    [Fact]
    public void Player_TakeDamage_ReducesHealth()
    {
        // Arrange
        var player = new Player(health: 100);

        // Act
        player.TakeDamage(30);

        // Assert
        player.Health.Should().Be(70);
    }

    [Theory]
    [InlineData(100, 50, 50)]
    [InlineData(100, 150, 0)]
    [InlineData(50, 25, 25)]
    public void Player_TakeDamage_HandlesEdgeCases(int initialHealth, int damage, int expectedHealth)
    {
        // Arrange
        var player = new Player(health: initialHealth);

        // Act
        player.TakeDamage(damage);

        // Assert
        player.Health.Should().Be(expectedHealth);
    }
}
```

#### 接口注入测试

```csharp
// Game.Core.Tests/Services/GameStateManagerTests.cs
using Xunit;
using FluentAssertions;
using NSubstitute;

public class GameStateManagerTests
{
    [Fact]
    public void GameStateManager_Update_UsesInjectedTime()
    {
        // Arrange
        var mockTime = Substitute.For<ITime>();
        mockTime.DeltaTime.Returns(0.016f); // 固定 60 FPS

        var stateManager = new GameStateManager(mockTime);

        // Act
        stateManager.Update();

        // Assert
        mockTime.Received(1).DeltaTime;
        stateManager.ElapsedTime.Should().BeApproximately(0.016f, 0.001f);
    }
}
```

### 运行测试

```bash
# 运行所有单元测试
dotnet test

# 带覆盖率收集
dotnet test --collect:"XPlat Code Coverage"

# 运行特定测试类
dotnet test --filter "FullyQualifiedName~PlayerTests"

# 详细输出
dotnet test --logger "console;verbosity=detailed"
```

### 覆盖率目标

- **行覆盖率**: ≥90%
- **分支覆盖率**: ≥85%
- **方法覆盖率**: ≥95%

**门禁规则：**
- 覆盖率低于目标值时 CI 失败
- 新增代码必须带测试，否则 PR 不通过

## 场景测试：GdUnit4

### 技术栈

| 组件 | 工具 | 用途 |
|------|------|------|
| 测试框架 | **GdUnit4** | Godot 4 原生测试支持 |
| 运行模式 | **Headless** | CI/CD 无图形界面执行 |
| 输出格式 | **JUnit XML** | 集成到 CI 报告 |

### 使用场景

GdUnit4 用于测试**必须在 Godot 环境中运行**的逻辑：

- 节点生命周期（`_ready`, `_process`, `_exit_tree`）
- 信号连接和触发验证
- 场景树结构和子节点访问
- 资源加载路径正确性
- 场景切换和状态转换

**不应使用 GdUnit4 的场景：**
- 纯业务逻辑（应该在 Game.Core 用 xUnit 测试）
- 数学计算、算法验证
- 数据结构操作

### 项目结构

```
tests/                          # GdUnit4 测试目录
├── Scenes/                     # 场景节点测试
│   ├── PlayerNodeTests.gd
│   └── EnemySpawnerTests.gd
├── Integration/                # 集成测试
│   ├── ResourceLoadingTests.gd
│   └── SceneTransitionTests.gd
└── E2E/                        # 端到端冒烟测试
    └── GameFlowSmokeTests.gd
```

### 示例测试

#### 信号验证测试

```gdscript
# tests/Scenes/PlayerNodeTests.gd
extends GdUnitTestSuite

func test_player_emits_death_signal_when_health_zero():
    # Arrange
    var player = auto_free(preload("res://scenes/Player.tscn").instantiate())
    var signal_monitor = monitor_signals(player)

    # Act
    player.health = 10
    player.take_damage(10)

    # Assert
    assert_signal_emitted(player, "died")
    assert_int(player.health).is_equal(0)

func test_player_does_not_die_with_remaining_health():
    # Arrange
    var player = auto_free(preload("res://scenes/Player.tscn").instantiate())
    var signal_monitor = monitor_signals(player)

    # Act
    player.health = 50
    player.take_damage(30)

    # Assert
    assert_signal_not_emitted(player, "died")
    assert_int(player.health).is_equal(20)
```

#### 资源加载测试

```gdscript
# tests/Integration/ResourceLoadingTests.gd
extends GdUnitTestSuite

func test_all_scene_paths_valid():
    # Arrange
    var scene_paths = [
        "res://scenes/Player.tscn",
        "res://scenes/Enemy.tscn",
        "res://scenes/levels/Level1.tscn"
    ]

    # Act & Assert
    for path in scene_paths:
        var scene = load(path)
        assert_object(scene).is_not_null()
        assert_bool(ResourceLoader.exists(path)).is_true()
```

#### 场景转换测试

```gdscript
# tests/Integration/SceneTransitionTests.gd
extends GdUnitTestSuite

func test_main_menu_to_game_transition():
    # Arrange
    var scene_manager = autoload("SceneManager")

    # Act
    scene_manager.change_scene("res://scenes/Game.tscn")
    await get_tree().process_frame  # 等待场景切换完成

    # Assert
    var current_scene = get_tree().current_scene
    assert_str(current_scene.name).is_equal("Game")
```

### 运行测试

```bash
# 在 Godot 编辑器中运行（开发时）
# 使用 GdUnit4 插件面板

# CI/CD headless 模式
godot --headless --path . --gdunit-run

# 输出到指定目录
godot --headless --path . --gdunit-run --gdunit-report-path=logs/ci/

# 仅运行特定测试套件
godot --headless --path . --gdunit-run --gdunit-test-suite="res://tests/Scenes/PlayerNodeTests.gd"
```

## 端到端测试

### 策略

E2E 测试**仅保留关键路径冒烟**，避免过度投入维护成本：

- 游戏启动 → 主菜单加载 → 退出
- 主菜单 → 开始游戏 → 第一关加载成功
- 保存/加载游戏存档流程

### 示例

```gdscript
# tests/E2E/GameFlowSmokeTests.gd
extends GdUnitTestSuite

func test_game_startup_to_main_menu():
    # Arrange
    var scene_manager = autoload("SceneManager")

    # Act
    scene_manager.load_main_menu()
    await get_tree().create_timer(1.0).timeout  # 等待加载

    # Assert
    var main_menu = get_tree().current_scene
    assert_str(main_menu.name).is_equal("MainMenu")
    assert_object(main_menu.get_node("StartButton")).is_not_null()

func test_start_game_loads_first_level():
    # Arrange
    var scene_manager = autoload("SceneManager")
    scene_manager.load_main_menu()
    await get_tree().process_frame

    # Act
    var main_menu = get_tree().current_scene
    main_menu.get_node("StartButton").pressed.emit()
    await get_tree().create_timer(2.0).timeout

    # Assert
    var game_scene = get_tree().current_scene
    assert_str(game_scene.name).contains("Level")
```

## CI/CD 质量门禁

### 推荐管道

```yaml
# .github/workflows/ci.yml 或 .gitlab-ci.yml
test:
  steps:
    # 1. 类型检查
    - dotnet build /warnaserror

    # 2. 单元测试 + 覆盖率
    - dotnet test --collect:"XPlat Code Coverage"
    - coverlet Game.Core.Tests/bin/Debug/net8.0/Game.Core.Tests.dll --target "dotnet" --targetargs "test --no-build"

    # 3. 场景集成测试
    - godot --headless --path . --gdunit-run --gdunit-report-path=logs/ci/

    # 4. 代码重复检测
    - jscpd --threshold 2 Game.Core/ Adapters/

    # 5. 发布健康度检查
    - curl "https://sentry.io/api/0/organizations/{org}/releases/{version}/health/" | jq '.crashFreePercentage >= 99.5'

quality_gates:
  rules:
    - coverage >= 90% (line)
    - coverage >= 85% (branch)
    - duplication <= 2%
    - sentry_crash_free >= 99.5%
```

### 覆盖率报告

```bash
# 生成 HTML 覆盖率报告
dotnet test --collect:"XPlat Code Coverage"
reportgenerator -reports:TestResults/**/coverage.cobertura.xml -targetdir:logs/coverage -reporttypes:Html

# 上传到 Codecov/Coveralls（可选）
bash <(curl -s https://codecov.io/bash)
```

## 最佳实践

### 1. 确定性测试

**问题：**Godot 的时间、随机数、输入系统是全局状态，导致测试不可重复。

**解决方案：**接口注入 + 适配器隔离

```csharp
// Game.Core/Interfaces/ITime.cs
public interface ITime
{
    float DeltaTime { get; }
    float TotalTime { get; }
}

// Adapters/TimeAdapter.cs
public class TimeAdapter : ITime
{
    public float DeltaTime => (float)Engine.GetProcessTime();
    public float TotalTime => Time.GetTicksMsec() / 1000f;
}

// 测试时使用 Fake
public class FakeTime : ITime
{
    public float DeltaTime { get; set; } = 0.016f; // 固定 60 FPS
    public float TotalTime { get; set; } = 0f;
}
```

**同样模式适用于：**
- `IRandom` - 控制随机数生成
- `IInput` - 模拟玩家输入
- `IResourceLoader` - 控制资源加载

### 2. 测试组织

```
Game.Core.Tests/
├── Domain/                     # 实体和值对象测试
│   ├── PlayerTests.cs
│   └── InventoryTests.cs
├── Services/                   # 业务逻辑测试
│   ├── GameStateManagerTests.cs
│   └── EconomySimulatorTests.cs
└── Integration/                # 适配器契约测试
    ├── TimeAdapterTests.cs
    └── InputAdapterTests.cs

tests/
├── Scenes/                     # 节点生命周期测试
│   ├── PlayerNodeTests.gd
│   └── EnemySpawnerTests.gd
├── Integration/                # Godot 集成测试
│   ├── ResourceLoadingTests.gd
│   └── SceneTransitionTests.gd
└── E2E/                        # 端到端冒烟测试
    └── GameFlowSmokeTests.gd
```

### 3. 命名约定

**C# 单元测试：**
- 测试文件: `{ClassName}Tests.cs`
- 测试方法: `MethodName_Scenario_ExpectedBehavior`

```csharp
// 好的命名
public void Player_TakeDamage_ReducesHealth()
public void Player_TakeDamage_WithZeroHealth_DoesNotReduceFurther()

// 不好的命名
public void Test1()
public void TestPlayerDamage()
```

**GDScript 场景测试：**
- 测试文件: `{NodeName}Tests.gd`
- 测试方法: `test_描述性名称`

```gdscript
# 好的命名
func test_player_emits_death_signal_when_health_zero()
func test_enemy_spawner_creates_enemy_at_spawn_point()

# 不好的命名
func test1()
func test_player()
```

### 4. 测试数据管理

**使用 Theory 参数化测试：**

```csharp
[Theory]
[InlineData(100, 30, 70)]
[InlineData(50, 60, 0)]    // 伤害超过生命值
[InlineData(100, 0, 100)]  // 零伤害
public void Player_TakeDamage_VariousScenarios(int health, int damage, int expected)
{
    var player = new Player(health);
    player.TakeDamage(damage);
    player.Health.Should().Be(expected);
}
```

**使用 Builder 模式创建测试数据：**

```csharp
public class PlayerBuilder
{
    private int _health = 100;
    private int _level = 1;

    public PlayerBuilder WithHealth(int health)
    {
        _health = health;
        return this;
    }

    public PlayerBuilder WithLevel(int level)
    {
        _level = level;
        return this;
    }

    public Player Build() => new Player(_health, _level);
}

// 使用
var player = new PlayerBuilder()
    .WithHealth(50)
    .WithLevel(5)
    .Build();
```

### 5. 避免常见陷阱

**不要在单元测试中访问 Godot API：**

```csharp
// 错误 - 单元测试不应依赖 Godot
[Fact]
public void Test_UseGodotTime()
{
    var time = Time.GetTicksMsec(); // 会失败，Godot 未初始化
}

// 正确 - 通过接口注入
[Fact]
public void Test_UseInjectedTime()
{
    var mockTime = Substitute.For<ITime>();
    mockTime.TotalTime.Returns(1000f);
    // 测试使用 mockTime
}
```

**不要在 GdUnit4 中测试纯逻辑：**

```gdscript
# 错误 - 这应该是 C# 单元测试
func test_calculate_damage():
    var damage = calculate_damage(10, 5)  # 纯数学计算
    assert_int(damage).is_equal(50)

# 正确 - 测试 Godot 特定行为
func test_player_node_emits_signal():
    var player = auto_free(preload("res://scenes/Player.tscn").instantiate())
    assert_signal_emitted(player, "health_changed")
```

## 工具和插件

### 开发工具

- **Visual Studio 2022** / **JetBrains Rider** - C# IDE 与测试运行器
- **Visual Studio Code** + C# Dev Kit - 轻量级选择
- **NCrunch** / **dotCover** - 实时覆盖率反馈（可选）

### CI/CD 集成

- **GitHub Actions** - 推荐用于开源项目
- **GitLab CI** - 企业自托管
- **Azure Pipelines** - 微软生态集成

### 覆盖率工具

- **coverlet** - .NET Core 覆盖率收集器（必需）
- **ReportGenerator** - HTML 报告生成
- **Codecov** / **Coveralls** - 在线覆盖率展示

## 常见问题

**Q: 为什么不直接用 GdUnit4 测试所有代码？**

A: GdUnit4 需要启动 Godot 引擎，测试执行慢（秒级）。xUnit 纯 C# 测试执行快（毫秒级），更适合 TDD 红绿灯循环。

**Q: 适配器层需要测试吗？**

A: 需要契约测试，验证适配器正确实现接口。但不需要测试 Godot 本身的行为（假设 Godot API 是正确的）。

**Q: 覆盖率 90% 是否过高？**

A: 对于纯业务逻辑（Game.Core），90% 是合理目标。对于 UI 绑定代码、场景脚本，可以放宽到 70-80%。

**Q: E2E 测试应该覆盖多少场景？**

A: 仅关键路径冒烟（3-5个测试）。过多 E2E 会导致维护成本高、执行慢、易碎。

**Q: 如何处理 Godot 单例（如 `Input`, `Time`）？**

A: 通过适配器隔离，在测试中注入 Mock。见"确定性测试"章节。

## 参考资源

- [xUnit 官方文档](https://xunit.net/)
- [FluentAssertions 文档](https://fluentassertions.com/)
- [NSubstitute 文档](https://nsubstitute.github.io/)
- [GdUnit4 GitHub](https://github.com/MikeSchulze/gdUnit4)
- [coverlet GitHub](https://github.com/coverlet-coverage/coverlet)
- [Godot 单元测试指南](https://docs.godotengine.org/en/stable/tutorials/scripting/unit_testing.html)
