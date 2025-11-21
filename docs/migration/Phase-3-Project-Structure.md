# Phase 3: Godot 椤圭洰缁撴瀯璁捐

> 鐘舵€? 璁捐闃舵
> 棰勪及宸ユ椂: 1 澶?
> 椋庨櫓绛夌骇: 浣?
> 鍓嶇疆鏉′欢: Phase 1-2 瀹屾垚

---

## 鐩爣

寤虹珛 Godot 4.5 + C# 椤圭洰鐨勬爣鍑嗙洰褰曠粨鏋勶紝閬靛惊绔彛閫傞厤鍣ㄦā寮忥紝鏀寔 TDD 寮€鍙戙€?

---

## 椤圭洰鏍圭洰褰曠粨鏋?

```
newguild/                              # 鏂伴」鐩牴鐩綍
鈹溾攢鈹€ .git/                               # Git 浠撳簱
鈹溾攢鈹€ .gitignore                          # Git 蹇界暐瑙勫垯
鈹溾攢鈹€ .gitattributes                      # Git 鏂囦欢灞炴€?
鈹溾攢鈹€ project.godot                       # Godot 椤圭洰閰嶇疆鏂囦欢
鈹溾攢鈹€ export_presets.cfg                  # 瀵煎嚭棰勮閰嶇疆
鈹溾攢鈹€ icon.svg                            # 椤圭洰鍥炬爣
鈹溾攢鈹€ Game.sln                            # Visual Studio 瑙ｅ喅鏂规
鈹?
鈹溾攢鈹€ Game.Core/                          # 绾?C# 棰嗗煙灞傦紙涓嶄緷璧?Godot锛?
鈹?  鈹溾攢鈹€ Game.Core.csproj
鈹?  鈹溾攢鈹€ Domain/
鈹?  鈹?  鈹溾攢鈹€ Entities/
鈹?  鈹?  鈹溾攢鈹€ ValueObjects/
鈹?  鈹?  鈹斺攢鈹€ Services/
鈹?  鈹溾攢鈹€ Ports/                          # 鎺ュ彛瀹氫箟
鈹?  鈹斺攢鈹€ Utilities/
鈹?
鈹溾攢鈹€ Game.Core.Tests/                    # xUnit 鍗曞厓娴嬭瘯
鈹?  鈹溾攢鈹€ Game.Core.Tests.csproj
鈹?  鈹溾攢鈹€ Domain/
鈹?  鈹溾攢鈹€ Fakes/                          # Fake 瀹炵幇锛堟祴璇曠敤锛?
鈹?  鈹斺攢鈹€ TestHelpers/
鈹?
鈹溾攢鈹€ Game.Godot/                         # Godot 鍦烘櫙涓庤剼鏈?
鈹?  鈹溾攢鈹€ .godot/                         # Godot 缂撳瓨锛圙it 蹇界暐锛?
鈹?  鈹溾攢鈹€ Scenes/                         # 鍦烘櫙鏂囦欢 (.tscn)
鈹?  鈹溾攢鈹€ Scripts/                        # C# 鑴氭湰锛堣杽灞傛帶鍒跺櫒锛?
鈹?  鈹溾攢鈹€ Autoloads/                      # 鍏ㄥ眬鍗曚緥
鈹?  鈹溾攢鈹€ Adapters/                       # Godot API 閫傞厤灞?
鈹?  鈹溾攢鈹€ Resources/                      # 璧勬簮瀹氫箟 (.tres)
鈹?  鈹溾攢鈹€ Themes/                         # UI 涓婚
鈹?  鈹斺攢鈹€ Assets/                         # 缇庢湳璧勪骇
鈹?      鈹溾攢鈹€ Textures/
鈹?      鈹溾攢鈹€ Fonts/
鈹?      鈹溾攢鈹€ Audio/
鈹?      鈹斺攢鈹€ Models/
鈹?
鈹溾攢鈹€ Game.Godot.Tests/                   # GdUnit4 鍦烘櫙娴嬭瘯
鈹?  鈹溾攢鈹€ Scenes/
鈹?  鈹溾攢鈹€ Scripts/
鈹?  鈹斺攢鈹€ E2E/
鈹?
鈹溾攢鈹€ docs/                               # 椤圭洰鏂囨。
鈹?  鈹溾攢鈹€ adr/                            # ADR 璁板綍
鈹?  鈹溾攢鈹€ architecture/                   # 鏋舵瀯鏂囨。
鈹?  鈹溾攢鈹€ contracts/                      # 濂戠害鏂囨。
鈹?  鈹?  鈹斺攢鈹€ signals/                    # Signal 濂戠害
鈹?  鈹斺攢鈹€ migration/                      # 杩佺Щ鏂囨。锛堟湰绯诲垪锛?
鈹?
鈹溾攢鈹€ scripts/                            # 鏋勫缓涓庡伐鍏疯剼鏈?
鈹?  鈹溾攢鈹€ ci/                             # CI/CD 鑴氭湰
鈹?  鈹溾攢鈹€ python/                         # Python 宸ュ叿
鈹?  鈹斺攢鈹€ godot/                          # Godot 杈呭姪鑴氭湰
鈹?
鈹溾攢鈹€ logs/                               # 鏃ュ織杈撳嚭鐩綍锛圙it 蹇界暐锛?
鈹?  鈹溾攢鈹€ ci/
鈹?  鈹溾攢鈹€ security/
鈹?  鈹斺攢鈹€ performance/
鈹?
鈹斺攢鈹€ TestResults/                        # 娴嬭瘯缁撴灉锛圙it 蹇界暐锛?
    鈹溾攢鈹€ coverage/
    鈹斺攢鈹€ gdunit4/
```

