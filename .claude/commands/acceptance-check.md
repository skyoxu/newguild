---
name: acceptance-check
description: 执行架构级验收检查（Multi-Subagent Orchestration）
---

# Acceptance Check (Architecture Verification)

执行多 Subagent 协作的综合架构验收，确保任务实现满足所有 ADR 要求和质量标准。

## Usage

```bash
/acceptance-check <task-id>
```

**示例**：
```bash
/acceptance-check 1.1
/acceptance-check 2.3
```

## Multi-Subagent Workflow

此命令自动协调以下 6 个专业 Subagents 进行全面验收：

### 1️⃣ adr-compliance-checker（项目特定）
**职责**: ADR 合规性验证
**检查项**:
- ADR-0002: 安全基线（路径使用、外链白名单、配置开关）
- ADR-0004: 事件契约（命名规范、CloudEvents 字段、契约位置）
- ADR-0005: 质量门禁（覆盖率、重复度、测试通过）
- ADR-0011: Windows-only 策略（文档标注、禁止跨平台抽象）

**输出**: ADR 合规性报告（通过/失败 + 具体违规项）

---

### 2️⃣ performance-slo-validator（项目特定）
**职责**: 性能 SLO 验证
**检查项**:
- 启动时间 ≤3s
- 帧耗时 P95 ≤16.6ms (60 FPS)
- 初始内存 ≤500MB
- 峰值内存 ≤1GB
- 内存增长率 ≤5% /小时

**数据来源**: `logs/perf/<latest>/summary.json`
**输出**: 性能 SLO 报告（通过/失败 + 趋势分析）

---

### 3️⃣ architect-reviewer（社区 - lst97）
**职责**: 架构一致性审查
**检查项**:
- 架构模式遵循（MVC、Ports & Adapters、Event-Driven）
- SOLID 原则合规
- 依赖方向正确（业务逻辑不依赖基础设施）
- 无循环依赖
- 适当的抽象层次

**输出**: 架构审查报告（High/Medium/Low 影响评级）

---

### 4️⃣ code-reviewer（社区 - lst97）
**职责**: 代码质量审查
**检查项**:
- 🚨 Critical & Security（漏洞、认证授权、输入验证）
- ⚠️ Quality & Best Practices（DRY、SOLID、测试覆盖）
- 💡 Performance & Maintainability（算法效率、资源管理）

**输出**: 代码审查报告（Critical/Warning/Suggestion 分级）

---

### 5️⃣ security-auditor（社区 - lst97）
**职责**: 安全审计
**检查项**:
- OWASP Top 10 覆盖
- 威胁建模验证
- 加密实现审查
- 依赖漏洞检测
- 合规框架验证（NIST、ISO 27001）

**输出**: 安全审计报告（Critical/High/Medium/Low 风险分级）

---

### 6️⃣ test-automator（社区 - lst97）
**职责**: 测试质量验证
**检查项**:
- 测试金字塔比例（Unit 80% / Integration 15% / E2E 5%）
- 覆盖率门禁（90% lines / 85% branches）
- 测试确定性（无 flaky tests）
- 测试命名规范
- Mock/Stub 使用正确

**输出**: 测试质量报告（通过/失败 + 覆盖率详情）

---

## Execution Flow

```mermaid
graph TD
    A[/acceptance-check task-id] --> B[读取任务元数据]
    B --> C[提取 adrRefs 字段]
    C --> D[并行调用 6 个 Subagents]

    D --> E1[adr-compliance-checker]
    D --> E2[performance-slo-validator]
    D --> E3[architect-reviewer]
    D --> E4[code-reviewer]
    D --> E5[security-auditor]
    D --> E6[test-automator]

    E1 --> F[汇总报告]
    E2 --> F
    E3 --> F
    E4 --> F
    E5 --> F
    E6 --> F

    F --> G{所有检查通过?}
    G -->|是| H[✅ PASS - 可标记 done]
    G -->|否| I[❌ FAIL - 标记 blocked]

    I --> J[生成 blockers 列表]
    J --> K[返回修复建议]
```

## Detailed Workflow Steps

### Step 1: 读取任务元数据

```bash
# 从 tasks.json 读取任务信息
task=$(cat .taskmaster/tasks/tasks.json | jq '.[] | select(.id=="'$task_id'")')

# 提取关键字段
adr_refs=$(echo "$task" | jq -r '.adrRefs[]')
overlay=$(echo "$task" | jq -r '.overlay')
title=$(echo "$task" | jq -r '.title')
```

### Step 2: 调用 Subagents（并行）

```bash
# 6 个 Subagents 并行执行
{
  Use adr-compliance-checker to verify task $task_id
} &

{
  Use performance-slo-validator to check latest perf results
} &

{
  Use architect-reviewer to review architectural consistency
} &

{
  Use code-reviewer to review code quality and security
} &

{
  Use security-auditor to audit security compliance
} &

{
  Use test-automator to validate test coverage and quality
} &

wait  # 等待所有 Subagents 完成
```

### Step 3: 汇总报告

整合所有 Subagent 的输出，生成综合验收报告。

### Step 4: 判定结果

- **PASS**: 所有 Subagents 报告通过
- **FAIL**: 任一 Subagent 报告失败或存在 Critical 问题

---

## Output Format

