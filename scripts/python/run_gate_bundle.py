#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Run grouped hard/soft gates for local and CI usage.

This is a localized gate bundle for newguild:
- keep upstream CLI and output shape where practical
- only wire gates that actually exist in this repository
- fail on real gate failures, not on missing upstream-only scripts
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import subprocess
import sys
from pathlib import Path
from typing import Any

try:
    from gate_bundle_retention import prune_gate_bundle_runs
except ImportError:
    from scripts.python.gate_bundle_retention import prune_gate_bundle_runs


ROOT = Path(__file__).resolve().parents[2]


def _today() -> str:
    return dt.date.today().strftime("%Y-%m-%d")


def _default_run_id() -> str:
    gh_run = os.getenv("GITHUB_RUN_ID", "").strip()
    gh_attempt = os.getenv("GITHUB_RUN_ATTEMPT", "").strip()
    if gh_run:
        return f"gh-{gh_run}" + (f"-a{gh_attempt}" if gh_attempt else "")

    ci_pipeline = os.getenv("CI_PIPELINE_ID", "").strip()
    if ci_pipeline:
        return f"ci-{ci_pipeline}"

    build_id = os.getenv("BUILD_BUILDID", "").strip()
    if build_id:
        return f"build-{build_id}"

    ts = dt.datetime.now(dt.timezone.utc).strftime("%H%M%S-%f")
    return f"local-{ts}-{os.getpid()}"


def _default_out_root(run_id: str) -> Path:
    return ROOT / "logs" / "ci" / _today() / "gate-bundle" / "runs" / run_id


