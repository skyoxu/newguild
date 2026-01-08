# ID and Versioning Strategy（内容/事件/存档）

## 目标

在进入 T3 之前先固定“稳定 ID 与版本”的最小口径，避免后期出现：

- 内容 ID 漂移导致存档不可迁移
- 测试夹具无法复现（输入不稳定）
- 契约/事件/内容三套命名互相打架

## 1. 事件 type（CloudEvents）

- 事件 type 由 Contracts SSoT 定义
- 命名遵循 ADR-0004（例如 `core.guild.created`）
- 事件 type 一旦发布应视为稳定标识，不得随意改名

## 2. 内容 ID（ContentId）

建议格式（全小写，`.` 分隔）：

`content.<module>.<kind>.<name>`

示例：

- `content.guild.event.welcome_new_member`
- `content.guild.roster.sample_member_001`

规则：

- 不使用空格/中文/随机 GUID 作为内容 ID
- `name` 使用 `snake_case`
- 内容 ID 允许在早期阶段少量人工维护，但必须可被脚本校验

## 3. 版本字段

### 3.1 内容版本

- `version`：整数（`>=1`），用于内容 JSON 自身的演进
- 版本提升意味着兼容性变化需要明确说明（至少在 PR 描述或变更记录中）

### 3.2 存档版本

- `schemaVersion`：数据库 schema 版本（迁移 runner 负责）
- `payloadVersion`：逻辑快照版本（Core 映射器负责）

## 4. 最小校验规则（门禁绑定）

建议对内容文件最小硬规则：

- JSON 必须可解析
- 必须包含 `id` 与 `version`
- `id` 必须匹配 `content.<module>.<kind>.<name>`

