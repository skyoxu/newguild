#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Update a GitHub PR title/body from .taskmaster/tasks/tasks.json using UTF-8 files.

Why this exists:
  - Avoid PowerShell codepage issues that can turn Chinese text into '?' or mojibake.
  - Keep a reproducible, repo-local PR body under logs/ci/<date>/pr/.

Usage (Windows, PowerShell):
  py -3 scripts/python/update_pr_from_taskmaster.py --task-id 12 --pr 9
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import subprocess
from pathlib import Path
from typing import Any


def today_str() -> str:
    return dt.date.today().strftime("%Y-%m-%d")


def write_text(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8", newline="\n")


def read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def parse_semicolon_list(value: str) -> list[str]:
    return [s.strip() for s in (value or "").split(";") if s.strip()]


def parse_details(details: str) -> dict[str, list[str] | str]:
    out: dict[str, list[str] | str] = {}
    for raw_line in (details or "").splitlines():
        line = raw_line.strip()
        if not line or ":" not in line:
            continue
        k, v = line.split(":", 1)
        key = k.strip()
        val = v.strip()
        if key in {"ADR Refs", "Chapters", "Test Refs", "Labels"}:
            out[key] = parse_semicolon_list(val)
        else:
            out[key] = val
    return out


def build_body(task_id: str, task: dict[str, Any]) -> str:
    # Headings are Unicode escapes to keep this script ASCII-only.
    h_task = "## " + "\u4efb\u52a1\u8bf4\u660e"  # Task description
    h_adr = "## ADR/CH " + "\u5f15\u7528"  # References
    h_tests = "## " + "\u6d4b\u8bd5\u5f15\u7528"  # Test references
    h_gates = "## " + "\u8d28\u91cf\u95e8\u7981"  # Quality gates

    title = str(task.get("title") or "").strip()
    description = str(task.get("description") or "").strip()
    details_raw = str(task.get("details") or "").strip()
    parsed = parse_details(details_raw)

    story = str(parsed.get("Story") or "").strip()
    adrs = parsed.get("ADR Refs") or []
    chapters = parsed.get("Chapters") or []
    tests = parsed.get("Test Refs") or []

    lines: list[str] = []
    lines.append(h_task)
    lines.append(f"- \u672c\u6b21 PR \u5305\u542b\u4efb\u52a1 {task_id}\uff1a{title}")
    if description:
        lines.append(f"- {description}")
    if story:
        lines.append(f"- Story: `{story}`")

    lines.append("")
    lines.append(h_adr)
    if adrs:
        lines.append("- ADR: " + ", ".join(f"`{a}`" for a in adrs))
    if chapters:
        lines.append("- CH: " + ", ".join(f"`{c}`" for c in chapters))
    if not adrs and not chapters:
        lines.append("- (none)")

    lines.append("")
    lines.append(h_tests)
    if tests:
        for t in tests:
            lines.append(f"- `{t}`")
    else:
        lines.append("- (none)")

    lines.append("")
    lines.append(h_gates)
    lines.append("- Windows Quality Gate: `py -3 scripts/python/ci_pipeline.py all --solution Game.sln --configuration Debug --godot-bin \"$env:GODOT_BIN\" --build-solutions`")
    lines.append("- Notes: `encoding` is a soft gate; others in `ci_pipeline.py` are hard gates unless marked soft in workflows.")

    refs = [
        f"Task {task_id}",
        f"Story {story}" if story else None,
        "Workflows: Windows Quality Gate, Windows CI",
    ]
    refs = [r for r in refs if r]
    lines.append("")
    lines.append("Refs: " + "; ".join(refs))
    return "\n".join(lines).rstrip() + "\n"


def run_gh(args: list[str], cwd: Path, log_path: Path) -> int:
    proc = subprocess.run(
        args,
        cwd=str(cwd),
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    out = (proc.stdout or "") + (proc.stderr or "")
    write_text(log_path, out)
    return proc.returncode


def main() -> int:
    ap = argparse.ArgumentParser(description="Update GitHub PR title/body from Taskmaster tasks.json (UTF-8)")
    ap.add_argument("--task-id", required=True, help="Task id in .taskmaster/tasks/tasks.json (e.g. 12)")
    ap.add_argument("--pr", type=int, default=None, help="PR number to edit (optional)")
    ap.add_argument("--tasks-json", default=".taskmaster/tasks/tasks.json")
    ap.add_argument("--out", default=None, help="Output markdown path (defaults to logs/ci/<date>/pr/pr-body-task<id>.md)")
    args = ap.parse_args()

    root = Path(__file__).resolve().parents[2]
    tasks_path = (root / args.tasks_json).resolve()
    data = read_json(tasks_path)

    tasks = (data.get("master") or {}).get("tasks") or []
    task = next((t for t in tasks if str(t.get("id")) == str(args.task_id)), None)
    if not task:
        raise SystemExit(f"Task id not found: {args.task_id} (in {tasks_path})")

    title = str(task.get("title") or "").strip()
    pr_title = f"Task [{args.task_id}]:{title}"

    date = today_str()
    out_path = Path(args.out) if args.out else (root / "logs" / "ci" / date / "pr" / f"pr-body-task{args.task_id}.md")
    body = build_body(str(args.task_id), task)
    write_text(out_path, body)

    if args.pr is None:
        print(f"[OK] Wrote PR body: {out_path}")
        print(f"[OK] Title: {pr_title}")
        return 0

    pr_dir = out_path.parent
    rc = run_gh(
        ["gh", "pr", "edit", str(args.pr), "--title", pr_title, "--body-file", str(out_path)],
        cwd=root,
        log_path=pr_dir / f"pr-edit-{args.pr}.log",
    )
    if rc != 0:
        print(f"[FAIL] gh pr edit rc={rc} (see {pr_dir / f'pr-edit-{args.pr}.log'})")
        return rc

    # Verify body round-trip (lightweight).
    rc2 = run_gh(
        ["gh", "pr", "view", str(args.pr), "--json", "title,body"],
        cwd=root,
        log_path=pr_dir / f"pr-view-{args.pr}.json",
    )
    if rc2 != 0:
        print(f"[WARN] gh pr view rc={rc2} (see {pr_dir / f'pr-view-{args.pr}.json'})")
        return 0

    print(f"[OK] Updated PR #{args.pr} with UTF-8 title/body. Body file: {out_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
