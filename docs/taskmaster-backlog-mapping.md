---
Title: Taskmaster Backlog ↔ NG 任务映射索引（newguild）
Status: Draft
ADR-Refs:
  - ADR-0018  # Godot 4.5 + C# 技术栈
  - ADR-0019  # Godot 安全基线
  - ADR-0004  # 事件总线与信号命名
  - ADR-0005  # 质量门禁
  - ADR-0015  # 性能预算与门禁
  - ADR-0003  # 可观测性与 Release Health
---

> 用途：把 docs/migration 下的 Phase Backlog 与 .taskmaster/tasks/tasks_back.json
> 中的 NG-任务之间的对应关系说清楚，避免后续在迁移/开发时误以为
> “Backlog 里的每一条都已经一一落盘为 Taskmaster 任务”。本文件本身
> 不是 SSoT，只是导航索引；真正的 SSoT 仍然是 Base 文档 + ADR + Tasks。

## 1. Phase Backlog 与 NG-任务的分工

- Backlog（docs/migration/Phase-*.md）
  - 记录从旧项目迁移到 newguild Godot+C# 模板过程中的“潜在增量工作”。
  - 含有分 Phase、分 B1/B2/B3… 的细粒度愿望清单，刻意比当前模板 DoD 更“饱满”。
- Taskmaster 任务（.taskmaster/tasks/tasks_back.json）
  - 只收敛成 **当前 newguild 模板真正要做的 Story**；
  - 一部分 Backlog 被精炼成 NG-00xx（骨干），另一部分被合并成汇总任务，
    还有一些被放入长期 backlog（tasks_longterm.json）。

换句话说：**Backlog ≥ 任务 ≥ 实际已实现代码**，不能反过来理解。

## 2. 已入库的 Backlog 条目（短期/中期）

本节只列出 Phase 9/13/15/16/17 中已经在 tasks_back.json 里有专门 Story 的条目：

### 2.1 Phase 9 – Signal System Backlog

- 文档：docs/migration/Phase-9-Signal-Backlog.md
- 已入 Taskmaster 的条目：
  - B1：事件命名统一迁移（`game.*` → `core.*.*`）
    - 任务：`NG-0023`（story_id: `PH9-BACKLOG-B1`）
    - 代码落点：
      - Game.Core.Tests/Engine/GameEngineCoreEventTests.cs（事件类型常量）
    - 文档落点：
      - docs/architecture/overlays/PRD-Guild-Manager/08/08-Contracts-Guild-Manager-Events.md
      - docs/architecture/overlays/PRD-Guild-Manager/08/08-功能纵切-公会管理器.md

B2–B5 目前只在 `docs/migration/Phase-9-Signal-Backlog.md` + tasks_longterm.json
（NG-1001..NG-1004）中存在，用作以后增强与治理的候选；不视作模板当前 DoD。

### 2.2 Phase 13 – Quality Gates Backlog

- 文档：docs/migration/Phase-13-Quality-Gates-Backlog.md
- 已入 Taskmaster 的 B 条目：
  - B1/B2/B5：质量脚本骨干（覆盖率 + GdUnit 集成 + 架构门禁）
    - 任务：`NG-0013`（旧任务，已在 tasks_back.json 中）
  - B3：代码重复率与圈复杂度门禁
    - 任务：`NG-0027`（story_id: `PH13-BACKLOG-B3`）
  - B4：性能 P95 与审计 JSONL 校验
    - 任务：`NG-0028`（story_id: `PH13-BACKLOG-B4`）

### 2.3 Phase 15 – Performance Budgets Backlog