def _write_json(path: Path, payload: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")


def _run_command(cmd: list[str], log_path: Path) -> tuple[int, str]:
    proc = subprocess.run(
        cmd,
        cwd=ROOT,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        encoding="utf-8",
        errors="ignore",
        check=False,
    )
    output = proc.stdout or ""
    log_path.parent.mkdir(parents=True, exist_ok=True)
    log_path.write_text(output, encoding="utf-8", newline="\n")
    return proc.returncode, output


def _git_ref_exists(ref: str) -> bool:
    proc = subprocess.run(
        ["git", "rev-parse", "--verify", ref],
        cwd=ROOT,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        check=False,
    )
    return proc.returncode == 0


def _allowlist_files() -> list[str]:
    candidates = [
        ROOT / "docs/workflows/unified-pipeline-command-whitelist.txt",
        ROOT / "scripts/python/check_test_naming.allowlist.txt",
        ROOT / "scripts/python/check_test_naming.strict.allowlist.txt",
    ]
    return [str(path.relative_to(ROOT)).replace("\\", "/") for path in candidates if path.exists()]


def _resolve_gate_command(name: str, cmd: list[str], out_dir: Path) -> list[str]:
    resolved = [str(x) for x in cmd]
    if name == "check_tasks_all_refs" and "--summary-out" not in resolved:
        resolved.extend(["--summary-out", str((out_dir / "check-tasks-all-refs-summary.json")).replace("\\", "/")])
    elif name == "task_links_validate" and "--summary-out" not in resolved:
        resolved.extend(["--summary-out", str((out_dir / "task-links-validate-summary.json")).replace("\\", "/")])
    elif name == "check_domain_contracts" and "--out" not in resolved:
        resolved.extend(["--out", str((out_dir / "domain-contracts-summary.json")).replace("\\", "/")])
    return resolved


def _hard_gate_commands(task_files: list[str], task_links_max_warnings: int) -> list[dict[str, Any]]:
    commands: list[dict[str, Any]] = []

    whitelist = ROOT / "docs/workflows/unified-pipeline-command-whitelist.txt"
    manual_triplet_cmd = [
        "py",
        "-3",
        "scripts/python/forbid_manual_sc_triplet_examples.py",
        "--root",
        ".",
        "--mode",
        "diff",
        "--whitelist-metadata",
        "require",
    ]
    if whitelist.exists():
        manual_triplet_cmd.extend(["--whitelist", str(whitelist.relative_to(ROOT)).replace("\\", "/")])
    commands.append({"name": "forbid_manual_sc_triplet_examples", "cmd": manual_triplet_cmd})

    commands.extend(
        [
            {
                "name": "forbid_mirror_path_refs",
                "cmd": ["py", "-3", "scripts/python/forbid_mirror_path_refs.py", "--root", "."],
            },
            {
                "name": "validate_contracts",
                "cmd": ["py", "-3", "scripts/python/validate_contracts.py", "--root", "."],
            },
            {
                "name": "check_domain_contracts",
                "cmd": ["py", "-3", "scripts/python/check_domain_contracts.py"],
            },
            {
                "name": "task_links_validate",
                "cmd": [
                    "py",
                    "-3",
                    "scripts/python/task_links_validate.py",
                    "--mode",
                    "all",
                    "--max-warnings",
                    str(task_links_max_warnings),
                ],
            },
            {
                "name": "check_tasks_all_refs",
                "cmd": [
                    "py",
                    "-3",
                    "scripts/python/check_tasks_all_refs.py",
                    "--max-warnings",
                    str(task_links_max_warnings),
                ],
            },
            {
                "name": "check_test_naming",
                "cmd": [
                    "py",
                    "-3",
                    "scripts/python/check_test_naming.py",
                    "--style",
                    "auto",
                    "--allowlist",
                    "scripts/python/check_test_naming.allowlist.txt",
                ],
            },
        ]
    )

    # task_files is kept for CLI compatibility and future expansion.
    _ = task_files
    return commands


def _soft_gate_commands(task_files: list[str]) -> list[dict[str, Any]]:
    commands: list[dict[str, Any]] = [
        {
            "name": "warn_whitelist_expiry",
            "cmd": [
                "py",
                "-3",
                "scripts/python/warn_whitelist_expiry.py",
                "--root",
                ".",
                "--warn-days",
                str(int(os.getenv("WHITELIST_WARN_DAYS", "90") or "90")),
                "--fail-on-expired",
            ],
        },
        {
            "name": "check_eol_policy",
            "cmd": ["py", "-3", "scripts/python/check_eol_policy.py", "--strict"],
        },
    ]

    allowlist_files = _allowlist_files()
    if allowlist_files and _git_ref_exists("origin/main"):
        cmd = [
            "py",
            "-3",
            "scripts/python/check_allowlist_growth.py",
            "--base-ref",
            "origin/main",
            "--allow-bootstrap",
        ]
        for file_path in allowlist_files:
            cmd.extend(["--file", file_path])
        commands.append({"name": "check_allowlist_growth", "cmd": cmd})

    _ = task_files
    return commands


def _run_group(mode: str, commands: list[dict[str, Any]], strict_soft: bool, out_dir: Path, run_id: str) -> tuple[int, dict[str, Any]]:
    summary: dict[str, Any] = {
        "ts": dt.datetime.now(dt.timezone.utc).isoformat(),
        "action": "gate-bundle",
        "mode": mode,
        "run_id": run_id,
        "out_dir": str(out_dir).replace("\\", "/"),
        "status": "ok",
        "total": 0,
        "passed": 0,
        "failed": 0,
        "skipped": 0,
        "gates": [],
    }
    out_dir.mkdir(parents=True, exist_ok=True)

    for spec in commands:
        name = str(spec["name"])
        cmd = _resolve_gate_command(name, list(spec["cmd"]), out_dir)
        script_path = cmd[2] if len(cmd) >= 3 else ""
        if script_path and not (ROOT / script_path).exists():
            gate = {
                "name": name,
                "status": "skipped",
                "rc": 0,
                "reason": "script_missing_locally",
                "cmd": cmd,
            }
            summary["gates"].append(gate)
            summary["skipped"] += 1
            continue

        log_path = out_dir / f"{name}.log"
        rc, _ = _run_command(cmd, log_path)
        gate = {
            "name": name,
            "status": "ok" if rc == 0 else "fail",
            "rc": rc,
            "cmd": cmd,
            "log": str(log_path).replace("\\", "/"),
        }
        summary["gates"].append(gate)
        summary["total"] += 1
        if rc == 0:
            summary["passed"] += 1
        else:
            summary["failed"] += 1

    if summary["failed"]:
        summary["status"] = "fail"

    _write_json(out_dir / "summary.json", summary)
    print(
        f"GATE_BUNDLE_GROUP status={summary['status']} mode={mode} "
        f"failed={summary['failed']} skipped={summary['skipped']} "
        f"out={str((out_dir / 'summary.json')).replace('\\', '/')}"
    )

    if mode == "soft" and not strict_soft:
        return 0, summary
    return (0 if summary["failed"] == 0 else 1), summary


def main() -> int:
    try:
        env_task_links_budget = int((os.getenv("TASK_LINKS_MAX_WARNINGS", "") or "-1").strip())
    except ValueError:
        env_task_links_budget = -1

    parser = argparse.ArgumentParser(description="Run grouped hard/soft gates.")
    parser.add_argument("--mode", choices=["hard", "soft", "all"], default="all")
    parser.add_argument("--strict-soft", action="store_true", help="Return non-zero if any soft gate fails.")
    parser.add_argument(
        "--task-links-max-warnings",
        type=int,
        default=env_task_links_budget,
        help="Warning budget for task link gates; -1 disables budget checks.",
    )
    parser.add_argument(
        "--stability-template-hard",
        action="store_true",
        help="Accepted for upstream CLI compatibility; no-op in this repository.",
    )
    parser.add_argument(
        "--task-files",
        nargs="*",
        default=[".taskmaster/tasks/tasks_back.json", ".taskmaster/tasks/tasks_gameplay.json"],
        help="Task view files passed through for CLI compatibility.",
    )
    parser.add_argument("--out-dir", default="", help="Optional output root.")
    parser.add_argument("--run-id", default="", help="Optional fixed run id.")
    parser.add_argument("--retention-days", type=int, default=14)
    parser.add_argument("--max-runs-per-day", type=int, default=20)
    parser.add_argument("--skip-prune-runs", action="store_true")
    args = parser.parse_args()

    if args.retention_days < 0:
        print("GATE_BUNDLE status=fail reason=invalid-retention-days")
        return 2
    if args.max_runs_per_day < 1:
        print("GATE_BUNDLE status=fail reason=invalid-max-runs-per-day")
        return 2

    run_id = str(args.run_id or "").strip() or _default_run_id()
    out_root = Path(args.out_dir) if args.out_dir else _default_out_root(run_id)

    hard_commands = _hard_gate_commands(args.task_files, args.task_links_max_warnings)
    soft_commands = _soft_gate_commands(args.task_files)

    if args.mode == "hard":
        rc, _ = _run_group("hard", hard_commands, args.strict_soft, out_root / "hard", run_id)
    elif args.mode == "soft":
        rc, _ = _run_group("soft", soft_commands, args.strict_soft, out_root / "soft", run_id)
    else:
        hard_rc, hard_summary = _run_group("hard", hard_commands, args.strict_soft, out_root / "hard", run_id)
        soft_rc, soft_summary = _run_group("soft", soft_commands, args.strict_soft, out_root / "soft", run_id)
        combined = {
            "ts": dt.datetime.now(dt.timezone.utc).isoformat(),
            "action": "gate-bundle",
            "mode": "all",
            "run_id": run_id,
            "out_dir": str(out_root).replace("\\", "/"),
            "status": "ok" if hard_rc == 0 and soft_rc == 0 else "fail",
            "hard": hard_summary,
            "soft": soft_summary,
        }
        _write_json(out_root / "summary.json", combined)
        print(
            f"GATE_BUNDLE status={combined['status']} mode=all hard_failed={hard_summary['failed']} "
            f"soft_failed={soft_summary['failed']} out={str((out_root / 'summary.json')).replace('\\', '/')}"
        )
        rc = 0 if combined["status"] == "ok" else 1

    if not args.skip_prune_runs:
        prune = prune_gate_bundle_runs(
            ROOT / "logs" / "ci",
            retention_days=args.retention_days,
            max_runs_per_day=args.max_runs_per_day,
            keep_run_id=run_id,
        )
        _write_json(out_root / "prune-summary.json", prune)
        print(
            "GATE_BUNDLE_PRUNE "
            f"deleted={prune['deleted_count']} failed={prune['failed_count']} "
            f"out={str((out_root / 'prune-summary.json')).replace('\\', '/')}"
        )

    return rc


if __name__ == "__main__":
    sys.exit(main())
