# AI 助手与集成索引

> 说明：本索引由 scripts/python/update_ai_index.py 自动生成，汇总与 AI 助手、Taskmaster/SuperClaude 工作流相关的关键文档与命令。

| 类别 | 标题 | 路径 |
| ---- | ---- | ---- |
| architecture | Repository Guidelines | `AGENTS.md` |
| assistant | CLAUDE.md | `CLAUDE.md` |
| mcp | 🛠️ 工具使用指南 | `mcpuse.md` |
| workflow | Task Master + SuperClaude 联合使用最佳实践 | `docs/workflows/task-master-superclaude-integration.md` |
| index | 项目完整文档索引 | `docs/PROJECT_DOCUMENTATION_INDEX.md` |
| command | Tests/Scenes/test_{{组件名称}}.gd | `.claude/commands/godot-component.md` |
| command | godot-scene.md | `.claude/commands/godot-scene.md` |
| command | godot-script.md | `.claude/commands/godot-script.md` |

## 使用建议

- 修改 AGENTS/CLAUDE/工作流文档或 .claude/commands/** 后，可运行 `py -3 scripts/python/update_ai_index.py --write` 更新本索引。
- CI 中会在 `windows-ci` 工作流中以非阻断方式调用该脚本，并将日志写入 `logs/ci/<date>/ai-index/`。
