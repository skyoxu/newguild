# Stage Content Inventory（T2 已完成 / T3 已规划）

## 目的

这份清单用于把 **PRD 语义**、**Overlay 验收锚点**、**Tasks（SSoT）** 对齐到“可玩内容集合”的视角，避免出现：

- 任务完成但可玩内容缺失
- 内容存在但缺少测试夹具/门禁引用
- 存档/迁移/稳定 ID 未定义导致后期返工

注意：任务状态与依赖以 `.taskmaster/tasks/tasks.json` 为准；本文件不是任务 SSoT。

## T2（已完成）的内容集合（最小可玩）

### 系统骨架

- 回合循环（Resolution/Player/AI phases）
- 事件引擎（事件队列/分发/最小契约）
- 安全基线（外链白名单、文件路径守卫、审计 JSONL）
- 测试与门禁（xUnit + GdUnit4 + headless + 质量门禁脚本）

### 交付视角（玩家体验）

- 可以启动并跑过最小回合推进
- 可以看到事件/状态变化的最小 UI/输出（以当前实现为准）
- CI 可以在 Windows 上复现（headless）

## T3（已规划）的内容集合（下一阶段）

### 玩法模块（按任务分组）

- 成员管理 UI（Roster）
- 招募系统（Recruitment）
- NPC 公会生态（AI Ecosystem）
- PVE 副本（Raid）
- 社交系统（Social）
- 媒体与声望（Media）

### T3 的跨切面前置能力

- Save/Load + Schema Migration（必须先于大规模内容扩展）
- 稳定 ID 与版本策略（内容/事件/存档）
- 最小内容模板与校验门禁（避免内容无法被自动化验证）

