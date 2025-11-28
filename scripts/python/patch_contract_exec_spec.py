#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Patch section 3.4.1 契约先行执行规范 to add MCP usage.

This script reads docs/workflows/task-master-superclaude-integration.md
as UTF-8 and injects a new step 0 in section 3.4.1 describing how to
use Context7/Serena MCP to assist contract-first work, without changing
the existing steps 1-6.
"""

from __future__ import annotations

from pathlib import Path


def main() -> None:
    path = Path("docs/workflows/task-master-superclaude-integration.md")
    text = path.read_text(encoding="utf-8")

    marker = "**契约定义流程：**"
    if "使用 MCP 辅助契约检索" in text:
        print("[patch_contract_exec_spec] MCP step already present, no changes made")
        return

    insert_block = (
        "**步骤 0：使用 MCP 辅助契约检索（Context7 / Serena 等）**\n\n"
        "在识别契约需求之前，优先用 MCP 工具收集上下文，避免“重复造轮子”或破坏既有契约：\n\n"
        "- 使用 **Context7 MCP** 检索代码与文档：\n"
        "  - 典型查询对象：`Game.Core`、`Scripts/Core/Contracts/**`、`docs/adr/ADR-0004-*`、Overlay 08；\n"
        "  - 目标：确认是否已有同名或语义相近的事件/DTO/接口定义；\n"
        "  - 示例（在 Claude Code 中）：`@context7 search \"GuildCreated EventType\"`。\n"
        "- 使用 **Serena MCP**（如已配置）在仓库中搜索符号：\n"
        "  - 例如查找已有的 `GuildCreated` 类型、`IGuildService` 接口、`EventType` 常量等；\n"
        "  - 目标：让新契约与现有命名/字段保持一致。\n"
        "- 如涉及外部协议（OpenAPI/HTTP/第三方 SDK），可按需启用对应 MCP：\n"
        "  - 只将协议片段作为契约模板输入，不直接生成实现代码。\n\n"
        " > 约束：MCP 只用于“找资料”和“补充上下文”，契约文件的最终内容仍以 `Scripts/Core/Contracts/**` 中的人工确认版本为 SSoT，并需经过步骤 3 和步骤 5 的审查与文档更新。"
    )

    if marker not in text:
        print("[patch_contract_exec_spec] marker not found, no changes made")
        return

    new_text = text.replace(marker, marker + "\n\n" + insert_block)
    path.write_text(new_text, encoding="utf-8")
    print("[patch_contract_exec_spec] inserted MCP helper step into 3.4.1")


if __name__ == "__main__":  # pragma: no cover
    main()

