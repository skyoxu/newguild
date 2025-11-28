"""Update AI assistants index and state files.

This script is the newguild equivalent of vitegame's
`scripts/ci/update-ai-index.mjs`. It scans AI assistant
related documentation (AGENTS, CLAUDE commands, workflows)
and generates:

- docs/ai-assistants.index.md   (human-readable Markdown index)
- docs/ai-assistants.state.json (machine-readable state snapshot)

It also writes a small summary JSON under
logs/ci/<YYYY-MM-DD>/ai-index/summary.json so CI can
collect an artifact (docs-encoding-and-ai-index-logs).

Usage (Windows):

    py -3 scripts/python/update_ai_index.py --write

The script is non-destructive and safe to run multiple
times; it will overwrite the two docs files with
deterministic content.
"""

from __future__ import annotations

import argparse
import datetime as dt
import io
import json
from pathlib import Path
from typing import Dict, List, Optional


ROOT = Path(__file__).resolve().parents[2]
DOCS_DIR = ROOT / "docs"


def _read_title(path: Path) -> str:
    """Read the first Markdown heading as title, fallback to filename."""

    try:
        with path.open("r", encoding="utf-8", errors="ignore") as f:
            for line in f:
                stripped = line.strip()
                if stripped.startswith("#"):
                    return stripped.lstrip("#").strip()
    except OSError:
        pass
    return path.name


def _load_index_entries() -> List[Dict[str, str]]:
    """Gather AI-assistant related docs into a structured list.

    This intentionally focuses on a small, well-known set of
    files which act as the SSoT for AI-assisted workflows in
    this repository.
    """

    entries: List[Dict[str, str]] = []

    def add_if_exists(rel_path: str, category: str) -> None:
        path = ROOT / rel_path
        if path.exists():
            entries.append(
                {
                    "path": rel_path.replace("\\", "/"),
                    "title": _read_title(path),
                    "category": category,
                }
            )

    # Core AI / agent docs at repo root
    add_if_exists("AGENTS.md", "architecture")
    add_if_exists("CLAUDE.md", "assistant")
    add_if_exists("mcpuse.md", "mcp")

    # Workflows describing Taskmaster + SuperClaude integration
    add_if_exists(
        "docs/workflows/task-master-superclaude-integration.md",
        "workflow",
    )

    # Project-wide documentation index (AI assistants section)
    add_if_exists("docs/PROJECT_DOCUMENTATION_INDEX.md", "index")

    # Claude Code CLI slash commands (scene/script/component helpers)
    commands_dir = ROOT / ".claude" / "commands"
    if commands_dir.exists():
        for md in sorted(commands_dir.glob("*.md")):
            entries.append(
                {
                    "path": str(md.relative_to(ROOT)).replace("\\", "/"),
                    "title": _read_title(md),
                    "category": "command",
                }
            )

    return entries


def _build_markdown(entries: List[Dict[str, str]]) -> str:
    """Render a simple Markdown index for AI assistants."""

    lines: List[str] = []
    lines.append("# AI 助手与集成索引")
    lines.append("")
    lines.append(
        "> 说明：本索引由 scripts/python/update_ai_index.py 自动生成，" "汇总与 AI 助手、Taskmaster/SuperClaude 工作流相关的关键文档与命令。"
    )
    lines.append("")
    if not entries:
        lines.append("（当前未检测到任何 AI 助手相关文档条目。）")
        return "\n".join(lines) + "\n"

    lines.append("| 类别 | 标题 | 路径 |")
    lines.append("| ---- | ---- | ---- |")
    for e in entries:
        cat = e["category"]
        title = e["title"].replace("|", "\\|")
        path = e["path"]
        lines.append(f"| {cat} | {title} | `{path}` |")

    lines.append("")
    lines.append("## 使用建议")
    lines.append("")
    lines.append("- 修改 AGENTS/CLAUDE/工作流文档或 .claude/commands/** 后，可运行 `py -3 scripts/python/update_ai_index.py --write` 更新本索引。")
    lines.append("- CI 中会在 `windows-ci` 工作流中以非阻断方式调用该脚本，并将日志写入 `logs/ci/<date>/ai-index/`。")
    return "\n".join(lines) + "\n"


def write_index(write: bool = True) -> Dict[str, object]:
    """Compute and optionally write index/state files.

    Returns a small summary dict for logging.
    """

    entries = _load_index_entries()
    generated_at = dt.datetime.utcnow().isoformat() + "Z"

    # Build docs content
    md = _build_markdown(entries)
    state = {
        "generated_at": generated_at,
        "entry_count": len(entries),
        "entries": entries,
    }

    if write:
        DOCS_DIR.mkdir(parents=True, exist_ok=True)
        index_path = DOCS_DIR / "ai-assistants.index.md"
        state_path = DOCS_DIR / "ai-assistants.state.json"
        with io.open(index_path, "w", encoding="utf-8") as f_md:
            f_md.write(md)
        with io.open(state_path, "w", encoding="utf-8") as f_js:
            json.dump(state, f_js, ensure_ascii=False, indent=2)

    return {"generated_at": generated_at, "entry_count": len(entries)}


def main(argv: Optional[List[str]] = None) -> int:
    ap = argparse.ArgumentParser(
        description="Update AI assistants index/state docs and write CI summary."
    )
    ap.add_argument(
        "--write",
        action="store_true",
        help="Write docs/ai-assistants.* files (default behavior).",
    )
    args = ap.parse_args(argv)

    summary = write_index(write=args.write)

    # Write CI summary under logs/ci/<YYYY-MM-DD>/ai-index/summary.json
    date = dt.date.today().strftime("%Y-%m-%d")
    ai_dir = ROOT / "logs" / "ci" / date / "ai-index"
    ai_dir.mkdir(parents=True, exist_ok=True)
    with io.open(ai_dir / "summary.json", "w", encoding="utf-8") as f:
        json.dump(summary, f, ensure_ascii=False, indent=2)

    print(
        f"AI_INDEX generated_at={summary['generated_at']} entries={summary['entry_count']}"
    )
    return 0


if __name__ == "__main__":  # pragma: no cover - CLI entry point
    raise SystemExit(main())

