"""
Deterministic guardrail for CI GdUnit gates.

Purpose:
  Prevent accidental inclusion of demo/experimental UI tests in the hard UI gate.

Rationale:
  CI runs a stable, headless-compatible suite. Demo tests (often non-deterministic or incomplete)
  must live outside the hard-gated directories to avoid unrelated red builds.
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import re
from pathlib import Path


def repo_root() -> Path:
    # Resolve by walking up to a directory containing project.godot
    cur = Path.cwd().resolve()
    while True:
        if (cur / "project.godot").is_file():
            return cur
        if cur.parent == cur:
            raise RuntimeError("Failed to locate repo root (missing project.godot)")
        cur = cur.parent


def ci_date() -> str:
    raw = os.environ.get("CI_DATE_UTC") or os.environ.get("CI_DATE")
    if raw:
        try:
            dt.datetime.strptime(raw, "%Y-%m-%d")
            return raw
        except ValueError:
            pass
    return dt.datetime.now(dt.UTC).strftime("%Y-%m-%d")


def write_report(out_dir: Path, *, payload: dict) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / "report.json").write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    lines = [
        f"generated: {payload.get('generated')}",
        f"status: {payload.get('status')}",
        f"ui_dir: {payload.get('ui_dir')}",
        f"violations: {len(payload.get('violations', []))}",
        "",
    ]
    for v in payload.get("violations", []):
        lines.append(f"- {v.get('path')} :: {v.get('reason')}")
    (out_dir / "report.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--ui-dir", default="Tests.Godot/tests/UI", help="UI tests directory to hard-gate")
    ap.add_argument(
        "--forbid-name-regex",
        default=r"(?:^|[_-])demo(?:[_-]|\.)",
        help="Case-insensitive regex applied to filename only (default forbids *_demo* patterns).",
    )
    args = ap.parse_args()

    root = repo_root()
    ui_dir = (root / args.ui_dir).resolve()
    out_dir = root / "logs" / "ci" / ci_date() / "gdunit-gate-layout"

    violations: list[dict] = []
    rx = re.compile(args.forbid_name_regex, re.IGNORECASE)

    if not ui_dir.is_dir():
        payload = {
            "generated": dt.datetime.now(dt.UTC).isoformat(timespec="seconds"),
            "status": "fail",
            "ui_dir": str(ui_dir),
            "violations": [{"path": str(ui_dir), "reason": "UI_DIR_NOT_FOUND"}],
        }
        write_report(out_dir, payload=payload)
        print(f"[REPORT] {out_dir / 'report.json'}")
        print(f"[REPORT] {out_dir / 'report.txt'}")
        return 2

    for path in sorted(ui_dir.rglob("*.gd")):
        name = path.name
        if rx.search(name):
            rel = path.relative_to(root).as_posix()
            violations.append(
                {
                    "path": rel,
                    "reason": f"FORBIDDEN_TEST_NAME_MATCH: {args.forbid_name_regex}",
                }
            )

    status = "ok" if not violations else "fail"
    payload = {
        "generated": dt.datetime.now(dt.UTC).isoformat(timespec="seconds"),
        "status": status,
        "ui_dir": str(ui_dir.relative_to(root).as_posix()),
        "violations": violations,
    }
    write_report(out_dir, payload=payload)
    print(f"[REPORT] {out_dir / 'report.json'}")
    print(f"[REPORT] {out_dir / 'report.txt'}")
    return 0 if status == "ok" else 1


if __name__ == "__main__":
    raise SystemExit(main())
