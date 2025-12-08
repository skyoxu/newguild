[![Windows Export Slim](https://github.com/skyoxu/newguild/actions/workflows/windows-export-slim.yml/badge.svg)](https://github.com/skyoxu/newguild/actions/workflows/windows-export-slim.yml) [![Windows Release](https://github.com/skyoxu/newguild/actions/workflows/windows-release.yml/badge.svg)](https://github.com/skyoxu/newguild/actions/workflows/windows-release.yml) [![Windows Quality Gate](https://github.com/skyoxu/newguild/actions/workflows/windows-quality-gate.yml/badge.svg)](https://github.com/skyoxu/newguild/actions/workflows/windows-quality-gate.yml)

# Godot game (C#)

Godot 4.5.1 + .NET（Windows-only）游戏项目。

## 3‑Minute From Zero to Export（3 分钟从 0 到导出）

1) 安装 Godot .NET（mono）并设置环境：
   - `setx GODOT_BIN C:\Godot\Godot_v4.5.1-stable_mono_win64.exe`
2) 运行最小测试与冒烟（可选示例）：
   - `./scripts/test.ps1 -GodotBin "$env:GODOT_BIN"`（默认不含示例；`-IncludeDemo` 可启用）
   - `./scripts/ci/smoke_headless.ps1 -GodotBin "$env:GODOT_BIN"`
3) 在 Godot Editor 安装 Export Templates（Windows Desktop）。
4) 导出与运行 EXE：
   - `./scripts/ci/export_windows.ps1 -GodotBin "$env:GODOT_BIN" -Output build\Game.exe`
   - `./scripts/ci/smoke_exe.ps1 -ExePath build\Game.exe`

One‑liner（已在 Editor 安装 Export Templates 后）：
- PowerShell：`$env:GODOT_BIN='C:\\Godot\\Godot_v4.5.1-stable_mono_win64.exe'; ./scripts/ci/export_windows.ps1 -GodotBin "$env:GODOT_BIN" -Output build\Game.exe; ./scripts/ci/smoke_exe.ps1 -ExePath build\Game.exe`

## What You Get（内容）
- 适配层 Autoload：EventBus/DataStore/Logger/Audio/Time/Input/SqlDb
- 场景分层：ScreenRoot + Overlays；ScreenNavigator（淡入淡出 + Enter/Exit 钩子）
- 安全基线：仅允许 `res://`/`user://` 读取，启动审计 JSONL，HTTP 验证示例
- 可观测性：本地 JSONL（Security/Sentry 占位），性能指标（[PERF] + perf.json）
- 测试体系：xUnit + GdUnit4（示例默认关闭），一键脚本
- 导出与冒烟：Windows-only 脚本与文档

## Quick Links
- 文档索引：`docs/PROJECT_DOCUMENTATION_INDEX.md`
- Godot+C# 快速开始（newguild 项目）：`docs/TEMPLATE_GODOT_GETTING_STARTED.md`
- Windows-only 快速指引：`docs/migration/Phase-17-Windows-Only-Quickstart.md`
- FeatureFlags 快速指引：`docs/migration/Phase-18-Staged-Release-and-Canary-Strategy.md`
- 导出清单：`docs/migration/Phase-17-Export-Checklist.md`
- Headless 冒烟：`docs/migration/Phase-12-Headless-Smoke-Tests.md`
 - Actions 快速链路验证（Dry Run）：`.github/workflows/windows-smoke-dry-run.yml`
- 场景设计：`docs/migration/Phase-8-Scene-Design.md`
- 测试体系：`docs/migration/Phase-10-Unit-Tests.md`
- 安全基线：`docs/migration/Phase-14-Godot-Security-Baseline.md`
 - 手动发布指引：`docs/release/WINDOWS_MANUAL_RELEASE.md`

## Notes
- DB 后端：默认插件优先；`GODOT_DB_BACKEND=plugin|managed` 可控。
- 示例 UI/测试：默认关闭；设置 `TEMPLATE_DEMO=1` 启用（Examples/**）。

## 仓库名与内部项目名对应关系

- GitHub 仓库名：`skyoxu/newguild` —— 对外链接、Actions badge、协作入口统一使用此名。
- Godot 主工程：`GodotGame.csproj` / `GodotGame.sln` —— Godot .NET 入口工程，CI 构建命令中仍使用该名称。
- 领域与适配层工程：`Game.Core`、`Game.Godot`、`Game.Core.Tests`、`Game.Godot.Tests` 等 —— .NET 解决方案内部的逻辑拆分。
- 约定：文档或脚本中提到 “newguild” 时，指当前这一套模板/游戏项目；提到 `GodotGame` 时，指该项目内的 Godot .NET 主工程文件，而不是另一个仓库。

## Feature Flags（特性旗标）
- Autoload：`/root/FeatureFlags`（文件：`Game.Godot/Scripts/Config/FeatureFlags.cs`）
- 环境变量优先生效：
  - 单项：`setx FEATURE_demo_screens 1`
  - 多项：`setx GAME_FEATURES "demo_screens,perf_overlay"`
- 文件配置：`user://config/features.json`（示例：`{"demo_screens": true}`）
- 代码示例：`if (FeatureFlags.IsEnabled("demo_screens")) { /* ... */ }`

## 如何发版（打 tag）
- 确认主分支已包含所需变更：`git status && git push`
- 创建版本标签：`git tag v0.1.1 -m "v0.1.1 release"`
- 推送标签触发发布：`git push origin v0.1.1`
- 工作流：`Windows Release (Tag)` 自动导出并将 `build/Game.exe` 附加到 GitHub Release。
- 如需手动导出：运行 `Windows Release (Manual)` 或 `Windows Export Slim`。

## 自定义应用元数据（图标/公司/描述）
- 文件：`export_presets.cfg` → `[preset.0.options]` 段。
- 关键字段：
  - `application/product_name`（产品名），`application/company_name`（公司名）
  - `application/file_description`（文件描述），`application/*_version`（版本）
  - 图标：`application/icon`（推荐 ICO：`res://icon.ico`；当前为 `res://icon.svg`）
- 修改后，运行 `Windows Export Slim` 或 `Windows Release (Manual)` 验证导出产物。