---

## Game.Core 椤圭洰璇︾粏缁撴瀯

```
Game.Core/
鈹溾攢鈹€ Game.Core.csproj
鈹?
鈹溾攢鈹€ Domain/
鈹?  鈹溾攢鈹€ Entities/                       # 棰嗗煙瀹炰綋锛堟湁鏍囪瘑鐨勫彲鍙樺璞★級
鈹?  鈹?  鈹溾攢鈹€ Player.cs
鈹?  鈹?  鈹溾攢鈹€ Enemy.cs
鈹?  鈹?  鈹溾攢鈹€ Item.cs
鈹?  鈹?  鈹斺攢鈹€ GameSession.cs
鈹?  鈹?
鈹?  鈹溾攢鈹€ ValueObjects/                   # 鍊煎璞★紙涓嶅彲鍙橈級
鈹?  鈹?  鈹溾攢鈹€ Position.cs
鈹?  鈹?  鈹溾攢鈹€ Vector2D.cs
鈹?  鈹?  鈹溾攢鈹€ Health.cs
鈹?  鈹?  鈹溾攢鈹€ Damage.cs
鈹?  鈹?  鈹溾攢鈹€ Score.cs
鈹?  鈹?  鈹斺攢鈹€ ItemQuantity.cs
鈹?  鈹?
鈹?  鈹溾攢鈹€ Services/                       # 棰嗗煙鏈嶅姟锛堣法瀹炰綋閫昏緫锛?
鈹?  鈹?  鈹溾攢鈹€ CombatService.cs
鈹?  鈹?  鈹溾攢鈹€ InventoryService.cs
鈹?  鈹?  鈹溾攢鈹€ ScoreService.cs
鈹?  鈹?  鈹斺攢鈹€ CollisionService.cs
鈹?  鈹?
鈹?  鈹斺攢鈹€ Events/                         # 棰嗗煙浜嬩欢锛堝彲閫夛級
鈹?      鈹溾攢鈹€ PlayerHealthChanged.cs
鈹?      鈹斺攢鈹€ EnemyDefeated.cs
鈹?
鈹溾攢鈹€ Ports/                              # 绔彛鎺ュ彛锛堜緷璧栧€掔疆锛?
鈹?  鈹溾攢鈹€ ITime.cs                        # 鏃堕棿鏈嶅姟
鈹?  鈹溾攢鈹€ IInput.cs                       # 杈撳叆鏈嶅姟
鈹?  鈹溾攢鈹€ IResourceLoader.cs              # 璧勬簮鍔犺浇
鈹?  鈹溾攢鈹€ IDataStore.cs                   # 鏁版嵁瀛樺偍
鈹?  鈹溾攢鈹€ IAudioPlayer.cs                 # 闊抽鎾斁
鈹?  鈹斺攢鈹€ ILogger.cs                      # 鏃ュ織鏈嶅姟
鈹?
鈹斺攢鈹€ Utilities/                          # 宸ュ叿绫伙紙绾嚱鏁帮級
    鈹溾攢鈹€ MathHelper.cs
    鈹溾攢鈹€ RandomHelper.cs
    鈹斺攢鈹€ StringHelper.cs
```

