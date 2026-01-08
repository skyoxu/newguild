# ID and Versioning Strategy（内容/事件/存档）

> 说明：本文件按“Schema.txt/ID 命名空间”模板做了本项目适配，用于在进入 T3 前先固定稳定标识与版本口径。

## 1) 事件 type（CloudEvents）

- 事件 type 由 Contracts SSoT 定义
- 命名遵循 ADR-0004（例如 `core.guild.created`）
- 事件 type 一旦发布应视为稳定标识，不得随意改名

## 2) 内容 ID（Content IDs）

### 2.1 命名空间

- Base 内容：`Base_*`
- DLC1 内容：`DLC1_*`

规则：

- 所有引用一律用 `id` 字符串，不使用数组下标
- 不使用空格/中文/随机 GUID 作为内容 ID
- 建议用 PascalCase 分词并用 `_` 分隔（示例：`Base_GuildEvent_WelcomeNewMember`）

### 2.2 内容文件级版本

- `contentVersion`：语义版本字符串（例如 `"1.0.0"`）

建议：

- Base 内容与 DLC 内容各自维护 `contentVersion`
- 内容变更需要明确“向后兼容策略”（至少能解释是否影响存档）

## 3) 存档版本（Save Versions）

存档版本至少拆分为两层：

- `schemaVersion`：数据库 schema 版本（SQLite 迁移 runner 负责）
- `saveVersion`：逻辑快照版本（Core 映射器负责）

约束：

- `schemaVersion` 只递增
- 迁移必须幂等、可回放
- Release/secure 模式下，错误信息与审计 `reason` 不得泄露绝对路径或 SQL 原文（ADR-0019）

## 4) 最小校验规则（门禁绑定）

本项目的“内容口径”必须满足：

- JSON 必须可解析（UTF-8）
- 必须包含 `contentVersion`
- 内容条目必须包含 `id` 且符合 `Base_*` / `DLC1_*` 命名空间

CI 校验入口：

- `py -3 scripts/python/validate_content_assets.py`
- 产物：`logs/ci/<YYYY-MM-DD>/content-validation/`