```markdown
# 综合架构验收报告 - Task {task_id}

**任务**: {task_title}
**验收日期**: {date}
**最终结果**: {PASS/FAIL}

---

## 验收汇总

| Subagent | 状态 | Critical | High | Medium | Low |
|---------|------|----------|------|--------|-----|
| adr-compliance-checker | ❌ FAIL | 2 | 0 | 0 | 0 |
| performance-slo-validator | ✅ PASS | 0 | 0 | 0 | 0 |
| architect-reviewer | ✅ PASS | 0 | 0 | 1 | 2 |
| code-reviewer | ⚠️ WARN | 0 | 1 | 2 | 3 |
| security-auditor | ✅ PASS | 0 | 0 | 0 | 1 |
| test-automator | ✅ PASS | 0 | 0 | 0 | 0 |

**统计**:
- ✅ 通过: 4 个 Subagents
- ⚠️ 警告: 1 个 Subagent (有 High 级别问题)
- ❌ 失败: 1 个 Subagent (有 Critical 问题)

---

## 阻断问题（必须修复）

### 🚨 Critical Issues (2)

#### 1. ADR-0002 违规: 绝对路径使用
**来源**: adr-compliance-checker
**位置**: Scripts/Services/ConfigLoader.cs:78
**问题**:
```csharp
var path = "C:/config.json";  // 违规！
```
**修复**:
```csharp
var path = "user://config.json";  // 使用 Godot 路径
```

#### 2. ADR-0004 违规: CloudEvents 字段缺失
**来源**: adr-compliance-checker
**位置**: Scripts/Core/Contracts/Guild/GuildCreated.cs:15
**问题**: 缺少 Source, Subject, Id 字段
**修复**:
```csharp
public string Source { get; init; } = "/guilds/service";
public string Subject { get; init; }
public string Id { get; init; }
```

---

## 警告问题（建议修复）

### ⚠️ High Issues (1)

#### 1. 潜在 SQL 注入风险
**来源**: code-reviewer
**位置**: Scripts/Services/GuildRepository.cs:102
**问题**: 字符串拼接构建 SQL 查询
**建议**: 使用参数化查询

---

## 详细报告

### 1. ADR 合规性检查
<details>
<summary>展开完整报告</summary>

[adr-compliance-checker 的完整输出]

</details>

### 2. 性能 SLO 验证
<details>
<summary>展开完整报告</summary>

[performance-slo-validator 的完整输出]

</details>

### 3. 架构一致性审查
<details>
<summary>展开完整报告</summary>

[architect-reviewer 的完整输出]

</details>

### 4. 代码质量审查
<details>
<summary>展开完整报告</summary>

[code-reviewer 的完整输出]

</details>

### 5. 安全审计
<details>
<summary>展开完整报告</summary>

[security-auditor 的完整输出]

</details>

### 6. 测试质量验证
<details>
<summary>展开完整报告</summary>

[test-automator 的完整输出]

</details>

---

## 修复指南

### 立即修复（阻断合并）
1. ConfigLoader.cs 改用 user:// 路径
2. GuildCreated.cs 添加 CloudEvents 字段

### 建议修复（不阻断）
1. GuildRepository.cs 改用参数化查询

### 修复后操作
```bash
# 修复代码后重新验收
/acceptance-check {task_id}

# 如果通过，标记任务完成
task-master set-status --id={task_id} --status=done
```

---

## 最终判定

❌ **FAIL** - 存在 2 个 Critical 阻断问题

**下一步**:
1. 修复上述 2 个 Critical 问题
2. 重新运行 `/acceptance-check {task_id}`
3. 通过后执行 `task-master set-status --id={task_id} --status=done`
```

---

## Implementation Notes

### Subagent 优先级
1. **adr-compliance-checker**: 最高优先级（ADR 是口径 SSoT）
2. **security-auditor**: 次高优先级（安全问题不可妥协）
3. **performance-slo-validator**: 高优先级（性能退化需阻断）
4. **architect-reviewer**: 中优先级（架构一致性重要但可讨论）
5. **code-reviewer**: 中优先级（质量问题分级处理）
6. **test-automator**: 基础优先级（测试是质量保障基础）

### 失败判定规则
- 任一 Subagent 报告 **Critical** 问题 → **FAIL**
- 多个 Subagent 报告 **High** 问题 → **FAIL**
- 仅 **Medium/Low** 问题 → **PASS with Warnings**

### 并行执行优化
- 所有 Subagents 并行调用以减少总耗时
- 预期总耗时：30-60 秒（取决于代码规模）
- 单独调用需 5-10 分钟（串行）

### 报告存储
所有验收报告保存至：
```
logs/acceptance/
├── 2025-11-29/
│   ├── task-1.1-acceptance.md
│   ├── task-1.2-acceptance.md
│   └── ...
└── 2025-11-30/
    └── ...
```

---

## Best Practices

### 何时运行验收检查
- ✅ 完成任务后，标记 `done` 之前
- ✅ 重构后验证架构一致性
- ✅ 提交 PR 前最终检查
- ✅ 发布前质量守门

### 如何处理失败
1. **Critical 问题**: 必须修复，不可合并
2. **High 问题**: 强烈建议修复
3. **Medium 问题**: 建议修复或记录技术债
4. **Low 问题**: 可延后处理

### 持续改进
- 定期审查验收标准的合理性
- 根据团队反馈调整 Subagent 配置
- 更新 ADR 以反映新的最佳实践

---

## Troubleshooting

### Subagent 调用失败
```bash
# 检查 Subagent 是否正确安装
ls ~/.claude/agents/lst97/
ls .claude/agents/

# 应看到：
# - architect-reviewer.md
# - code-reviewer.md
# - security-auditor.md
# - test-automator.md
# - adr-compliance-checker.md
# - performance-slo-validator.md
```

### 性能报告缺失
```bash
# 运行性能测试生成报告
py -3 scripts/python/perf_smoke.py --scene res://scenes/Main.tscn

# 验证报告存在
ls logs/perf/$(date +%Y-%m-%d)/summary.json
```

### 任务元数据缺失 adrRefs
```bash
# 手动添加 adrRefs 到任务
task-master update-task --id=1.1 --prompt="Add adrRefs: ADR-0002, ADR-0004, ADR-0005"
```