**Game.Core.csproj 绀轰緥**:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <!-- 绂佹寮曠敤 Godot 鐩稿叧鍖?-->
  <ItemGroup>
    <PackageReference Include="System.Text.Json" Version="8.0.0" />
  </ItemGroup>

</Project>
```

---

## Game.Core.Tests 椤圭洰缁撴瀯

```
Game.Core.Tests/
鈹溾攢鈹€ Game.Core.Tests.csproj
鈹?
鈹溾攢鈹€ Domain/
鈹?  鈹溾攢鈹€ Entities/
鈹?  鈹?  鈹溾攢鈹€ PlayerTests.cs
鈹?  鈹?  鈹溾攢鈹€ EnemyTests.cs
鈹?  鈹?  鈹斺攢鈹€ ItemTests.cs
鈹?  鈹?
鈹?  鈹溾攢鈹€ Services/
鈹?  鈹?  鈹溾攢鈹€ CombatServiceTests.cs
鈹?  鈹?  鈹溾攢鈹€ InventoryServiceTests.cs
鈹?  鈹?  鈹斺攢鈹€ ScoreServiceTests.cs
鈹?  鈹?
鈹?  鈹斺攢鈹€ ValueObjects/
鈹?      鈹溾攢鈹€ HealthTests.cs
鈹?      鈹斺攢鈹€ DamageTests.cs
鈹?
鈹溾攢鈹€ Fakes/                              # Fake 瀹炵幇锛堟祴璇曠敤锛?
鈹?  鈹溾攢鈹€ FakeTime.cs
鈹?  鈹溾攢鈹€ FakeInput.cs
鈹?  鈹溾攢鈹€ FakeDataStore.cs
鈹?  鈹斺攢鈹€ FakeLogger.cs
鈹?
鈹斺攢鈹€ TestHelpers/
    鈹溾攢鈹€ TestDataBuilder.cs              # 娴嬭瘯鏁版嵁鏋勫缓鍣?
    鈹斺攢鈹€ AssertionExtensions.cs          # 鑷畾涔夋柇瑷€
```

**Game.Core.Tests.csproj 绀轰緥**:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit" Version="2.6.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.0" />
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
    <PackageReference Include="NSubstitute" Version="5.1.0" />
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Game.Core\Game.Core.csproj" />
  </ItemGroup>

</Project>
```

---

## Game.Godot 椤圭洰缁撴瀯

