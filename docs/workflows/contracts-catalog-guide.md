# Contracts Catalog（契约目录）生成与使用指南（newguild）

本项目提供脚本生成一份“Contracts Catalog”，用于在开工前快速对齐：
- `Game.Core/Contracts/**` 下已落盘的 `EventType` 常量（CloudEvents-like `type`）
- 任务视图（`.taskmaster/tasks/tasks_back.json` / `.taskmaster/tasks/tasks_gameplay.json`）中声明的 `contractRefs[]`
- Core 的公开接口索引（`Ports/Services/Repositories`），用于避免重复造轮子

重要口径（止损）：
- Contracts 的单一事实源（SSoT）永远是 `Game.Core/Contracts/**`。
- `contractRefs` 只用于对齐“领域/安全事件”的 `EventType`；门禁产物锚点请用视图任务的 `artifactRefs`（允许占位，不要混进 `test_refs` 的“必须存在”规则）。
- Catalog 属于审计材料，默认写入 `logs/ci/<YYYY-MM-DD>/`，不建议入库。

## 推荐用法（Windows PowerShell）

1) 生成契约目录（默认输出到 `logs/ci/<date>/contracts-catalog/`）：

```powershell
py -3 scripts/python/generate_contracts_catalog.py --prd-id PRD-Guild-Manager --domain-prefixes core,security
```

说明：
- `--prd-id` 仅用于目录标题/分组标签（命名空间/范围 ID），不要求与 PRD 文档强绑定。
- `--domain-prefixes` 默认是 `core,security`（可用环境变量 `DOMAIN_PREFIXES` 覆盖）。

2) 校验 Contracts 回链（建议作为硬门禁的一部分）：

```powershell
py -3 scripts/python/validate_contracts.py
```

3) 校验“视图 contractRefs 能否解析到 Contracts 的 EventType 常量”：

```powershell
py -3 scripts/python/validate_view_ref_semantics.py
py -3 scripts/python/audit_view_contractrefs_vs_contracts.py
```

## 常见问题（正解）

1) Catalog 里出现 `Task refs that do NOT resolve to Contracts`
- 表示某些视图任务的 `contractRefs` 指向的 `EventType` 在 `Game.Core/Contracts/**` 中不存在。
- 修复二选一：
  - 修正 `contractRefs` 为真实存在的 `EventType`；或
  - 补齐缺失的契约文件（新增强类型契约 + `EventType` 常量），并在 overlays 08 中记录。

2) `contractRefs` 如何填才算“最小覆盖定义”
- UI 订阅事件的任务：`contractRefs` 必须覆盖 UI 真正消费的事件（例如在 `switch (evt.Type)` 或 `== Xxx.EventType` 中出现的事件）。
- Core 计算点任务：`contractRefs` 必须覆盖会发布的事件（例如通过 `EventBus.PublishAsync` 发出的事件）。

