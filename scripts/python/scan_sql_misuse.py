#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Static scan for SQL misuse patterns (Windows, Godot+C# repo).

Hard-gate focus (CWE-89 defense-in-depth):
  - Reject: SqlStatement.NoParameters("...WHERE ...")

Rationale:
  - The port already enforces parameterization via SqlStatement.
  - This script prevents common future misuses where developers pass filtering SQL
    without parameters (e.g., string interpolation) into NoParameters.

Outputs (SSoT logs):
  logs/ci/<YYYY-MM-DD>/sql-scan/
    - report.json
    - report.txt

Usage:
  py -3 scripts/python/scan_sql_misuse.py
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import re
import subprocess
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Iterable, Optional


@dataclass(frozen=True)
class Finding:
    file: str
    line: int
    column: int
    rule: str
    message: str
    snippet: str


RULE_NO_PARAMETERS_WHERE = "NO_PARAMETERS_WHERE"


def today_str() -> str:
    return dt.date.today().strftime("%Y-%m-%d")


def run_git_ls_files(repo_root: Path, pattern: str) -> list[str]:
    proc = subprocess.run(
        ["git", "ls-files", pattern],
        cwd=str(repo_root),
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if proc.returncode != 0:
        raise RuntimeError(f"git ls-files failed: {proc.stderr.strip()}")
    return [line.strip() for line in proc.stdout.splitlines() if line.strip()]


def write_text(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8", newline="\n")


def write_json(path: Path, obj: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2), encoding="utf-8", newline="\n")


def is_word_boundary_char(c: str) -> bool:
    return not (c.isalnum() or c == "_")


def contains_word_case_insensitive(text: str, word: str) -> bool:
    if not text or not word:
        return False
    low = text.lower()
    w = word.lower()
    i = 0
    while True:
        j = low.find(w, i)
        if j < 0:
            return False
        before_ok = j == 0 or is_word_boundary_char(low[j - 1])
        after_idx = j + len(w)
        after_ok = after_idx >= len(low) or is_word_boundary_char(low[after_idx])
        if before_ok and after_ok:
            return True
        i = j + 1


def extract_csharp_string_literal(src: str, start: int) -> Optional[tuple[str, int]]:
    """
    Parse a C# string literal starting at src[start].
    Supports:
      - "..." (with escapes)
      - @"..." (verbatim, "" escapes)
    Returns (decoded_text, end_index_exclusive) or None if not parseable.
    """
    if start >= len(src):
        return None

    if src.startswith('@"', start):
        i = start + 2
        out = []
        while i < len(src):
            ch = src[i]
            if ch == '"':
                if i + 1 < len(src) and src[i + 1] == '"':
                    out.append('"')
                    i += 2
                    continue
                return ("".join(out), i + 1)
            out.append(ch)
            i += 1
        return None

    if src[start] != '"':
        return None

    i = start + 1
    out = []
    while i < len(src):
        ch = src[i]
        if ch == "\\":
            if i + 1 >= len(src):
                return None
            nxt = src[i + 1]
            # Keep it simple; we don't need exact unescape for word detection.
            out.append(nxt)
            i += 2
            continue
        if ch == '"':
            return ("".join(out), i + 1)
        out.append(ch)
        i += 1
    return None


def iter_no_parameters_calls(text: str) -> Iterable[tuple[int, int, str]]:
    """
    Yields (line, col, sql_text) for parseable SqlStatement.NoParameters(<string-literal>).
    """
    # Quick coarse matcher for call sites.
    pat = re.compile(r"SqlStatement\s*\.\s*NoParameters\s*\(", re.MULTILINE)
    for m in pat.finditer(text):
        call_start = m.end()
        i = call_start
        while i < len(text) and text[i].isspace():
            i += 1
        lit = extract_csharp_string_literal(text, i)
        if not lit:
            continue
        sql_text, _end = lit
        # Compute 1-based line/col for the start of the string literal.
        line = text.count("\n", 0, i) + 1
        last_nl = text.rfind("\n", 0, i)
        col = i + 1 if last_nl < 0 else (i - last_nl)
        yield (line, col, sql_text)


def scan_file(repo_root: Path, rel_path: str) -> list[Finding]:
    path = repo_root / rel_path
    try:
        content = path.read_text(encoding="utf-8", errors="replace")
    except Exception as exc:
        return [
            Finding(
                file=rel_path.replace("\\", "/"),
                line=1,
                column=1,
                rule="READ_ERROR",
                message=f"Failed to read file: {exc}",
                snippet="",
            )
        ]

    findings: list[Finding] = []
    for line, col, sql in iter_no_parameters_calls(content):
        if contains_word_case_insensitive(sql, "where"):
            findings.append(
                Finding(
                    file=rel_path.replace("\\", "/"),
                    line=line,
                    column=col,
                    rule=RULE_NO_PARAMETERS_WHERE,
                    message="SqlStatement.NoParameters must not contain WHERE; use parameters instead.",
                    snippet=sql[:180].replace("\n", "\\n"),
                )
            )

    return findings


def build_parser() -> argparse.ArgumentParser:
    ap = argparse.ArgumentParser(description="Static scan for SQL misuse patterns")
    ap.add_argument("--repo-root", default=".", help="Repository root (default: .)")
    ap.add_argument("--out-dir", default=None, help="Output directory (default: logs/ci/<date>/sql-scan)")
    ap.add_argument("--fail-on-findings", action="store_true", help="Return non-zero if findings exist (hard gate)")
    return ap


def main() -> int:
    args = build_parser().parse_args()
    repo_root = Path(args.repo_root).resolve()
    date = today_str()

    out_dir = Path(args.out_dir) if args.out_dir else (repo_root / "logs" / "ci" / date / "sql-scan")
    out_dir.mkdir(parents=True, exist_ok=True)

    try:
        files = run_git_ls_files(repo_root, "*.cs")
    except Exception as exc:
        report = {
            "timestamp": dt.datetime.now(dt.UTC).isoformat(timespec="seconds"),
            "status": "fail",
            "error": str(exc),
            "findings": [],
        }
        write_json(out_dir / "report.json", report)
        write_text(out_dir / "report.txt", f"[FAIL] git ls-files error: {exc}\n")
        print(f"SQL_SCAN status=fail out={out_dir}")
        return 1

    findings: list[Finding] = []
    for rel in files:
        # Skip generated/vendor dirs even if tracked (defense-in-depth).
        low = rel.replace("\\", "/").lower()
        if any(seg in low for seg in ["/addons/", "/.godot/", "/bin/", "/obj/"]):
            continue
        findings.extend(scan_file(repo_root, rel))

    status = "ok" if len(findings) == 0 else "fail"
    report = {
        "timestamp": dt.datetime.now(dt.UTC).isoformat(timespec="seconds"),
        "status": status,
        "findings_count": len(findings),
        "rules": [RULE_NO_PARAMETERS_WHERE],
        "findings": [asdict(f) for f in findings],
    }
    write_json(out_dir / "report.json", report)

    lines = [f"SQL static scan: {status} (findings={len(findings)})"]
    for f in findings[:200]:
        lines.append(f"- {f.rule} {f.file}:{f.line}:{f.column} {f.message} :: {f.snippet}")
    if len(findings) > 200:
        lines.append(f"... truncated, total findings={len(findings)}")
    write_text(out_dir / "report.txt", "\n".join(lines) + "\n")

    print(f"SQL_SCAN status={status} findings={len(findings)} out={out_dir}")
    if args.fail_on_findings and findings:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

