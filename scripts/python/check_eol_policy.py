#!/usr/bin/env python3
# -*- coding: utf-8 -*-

from __future__ import annotations

import argparse
import datetime as dt
import json
import subprocess
from dataclasses import dataclass
from pathlib import Path


LF_EXTENSIONS = {
    ".py",
    ".cs",
    ".gd",
    ".tscn",
    ".tres",
    ".cfg",
    ".ini",
    ".json",
    ".md",
    ".yml",
    ".yaml",
    ".xml",
    ".txt",
    ".sln",
    ".csproj",
    ".props",
    ".targets",
    ".sh",
}

CRLF_EXTENSIONS = {".ps1", ".bat", ".cmd"}

LF_EXACT_FILENAMES = {".editorconfig", ".gitignore", ".gitattributes"}


@dataclass(frozen=True)
class CheckEntry:
    path: str
    expected: str
    actual: str


def run_cmd(command: list[str], stdin: str | None = None) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        command,
        input=stdin,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=False,
    )


def list_tracked_files() -> list[str]:
    cp = run_cmd(["git", "ls-files"])
    if cp.returncode != 0:
        raise RuntimeError(f"git ls-files failed: {cp.stderr.strip()}")
    return [line.strip() for line in cp.stdout.splitlines() if line.strip()]


def expected_eol(path: str) -> str | None:
    p = Path(path)
    name = p.name.lower()
    suffix = p.suffix.lower()

    if name in LF_EXACT_FILENAMES:
        return "lf"
    if suffix in CRLF_EXTENSIONS:
        return "crlf"
    if suffix in LF_EXTENSIONS:
        return "lf"
    return None


def parse_attr_lines(output: str) -> dict[str, str]:
    result: dict[str, str] = {}
    for raw in output.splitlines():
        line = raw.strip()
        if not line:
            continue
        marker = ": eol: "
        idx = line.find(marker)
        if idx < 0:
            continue
        path = line[:idx].strip()
        if path.startswith('"') and path.endswith('"'):
            path = path[1:-1]
        path = path.replace("\\r", "").replace("\\n", "")
        value = line[idx + len(marker) :].strip().lower()
        result[path] = value
    return result


def query_eol_map(paths: list[str], batch_size: int = 400) -> dict[str, str]:
    eol_map: dict[str, str] = {}
    for i in range(0, len(paths), batch_size):
        batch = paths[i : i + batch_size]
        payload = ("\n".join(batch) + "\n").encode("utf-8")
        cp = subprocess.run(
            ["git", "check-attr", "eol", "--stdin"],
            input=payload,
            capture_output=True,
            check=False,
        )
        if cp.returncode != 0:
            raise RuntimeError(f"git check-attr failed: {cp.stderr.decode('utf-8', errors='replace').strip()}")
        stdout = cp.stdout.decode("utf-8", errors="replace")
        eol_map.update(parse_attr_lines(stdout))
    return eol_map


def write_json(path: Path, obj: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def build_report(entries: list[CheckEntry]) -> str:
    if not entries:
        return "EOL policy check passed.\n"

    lines = ["EOL policy violations:"]
    for item in entries:
        lines.append(f"- {item.path} expected={item.expected} actual={item.actual}")
    lines.append("")
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate repository EOL policy against .gitattributes expectations.")
    parser.add_argument("--date", default="", help="Date token for logs path (yyyy-MM-dd). Default: local today.")
    parser.add_argument("--out-dir", default="", help="Optional output directory. Default: logs/ci/<date>/eol-policy")
    parser.add_argument("--strict", action="store_true", help="Return non-zero when any violation is found.")
    args = parser.parse_args()

    date_token = args.date.strip() if args.date else dt.datetime.now().strftime("%Y-%m-%d")
    out_dir = Path(args.out_dir.strip()) if args.out_dir else Path("logs") / "ci" / date_token / "eol-policy"
    out_dir.mkdir(parents=True, exist_ok=True)

    tracked = list_tracked_files()
    targets: list[str] = []
    expected_map: dict[str, str] = {}

    for path in tracked:
        expected = expected_eol(path)
        if expected is None:
            continue
        targets.append(path)
        expected_map[path] = expected

    actual_map = query_eol_map(targets)

    violations: list[CheckEntry] = []
    for path in targets:
        expected = expected_map[path]
        actual = actual_map.get(path, "unspecified")
        if actual != expected:
            violations.append(CheckEntry(path=path, expected=expected, actual=actual))

    summary = {
        "date": date_token,
        "status": "fail" if violations else "ok",
        "checked_files": len(targets),
        "violations_count": len(violations),
        "violations": [
            {"path": item.path, "expected": item.expected, "actual": item.actual}
            for item in violations
        ],
    }

    write_json(out_dir / "summary.json", summary)
    write_text(out_dir / "report.txt", build_report(violations))

    print(
        f"EOL_POLICY status={summary['status']} checked={summary['checked_files']} "
        f"violations={summary['violations_count']} out={out_dir.as_posix()}"
    )

    if args.strict and violations:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