```
Game.Godot/
鈹溾攢鈹€ project.godot                       # Godot 椤圭洰閰嶇疆
鈹溾攢鈹€ export_presets.cfg                  # 瀵煎嚭閰嶇疆
鈹?
鈹溾攢鈹€ Scenes/                             # 鍦烘櫙鏂囦欢
鈹?  鈹溾攢鈹€ Main.tscn                       # 涓诲満鏅?
鈹?  鈹溾攢鈹€ UI/
鈹?  鈹?  鈹溾攢鈹€ MainMenu.tscn
鈹?  鈹?  鈹溾攢鈹€ HUD.tscn
鈹?  鈹?  鈹斺攢鈹€ SettingsMenu.tscn
鈹?  鈹溾攢鈹€ Game/
鈹?  鈹?  鈹溾攢鈹€ Player.tscn
鈹?  鈹?  鈹溾攢鈹€ Enemy.tscn
鈹?  鈹?  鈹斺攢鈹€ Item.tscn
鈹?  鈹斺攢鈹€ Levels/
鈹?      鈹溾攢鈹€ Level1.tscn
鈹?      鈹斺攢鈹€ Level2.tscn
鈹?
鈹溾攢鈹€ Scripts/                            # C# 鑴氭湰锛堣杽灞傛帶鍒跺櫒锛?
鈹?  鈹溾攢鈹€ PlayerController.cs             # Node 鐢熷懡鍛ㄦ湡 + Signal 杞彂
鈹?  鈹溾攢鈹€ EnemyController.cs
鈹?  鈹溾攢鈹€ UIController.cs
鈹?  鈹斺攢鈹€ CameraController.cs
鈹?
鈹溾攢鈹€ Autoloads/                          # 鍏ㄥ眬鍗曚緥锛堣嚜鍔ㄥ姞杞斤級
鈹?  鈹溾攢鈹€ ServiceLocator.cs               # DI 瀹瑰櫒
鈹?  鈹溾攢鈹€ Security.cs                     # 瀹夊叏灏佽锛圙DScript锛?
鈹?  鈹溾攢鈹€ Observability.cs                # Sentry + 鏃ュ織
鈹?  鈹斺攢鈹€ EventBus.cs                     # 鍏ㄥ眬浜嬩欢鎬荤嚎
鈹?
鈹溾攢鈹€ Adapters/                           # Godot API 閫傞厤灞?
鈹?  鈹溾攢鈹€ GodotTimeAdapter.cs
鈹?  鈹溾攢鈹€ GodotInputAdapter.cs
鈹?  鈹溾攢鈹€ GodotResourceLoader.cs
鈹?  鈹溾攢鈹€ GodotAudioPlayer.cs
鈹?  鈹溾攢鈹€ SqliteDataStore.cs
鈹?  鈹斺攢鈹€ GodotLogger.cs
鈹?
鈹溾攢鈹€ Resources/                          # 璧勬簮瀹氫箟鏂囦欢
鈹?  鈹溾攢鈹€ player_stats.tres
鈹?  鈹斺攢鈹€ enemy_config.tres
鈹?
鈹溾攢鈹€ Themes/                             # UI 涓婚
鈹?  鈹溾攢鈹€ default_theme.tres
鈹?  鈹斺攢鈹€ button_styles.tres
鈹?
鈹斺攢鈹€ Assets/                             # 缇庢湳璧勪骇
    鈹溾攢鈹€ Textures/
    鈹?  鈹溾攢鈹€ player.png
    鈹?  鈹斺攢鈹€ enemy.png
    鈹溾攢鈹€ Fonts/
    鈹?  鈹斺攢鈹€ main_font.ttf
    鈹溾攢鈹€ Audio/
    鈹?  鈹溾攢鈹€ bgm.ogg
    鈹?  鈹斺攢鈹€ sfx_hit.wav
    鈹斺攢鈹€ Shaders/
        鈹斺攢鈹€ outline.gdshader
```

---

## Godot 椤圭洰閰嶇疆鏂囦欢

### project.godot

