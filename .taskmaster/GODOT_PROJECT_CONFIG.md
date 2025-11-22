# Godot + C# 项目专用配置

## 任务层级定义（layer 字段）

根据项目三层架构，每个任务必须标注所属层级：

```json
{
  "layer": "core|adapter|scene|test"
}
```

### 层级映射关系

| Layer | 目录路径 | 职责 | 测试方式 |
|-------|---------|------|---------|
| `core` | `Game.Core/**` | 纯 C# 领域模型、业务逻辑 | xUnit 单测（覆盖率 ≥90%） |
| `adapter` | `Game.Godot/Adapters/**` | Godot API 适配层 | xUnit 契约测试 + Mock |
| `scene` | `Game.Godot/Scenes/**` + `Game.Godot/Scripts/**` | 场景、UI、信号连接 | GdUnit4 场景测试 |
| `test` | `Game.Core.Tests/**` 或 `Tests.Godot/tests/**` | 测试代码 | 测试本身 |

## 必需字段扩展

### adr_refs（必填）
关联的 ADR 文档，每个任务至少 1 个：

```json
{
  "adr_refs": ["ADR-0006", "ADR-0007"]
}
```

**可用 ADR 列表**：
- `ADR-0001`: 技术栈选型（Godot 4.5 + C#）
- `ADR-0004`: 事件总线和契约
- `ADR-0005`: 质量门禁
- `ADR-0006`: 数据存储（SQLite + ConfigFile）
- `ADR-0007`: 端口适配器模式
- `ADR-0011`: Windows-only 平台策略
- `ADR-0023`: Settings SSoT（ConfigFile）

### chapter_refs（推荐）
关联的 arc42 架构章节：

```json
{
  "chapter_refs": ["CH04", "CH05", "CH06"]
}
```

**可用章节**：
- `CH01`: 引言和目标
- `CH02`: 安全基线
- `CH03`: 可观测性
- `CH04`: 系统上下文
- `CH05`: 数据模型和存储
- `CH06`: 运行时视图
- `CH07`: 开发构建和门禁

### overlay_refs（可选）
功能纵切文档引用：

```json
{
  "overlay_refs": ["docs/architecture/overlays/PRD-GUILD/08/guild-system.md"]
}
```

### story_id（推荐）
来源需求 ID：

```json
{
  "story_id": "STORY-001"
}
```

## 任务模板示例

### Core 层任务
```json
{
  "id": "1",
  "story_id": "STORY-001",
  "title": "定义公会领域模型",
  "description": "创建 Guild、GuildMember 实体类，包含验证逻辑",
  "layer": "core",
  "adr_refs": ["ADR-0001", "ADR-0007"],
  "chapter_refs": ["CH04", "CH05"],
  "overlay_refs": [],
  "depends_on": [],
  "priority": "high",
  "status": "pending",
  "testStrategy": "xUnit 单测：实体验证、业务规则，覆盖率 ≥90%",
  "details": "遵循 DDD 原则，实体包含：\n- Guild: Id, Name, MemberCount, CreatedAt\n- GuildMember: Id, GuildId, UserId, Role, JoinedAt\n验证规则：\n- 公会名称 3-20 字符\n- 成员上限 100 人"
}
```

### Adapter 层任务
```json
{
  "id": "2",
  "story_id": "STORY-001",
  "title": "实现 GuildRepository 适配器",
  "description": "封装 SQLite 数据访问，限制 user:// 路径",
  "layer": "adapter",
  "adr_refs": ["ADR-0006", "ADR-0007"],
  "chapter_refs": ["CH05"],
  "depends_on": ["1"],
  "priority": "high",
  "status": "pending",
  "testStrategy": "xUnit 契约测试：Mock IRepository，验证接口正确性",
  "details": "使用 SqliteDataStore 适配器：\n- 路径验证：仅 user:// 开头\n- 错误处理：路径越界写入审计日志\n- 接口：SaveGuild, GetGuild, DeleteGuild"
}
```

### Scene 层任务
```json
{
  "id": "3",
  "story_id": "STORY-001",
  "title": "创建公会管理 UI 场景",
  "description": "设计 GuildPanel.tscn，包含创建、加入、管理功能",
  "layer": "scene",
  "adr_refs": ["ADR-0004"],
  "chapter_refs": ["CH04", "CH06"],
  "depends_on": ["2"],
  "priority": "medium",
  "status": "pending",
  "testStrategy": "GdUnit4 场景测试：节点可见性、信号连通性、UI 交互",
  "details": "场景结构：\n- GuildPanel (Control)\n  - CreateGuildDialog\n  - GuildListContainer\n  - MemberListPanel\n信号：\n- guild_created(guild_data)\n- member_joined(member_data)\n事件命名遵循：ui.guild.*"
}
```

