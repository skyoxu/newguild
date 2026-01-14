# Contracts Catalog（契约目录）生成与使用指南（newguild）

本项目提供脚本生成一份“契约目录（Contracts Catalog）”，用于在开工前快速对齐以下几件事：

- 当前仓库 `Game.Core/Contracts/**` 中已落盘的 `EventType`（领域事件稳定标识，CloudEvents type）
- 任务视图（`.taskmaster/tasks/tasks_back.json` / `.taskmaster/tasks/tasks_gameplay.json`）中声明的 `contractRefs[]`
- Core 的 `Ports/Services/Repositories`（公共接口索引，便于避免重复造轮子）

重要：

- **契约的单一事实源（SSoT）永远是 `Game.Core/Contracts/**`。**
- `contractRefs` 只用于**领域事件**（或安全域事件）对齐；门禁/产物锚点请使用视图任务的 `artifactRefs`（允许占位，不能混入 `Test-Refs` 的“必须存在”规则）。
- 生成目录属于审计材料，默认写入 `logs/ci/<YYYY-MM-DD>/`，不建议入库。

## 推荐用法（Windows）

1) 生成契约目录（默认输出到 `logs/ci/<date>/contracts-catalog/`）：

```powershell
py -3 scripts/python/generate_contracts_catalog.py --prd-id PRD-Guild-Manager --domain-prefixes core,security
```

说明：

- `--domain-prefixes` 默认是 `core,security`（可通过环境变量 `DOMAIN_PREFIXES` 覆盖）。
- 如果你只想看领域事件：`--domain-prefixes core`
- 如果你只想看安全事件：`--domain-prefixes security`

2) 校验 Overlay ↔ Contracts 回链（硬规则建议用它做门禁）：

```powershell
py -3 scripts/python/validate_contracts.py
```

3) 校验“视图 contractRefs 是否都能解析到 Contracts 的 EventType 常量”：

```powershell
py -3 scripts/python/validate_view_ref_semantics.py
py -3 scripts/python/audit_view_contractrefs_vs_contracts.py
```

## 生成产物与版本控制规则

- 生成产物默认写入 `logs/ci/<YYYY-MM-DD>/...`，作为 CI/本地审计证据。
- 不建议把带业务事件列表的“契约目录”长期写入 `docs/` 作为入口文档，避免项目演进中产生误导。

## 常见问题（止损）

1) 目录里出现 “Task refs that do NOT resolve to Contracts”

- 这表示某些视图任务的 `contractRefs` 指向的 `EventType` 在 `Game.Core/Contracts/**` 中不存在。
- 修复路线二选一：
  - 修正 `contractRefs` 为真实存在的事件；或
  - 补齐缺失的契约文件（新增强类型契约 + `EventType` 常量），并在 Overlay 08 中登记。

2) `contractRefs` 应该怎么填才算“最小覆盖”？

- **UI 订阅事件的任务**：`contractRefs` 必须覆盖 UI 真正消费的事件（例如在 `switch (evt.Type)` / `== Xxx.EventType` 中出现的事件）。
- **Core 计算点任务**：`contractRefs` 必须覆盖会发布的事件（例如通过 `EventBus.PublishAsync` 发出的事件）。