```ini
; Engine configuration file.

config_version=5

[application]

config/name="Game"
run/main_scene="res://Scenes/Main.tscn"
config/features=PackedStringArray("4.5", "C#", "Forward Plus")
config/icon="res://icon.svg"

[autoload]

ServiceLocator="*res://Autoloads/ServiceLocator.cs"
Security="*res://Autoloads/Security.cs"
Observability="*res://Autoloads/Observability.cs"
EventBus="*res://Autoloads/EventBus.cs"

[display]

window/size/viewport_width=1280
window/size/viewport_height=720
window/stretch/mode="canvas_items"
window/stretch/aspect="expand"

[dotnet]

project/assembly_name="Game.Godot"

[file_customization]

folder_colors={
"res://Adapters/": "blue",
"res://Autoloads/": "yellow",
"res://Scenes/": "green",
"res://Scripts/": "purple"
}

[input]

move_up={
"deadzone": 0.5,
"events": [Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":0,"physical_keycode":87,"key_label":0,"unicode":119,"echo":false,"script":null)
]
}

[physics]

2d/default_gravity=980.0
```

### export_presets.cfg

```ini
[preset.0]

name="Windows Desktop"
platform="Windows Desktop"
runnable=true
dedicated_server=false
custom_features=""
export_filter="all_resources"
include_filter=""
exclude_filter=""
export_path="build/Game.exe"
encryption_include_filters=""
encryption_exclude_filters=""
encrypt_pck=false
encrypt_directory=false

[preset.0.options]

custom_template/debug=""
custom_template/release=""
debug/export_console_wrapper=1
binary_format/embed_pck=false
texture_format/bptc=true
texture_format/s3tc=true
texture_format/etc=false
texture_format/etc2=false
binary_format/architecture="x86_64"
codesign/enable=false
codesign/timestamp=true
codesign/timestamp_server_url=""
codesign/digest_algorithm=1
codesign/description=""
codesign/custom_options=PackedStringArray()
application/modify_resources=true
application/icon=""
application/console_wrapper_icon=""
application/icon_interpolation=4
application/file_version=""
application/product_version=""
application/company_name=""
application/product_name="Game"
application/file_description=""
application/copyright=""
application/trademarks=""
application/export_angle=0
ssh_remote_deploy/enabled=false
ssh_remote_deploy/host="user@host_ip"
ssh_remote_deploy/port="22"
ssh_remote_deploy/extra_args_ssh=""
ssh_remote_deploy/extra_args_scp=""
ssh_remote_deploy/run_script="Expand-Archive -LiteralPath '{temp_dir}\\{archive_name}' -DestinationPath '{temp_dir}'
$action = New-ScheduledTaskAction -Execute '{temp_dir}\\{exe_name}' -Argument '{cmd_args}'
$trigger = New-ScheduledTaskTrigger -Once -At 00:00
$settings = New-ScheduledTaskSettingsSet
$task = New-ScheduledTask -Action $action -Trigger $trigger -Settings $settings
Register-ScheduledTask godot_remote_debug -InputObject $task -Force:$true
Start-ScheduledTask -TaskName godot_remote_debug
while (Get-ScheduledTask -TaskName godot_remote_debug | ? State -eq running) { Start-Sleep -Milliseconds 100 }
Unregister-ScheduledTask -TaskName godot_remote_debug -Confirm:$false -ErrorAction:SilentlyContinue"
ssh_remote_deploy/cleanup_script="Stop-ScheduledTask -TaskName godot_remote_debug -ErrorAction:SilentlyContinue
Unregister-ScheduledTask -TaskName godot_remote_debug -Confirm:$false -ErrorAction:SilentlyContinue
Remove-Item -Recurse -Force '{temp_dir}'"
```

---

## Game.Godot.Tests 椤圭洰缁撴瀯

```
Game.Godot.Tests/
鈹溾攢鈹€ addons/
鈹?  鈹斺攢鈹€ gdUnit4/                        # GdUnit4 鎻掍欢
鈹?
鈹溾攢鈹€ Scenes/
鈹?  鈹溾攢鈹€ MainSceneTest.cs
鈹?  鈹溾攢鈹€ PlayerTest.cs
鈹?  鈹斺攢鈹€ UITest.cs
鈹?
鈹溾攢鈹€ Scripts/
鈹?  鈹溾攢鈹€ SignalTest.cs
鈹?  鈹斺攢鈹€ AdapterTest.cs
鈹?
鈹斺攢鈹€ E2E/
    鈹溾攢鈹€ E2ERunner.tscn                  # E2E 娴嬭瘯杩愯鍣ㄥ満鏅?
    鈹溾攢鈹€ E2ERunner.cs                    # E2E 娴嬭瘯鑴氭湰
    鈹斺攢鈹€ SmokeTests.cs                   # 鍐掔儫娴嬭瘯
```

