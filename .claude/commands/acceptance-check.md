---
name: acceptance-check
description: 执行架构级验收检查（Subagents）
---

# Acceptance Check (Architecture Verification)

执行 Subagents 架构验收，基于任务的 overlay 字段加载 ACCEPTANCE_CHECKLIST.md。

## Usage

/acceptance-check <task-id>

## Workflow

1. 读取 `.taskmaster/tasks/*.json` 中对应 task 的 overlay 字段
2. 加载对应的 ACCEPTANCE_CHECKLIST.md
3. 执行架构级检查清单（50+ 条）：
   - ADR-0004 事件契约合规性（命名规范、CloudEvents 字段）
   - Godot 安全基线（res:// 和 user:// 路径使用）
   - 性能 SLO（帧耗时 P95 ≤ 16.6ms）
   - C# 契约文件验证（Scripts/Core/Contracts/**）
   - ADR 关联验证（引用的 ADR 是否 Accepted 状态）
4. 生成验收报告，标注通过/失败项及具体文件行号

## Output Format

```markdown
## 架构验收报告

### ADR-0004 事件契约合规性
- ✅ 事件命名：core.guild.created (符合 ${DOMAIN_PREFIX}.<entity>.<action>)
- ✅ 契约位置：Scripts/Core/Contracts/Guild/GuildCreated.cs
- ❌ CloudEvents 字段缺失：Type 字段未定义

### Godot 安全基线（ADR-0002）
- ✅ 仅使用 res:// 和 user:// 路径
- ✅ 无绝对路径引用

### 性能 SLO
- ✅ 帧耗时 P95：14.2ms（门禁 ≤ 16.6ms）

### ADR 关联验证
- ✅ 任务引用的 ADR-0002, ADR-0004 均为 Accepted 状态

### 总结
- 通过：4 项
- 失败：1 项（CloudEvents 字段缺失）
- **验收结果：FAIL**（需修复后重新验收）
```

## Implementation Details

- 使用 Subagents read + analyze 模式
- 优先检查 ADR Accepted 状态（读取 docs/adr/ 目录）
- 性能 SLO 检查：解析 logs/perf/ 目录的 summary.json
- 事件契约检查：扫描 Scripts/Core/Contracts/** 目录
- 路径检查：grep 扫描 Scripts/** 查找非 res:// 和 user:// 的文件系统调用