- 文档：docs/migration/Phase-15-Performance-Budgets-Backlog.md
- 已入 Taskmaster 的 B 条目：
  - B1/B2：性能采集基础设施（PerformanceTracker + Python 聚合脚本）
    - 任务：`NG-0015`
  - B3：性能报告与历史追踪（reports/performance/**）
    - 任务：`NG-0030`（story_id: `PH15-BACKLOG-B3`）
  - B5：独立性能门禁 CI 工作流（performance-gates.yml）
    - 任务：`NG-0031`（story_id: `PH15-BACKLOG-B5`）

B4（更细粒度指标，如 GC/Signal/DB）作为长期优化，收敛到 tasks_longterm.json
中的 `NG-1005`，对模板本身不是当前 DoD 必做项。

### 2.4 Phase 16 – Observability Backlog

- 文档：docs/migration/Phase-16-Observability-Backlog.md
- 已入 Taskmaster 的 B 条目：
  - B1：Observability Autoload + Release Health Gate（Godot 层）
    - 任务：`NG-0016`
  - B2：Game.Core Observability 客户端与结构化日志
    - 任务：`NG-0024`（story_id: `PH16-BACKLOG-B2`）
  - B4：隐私与合规文档（privacy-compliance.md）
    - 任务：`NG-0025`（story_id: `PH16-BACKLOG-B4`）
  - B5：日志使用规范（logging-guidelines.md）
    - 任务：`NG-0026`（story_id: `PH16-BACKLOG-B5`）

> 补充：
> - 针对“从 Sentry/错误反馈自动生成 Taskmaster 草案”这一需求，本仓库在 tasks_back.json 中新增了 `NG-0037`（story_id: `PH16-BACKLOG-TASKMASTER-SENTRY-FEEDBACK`）。
> - 该任务只负责设计 **Sentry → Taskmaster 草案** 的自动回链骨架（Python 脚本 + 非阻断 CI Job + logs/ci/**/taskmaster/** 工件），并明确所有草案必须经过人工审阅后才能被提升为正式 NG/GM 任务，避免将噪音或未脱敏字段直接写入任务 SSoT。

### 2.5 Phase 17 – Build / Export Backlog

- 文档：docs/migration/Phase-17-Build-Backlog.md
- 已入 Taskmaster 的 B 条目：
  - B1/B2/B3：Python 构建脚本 + 版本元数据 + Release Workflow
    - 任务：`NG-0017`
  - B4：代码签名与安全分发
    - 任务：`NG-0032`（story_id: `PH17-BACKLOG-B4`）
  - B5：导出预设与多配置支持
    - 任务：`NG-0033`（story_id: `PH17-BACKLOG-B5`）

## 3. 长期 backlog：已建模但不计入当前 DoD

以下 backlog 条目已在 `.taskmaster/tasks/tasks_longterm.json` 中建模为 NG-100x
系列任务，用于后续治理与增强：

- Phase 9：
  - B2：Signal XML 文档注释补全 → `NG-1001`
  - B3：Signal 性能基准测试 → `NG-1002`
  - B4：CI Signal 合规检查 workflow → `NG-1003`
  - B5：GDScript 订阅生命周期管理 → `NG-1004`
- Phase 15：
  - B4：更细粒度的性能指标采集（GC/Signal/DB 等）→ `NG-1005`

这些任务默认不算在 newguild 模板的 DoD 里，只在需要深度治理时再提
升为“必须完成”的 Story。

## 4. 校验方式（Task Links Validate）

为了避免日后 Backlog/Tasks/Base/ADR 口径漂移，本仓库提供一个简单的
校验脚本：

- 命令：
  - `py -3 scripts/python/task_links_validate.py`
- 当前行为：
  - 调用 `check_tasks_back_references.run_check(root)`，检查：
    - `tasks_back.json` 中 `NG-0023..NG-0033` 的 `adr_refs` 是否都指向真实存在的
      ADR 文件；
    - 这些任务的 `chapter_refs` 是否与 ADR→Chapter 映射（ADR-000x → CHyy）一致；
    - `overlay_refs`（如果有）是否指向真实存在的 overlay 文件。
  - 如有缺失或不一致，则返回非零退出码，可作为 CI 的 `task-links-validate`
    步骤使用。

> 提醒：后续如果在 tasks_back.json 中新增 NG-任务，建议：
> 1. 先更新 ADR-Refs 与 story_id；
> 2. 使用 `fix_tasks_back_chapters.py` 自动补齐 chapter_refs；
> 3. 再跑一遍 `task_links_validate.py` 确认通过；
> 4. 最后在 PR 描述中引用对应的 Phase Backlog 与 ADR，保持“文档↔任务↔代码”
>    三者一致。