---

## Visual Studio 瑙ｅ喅鏂规缁撴瀯

**Game.sln**:

```
Microsoft Visual Studio Solution File, Format Version 12.00
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Game.Core", "Game.Core\Game.Core.csproj", "{GUID-1}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Game.Core.Tests", "Game.Core.Tests\Game.Core.Tests.csproj", "{GUID-2}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Game.Godot", "Game.Godot\Game.Godot.csproj", "{GUID-3}"
EndProject
Global
    GlobalSection(SolutionConfigurationPlatforms) = preSolution
        Debug|Any CPU = Debug|Any CPU
        Release|Any CPU = Release|Any CPU
    EndGlobalSection
    GlobalSection(ProjectConfigurationPlatforms) = postSolution
        {GUID-1}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
        {GUID-1}.Release|Any CPU.ActiveCfg = Release|Any CPU
        {GUID-2}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
        {GUID-2}.Release|Any CPU.ActiveCfg = Release|Any CPU
        {GUID-3}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
        {GUID-3}.Release|Any CPU.ActiveCfg = Release|Any CPU
    EndGlobalSection
EndGlobal
```

---

## .gitignore

```gitignore
# Godot
.godot/
.mono/
data_*/
*.translation

# .NET
bin/
obj/
*.csproj.user
*.suo
*.cache
*.dll
*.exe
*.pdb

# Test & Coverage
TestResults/
coverage/
*.coverage
*.coveragexml

# Logs
logs/
*.log

# IDE
.vs/
.vscode/
.idea/
*.swp
*.swo

# OS
.DS_Store
Thumbs.db

# Build
build/
dist/
*.pck
```

---

## .gitattributes

```gitattributes
# Text files
*.cs text eol=lf
*.gd text eol=lf
*.tscn text eol=lf
*.tres text eol=lf
*.cfg text eol=lf
*.md text eol=lf
*.json text eol=lf
*.xml text eol=lf

# Binary files
*.png binary
*.jpg binary
*.ogg binary
*.wav binary
*.ttf binary
*.otf binary
```

---

## 鍒濆鍖栬剼鏈?

鍒涘缓 PowerShell 鑴氭湰鑷姩鍖栭」鐩垵濮嬪寲锛?

**scripts/init-godot-project.ps1**:

```powershell
# 椤圭洰鍒濆鍖栬剼鏈?

param(
    [string]$ProjectRoot = "C:\buildgame\newguild"
)

Write-Host "寮€濮嬪垵濮嬪寲 Godot 椤圭洰..." -ForegroundColor Green

# 鍒涘缓鐩綍缁撴瀯
$directories = @(
    "Game.Core/Domain/Entities",
    "Game.Core/Domain/ValueObjects",
    "Game.Core/Domain/Services",
    "Game.Core/Ports",
    "Game.Core/Utilities",
    "Game.Core.Tests/Domain/Entities",
    "Game.Core.Tests/Domain/Services",
    "Game.Core.Tests/Fakes",
    "Game.Core.Tests/TestHelpers",
    "Game.Godot/Scenes/UI",
    "Game.Godot/Scenes/Game",
    "Game.Godot/Scenes/Levels",
    "Game.Godot/Scripts",
    "Game.Godot/Autoloads",
    "Game.Godot/Adapters",
    "Game.Godot/Resources",
    "Game.Godot/Themes",
    "Game.Godot/Assets/Textures",
    "Game.Godot/Assets/Fonts",
    "Game.Godot/Assets/Audio",
    "Game.Godot.Tests/Scenes",
    "Game.Godot.Tests/Scripts",
    "Game.Godot.Tests/E2E",
    "docs/adr",
    "docs/architecture",
    "docs/contracts/signals",
    "docs/migration",
    "scripts/ci",
    "scripts/python",
    "scripts/godot",
    "logs/ci",
    "logs/security",
    "logs/performance"
)

foreach ($dir in $directories) {
    $fullPath = Join-Path $ProjectRoot $dir
    if (-not (Test-Path $fullPath)) {
        New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
        Write-Host "鍒涘缓鐩綍: $dir" -ForegroundColor Gray
    }
}

# 鍒涘缓 .NET 椤圭洰
Write-Host "`n鍒涘缓 .NET 椤圭洰..." -ForegroundColor Green