### Test 层任务
```json
{
  "id": "4",
  "story_id": "STORY-001",
  "title": "编写公会系统集成测试",
  "description": "GdUnit4 集成测试，覆盖完整链路",
  "layer": "test",
  "adr_refs": ["ADR-0005"],
  "chapter_refs": ["CH07"],
  "depends_on": ["3"],
  "priority": "medium",
  "status": "pending",
  "testStrategy": "GdUnit4 集成：Core → Adapter → Scene 完整流程",
  "details": "测试场景：\n1. 创建公会 → 验证 DB 写入 → UI 更新\n2. 加入成员 → 验证 DB 更新 → 事件发布\n3. 路径安全 → 拒绝绝对路径 → 审计日志"
}
```

## 验证命令映射

任务完成后的验证流程（引用 dev_cli.py）：

```bash
# Core/Adapter 层完成后
py -3 scripts/python/dev_cli.py run-ci-basic --godot-bin "C:\Godot\Godot_v4.5.1-stable_mono_win64.exe"

# Scene/Test 层完成后
py -3 scripts/python/dev_cli.py run-quality-gates --godot-bin "C:\Godot\Godot_v4.5.1-stable_mono_win64.exe" --gdunit-hard --smoke
```

## PRD 解析配置

### 推荐 PRD 结构
```markdown
# 功能名称 PRD

## STORY-001: [功能描述]

### 目标
简述核心价值

### 功能需求
#### 输入
- 用户操作：点击"创建公会"
- 输入字段：公会名称、图标

#### 输出
- UI 反馈：成功/失败提示
- 数据变更：guilds 表新增记录

#### 状态
- 前置条件：用户已登录
- 后置条件：公会创建成功

### 技术约束
- ADR-0006: 使用 SQLite 存储
- ADR-0007: 通过 Repository 模式访问
- ADR-0004: 发布 core.guild.created 事件

### 验收标准
- [ ] xUnit 单测覆盖率 ≥90%
- [ ] GdUnit4 场景测试通过
- [ ] 路径安全审计通过

### 分层建议
- Core: Guild 实体、验证逻辑
- Adapter: GuildRepository
- Scene: GuildPanel UI
- Test: 集成测试
```

### 解析命令
```bash
# 从 PRD 生成任务，自动推断层级和 ADR 引用
task-master parse-prd .taskmaster/docs/prd-guild-system.txt --num=20 --research
```

## 依赖管理规则

### 层级依赖顺序
```
Core → Adapter → Scene → Test
```

**示例依赖链**：
```json
[
  {"id": "1", "layer": "core", "depends_on": []},
  {"id": "2", "layer": "adapter", "depends_on": ["1"]},
  {"id": "3", "layer": "scene", "depends_on": ["2"]},
  {"id": "4", "layer": "test", "depends_on": ["3"]}
]
```

### 跨功能依赖
```json
{
  "id": "10",
  "title": "公会战斗系统",
  "depends_on": ["3", "7"],  // 依赖公会系统和战斗系统
  "layer": "core"
}
```

## 任务状态转换

```
pending → in-progress → done
   ↓           ↓
blocked    deferred
   ↓
cancelled
```

**状态验证规则**：
- `pending → in-progress`: 前置依赖全部 done
- `in-progress → done`: testStrategy 验证通过
- `* → blocked`: 外部依赖未就绪
- `* → deferred`: 延期到下版本

## AI 研究模式配置

针对 Godot 4.5 + C# 的研究主题：

```bash
# 启用研究模式（需要 PERPLEXITY_API_KEY）
task-master expand --id=5 --research

# 研究主题示例
task-master research "Godot 4.5 C# Signals 最佳实践" --id=7
task-master research "GdUnit4 headless CI/CD 配置 2025" --save-to-file
```

## 注意事项

### 禁止项
- ❌ 不要手动编辑 tasks.json
- ❌ 不要跳过 adr_refs 字段
- ❌ 不要违反层级依赖顺序（Core → Adapter → Scene → Test）
- ❌ 不要在 Core 层引用 Godot API

### 推荐项
- ✅ 每个任务附带 testStrategy
- ✅ 使用研究模式获取最新最佳实践
- ✅ 分阶段解析大型 PRD
- ✅ Git commit 引用任务 ID

---

**文档版本**: v1.0
**适用项目**: C:\buildgame\newguild
**更新日期**: 2025-01-22