Push-Location $ProjectRoot

# Game.Core
if (-not (Test-Path "Game.Core/Game.Core.csproj")) {
    dotnet new classlib -n Game.Core -o Game.Core -f net8.0
    Write-Host "鍒涘缓 Game.Core 椤圭洰" -ForegroundColor Gray
}

# Game.Core.Tests
if (-not (Test-Path "Game.Core.Tests/Game.Core.Tests.csproj")) {
    dotnet new xunit -n Game.Core.Tests -o Game.Core.Tests -f net8.0
    dotnet add Game.Core.Tests reference Game.Core
    dotnet add Game.Core.Tests package FluentAssertions
    dotnet add Game.Core.Tests package NSubstitute
    dotnet add Game.Core.Tests package coverlet.collector
    Write-Host "鍒涘缓 Game.Core.Tests 椤圭洰" -ForegroundColor Gray
}

# 鍒涘缓瑙ｅ喅鏂规
if (-not (Test-Path "Game.sln")) {
    dotnet new sln -n Game
    dotnet sln add Game.Core/Game.Core.csproj
    dotnet sln add Game.Core.Tests/Game.Core.Tests.csproj
    Write-Host "鍒涘缓 Visual Studio 瑙ｅ喅鏂规" -ForegroundColor Gray
}

Pop-Location

Write-Host "`nGodot 椤圭洰鍒濆鍖栧畬鎴? -ForegroundColor Green
Write-Host "`n涓嬩竴姝ワ細" -ForegroundColor Yellow
Write-Host "1. 浣跨敤 Godot Editor 鎵撳紑 $ProjectRoot" -ForegroundColor White
Write-Host "2. 鍦?Godot 涓垱寤?C# 椤圭洰锛堜細鐢熸垚 Game.Godot.csproj锛? -ForegroundColor White
Write-Host "3. 灏?Game.Godot.csproj 娣诲姞鍒?Game.sln" -ForegroundColor White
```

**鎵ц鍒濆鍖?*:

```powershell
pwsh scripts/init-godot-project.ps1 -ProjectRoot "C:\buildgame\newguild"
```

---

## 瀹屾垚鏍囧噯

- [ ] 鐩綍缁撴瀯宸插垱寤?
- [ ] Game.Core 椤圭洰宸插垵濮嬪寲
- [ ] Game.Core.Tests 椤圭洰宸插垵濮嬪寲
- [ ] Game.sln 瑙ｅ喅鏂规宸插垱寤?
- [ ] .gitignore 鍜?.gitattributes 宸查厤缃?
- [ ] Godot Editor 鍙墦寮€椤圭洰
- [ ] Game.Godot.csproj 宸茬敓鎴愶紙Godot 鑷姩鍒涘缓锛?
- [ ] `dotnet build` 缂栬瘧閫氳繃
- [ ] `dotnet test` 杩愯閫氳繃锛堝垵濮嬫祴璇曚负绌猴級

---

## 涓嬩竴姝?

瀹屾垚鏈樁娈靛悗锛岀户缁細

鉃★笍 [Phase-4-Domain-Layer.md](Phase-4-Domain-Layer.md) 鈥?绾?C# 棰嗗煙灞傝縼绉
