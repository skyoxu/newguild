#!/usr/bin/env python3
"""
Quality gates entry for Windows (Godot+C# variant).

Current minimal implementation:
- Delegates to ci_pipeline.py `all` command, which runs:
  * dotnet tests + coverage (soft gate on coverage)
  * Godot self-check (hard gate)
  * encoding scan (soft gate)
  * SQL static scan (hard gate)
  * DB perf smoke (hard gate)

Usage (Windows):
  py -3 scripts/python/quality_gates.py all \
    --solution Game.sln --configuration Debug \
    --godot-bin "C:\\Godot\\Godot_v4.5.1-stable_mono_win64_console.exe" \
    --build-solutions

Optional gates (Task 23):
  - --validate-audit: validate logs/ci/<date>/security-audit.jsonl (hard gate when enabled)
  - --validate-perf: validate logs/perf/<date> perf summaries (hard gate when enabled)

Exit codes:
  0  all hard gates passed
  1  hard gate failed (dotnet tests or self-check)

This script is designed to be extended in Phase 13 to include
additional gates (GdUnit4 sets, smoke, perf, etc.).
"""

import argparse
import datetime as dt
import os
import subprocess
import sys


def run_ci_pipeline(solution: str, configuration: str, godot_bin: str, build_solutions: bool) -> int:
    args = [
        "py",
        "-3",
        "scripts/python/ci_pipeline.py",
        "all",
        "--solution",
        solution,
        "--configuration",
        configuration,
        "--godot-bin",
        godot_bin,
    ]
    if build_solutions:
        args.append("--build-solutions")

    proc = subprocess.run(args, text=True)
    return proc.returncode


def run_gdunit_hard(godot_bin: str) -> int:
    """Run hard-gate GdUnit4 set (Adapters/Config + Security).

    Goals:
    - Keep aligned with the hard-gate set in CI workflow.
    - Write reports under logs/e2e/quality-gates/gdunit-hard.
    """

    args = [
        "py",
        "-3",
        "scripts/python/run_gdunit.py",
        "--prewarm",
        "--godot-bin",
        godot_bin,
        "--project",
        "Tests.Godot",
        "--add",
        "tests/Adapters/Config",
        "--add",
        "tests/Security/Hard",
        "--timeout-sec",
        "300",
        "--rd",
        "logs/e2e/quality-gates/gdunit-hard",
    ]
    proc = subprocess.run(args, text=True)
    return proc.returncode


def run_smoke_headless(godot_bin: str) -> int:
    """Run the Python headless smoke in strict mode.

    - Uses Main scene as the entry.
    - mode=strict requires a marker or "[DB] opened" to pass.
    """

    args = [
        "py",
        "-3",
        "scripts/python/smoke_headless.py",
        "--godot-bin",
        godot_bin,
        "--project",
        ".",
        "--scene",
        "res://Game.Godot/Scenes/Main.tscn",
        "--timeout-sec",
        "5",
        "--mode",
        "strict",
    ]
    proc = subprocess.run(args, text=True)
    return proc.returncode


def validate_security_audit_logs() -> int:
    """Validate security-audit JSONL format and required fields.

    Goals:
    - Ensure logs are valid JSONL.
    - Ensure required fields {ts, action, reason, target, caller} exist.
    - Designed to be used by CI quality gates.
    """

    date = dt.date.today().strftime("%Y-%m-%d")
    audit_root = os.environ.get("AUDIT_LOG_ROOT") or os.path.join("logs", "ci", date)
    log_pattern = os.path.join(audit_root, "security-audit*.jsonl")
    report_path = os.path.join(audit_root, "audit-validation-report.json")

    args = [
        "py",
        "-3",
        "scripts/python/validate_audit_logs.py",
        "--log-path",
        log_pattern,
        "--check-sensitive",
        "--strict",
        "--report",
        report_path,
    ]
    proc = subprocess.run(args, text=True)
    return proc.returncode


def validate_perf_logs() -> int:
    """Validate perf summary P95 thresholds and emit a JSON report.

    Default behavior:
    - Prefer logs/perf/<date>/summary.json when present.
    - Fall back to logs/perf/<date>/db/db-perf-summary.json.
    - Emit logs/ci/<date>/quality-gates-perf.json.
    """

    date = dt.date.today().strftime("%Y-%m-%d")
    ci_dir = os.path.join("logs", "ci", date)
    perf_dir = os.environ.get("PERF_LOG_ROOT") or os.path.join("logs", "perf", date)

    summary_candidates = [
        os.path.join(perf_dir, "summary.json"),
        os.path.join(perf_dir, "db", "db-perf-summary.json"),
    ]
    summary_path = None
    for p in summary_candidates:
        if os.path.isfile(p):
            summary_path = p
            break

    if not summary_path:
        # Still call the validator to ensure a JSON report is produced (for CI evidence).
        summary_path = summary_candidates[0]

    report_path = os.path.join(ci_dir, "quality-gates-perf.json")

    # Default: 20ms budget (override via env).
    threshold_ms = os.environ.get("PERF_P95_THRESHOLD_MS") or os.environ.get("PERF_DB_P95_THRESHOLD_MS") or "20"
    metric = os.environ.get("PERF_P95_METRIC") or "DB_QUERY_P95"

    args = [
        "py",
        "-3",
        "scripts/python/validate_perf.py",
        "--summary-path",
        summary_path,
        "--metric",
        metric,
        "--threshold-ms",
        str(threshold_ms),
        "--report",
        report_path,
        "--strict",
    ]
    proc = subprocess.run(args, text=True)
    return proc.returncode


def main() -> int:
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="cmd", required=True)

    p_all = sub.add_parser("all", help="run quality gates (ci_pipeline + optional GdUnit/Smoke)")
    p_all.add_argument("--solution", default="Game.sln")
    p_all.add_argument("--configuration", default="Debug")
    p_all.add_argument("--godot-bin", required=True)
    p_all.add_argument("--build-solutions", action="store_true")
    p_all.add_argument("--gdunit-hard", action="store_true", help="run hard GdUnit set (Adapters/Config + Security)")
    p_all.add_argument("--smoke", action="store_true", help="run headless smoke (strict marker/DB check)")
    p_all.add_argument("--validate-audit", action="store_true", help="validate security-audit.jsonl format")
    p_all.add_argument("--validate-perf", action="store_true", help="validate perf summary (P95 thresholds)")

    args = parser.parse_args()

    if args.cmd == "all":
        # 1) Base gates: dotnet + self-check + encoding scan
        rc = run_ci_pipeline(args.solution, args.configuration, args.godot_bin, args.build_solutions)
        hard_failed = rc != 0

        # 2) Optional hard gate: GdUnit4 set
        if args.gdunit_hard:
            gd_rc = run_gdunit_hard(args.godot_bin)
            if gd_rc != 0:
                hard_failed = True

        # 3) Optional hard gate: headless smoke (strict mode)
        if args.smoke:
            sm_rc = run_smoke_headless(args.godot_bin)
            if sm_rc != 0:
                hard_failed = True

        # 4) Optional gate: security-audit.jsonl format validation
        if args.validate_audit:
            audit_rc = validate_security_audit_logs()
            if audit_rc != 0:
                print("[ERROR] Security audit log validation failed", file=sys.stderr)
                hard_failed = True

        # 5) Optional gate: perf summary validation (hard gate)
        if args.validate_perf:
            perf_rc = validate_perf_logs()
            if perf_rc != 0:
                print("[ERROR] Perf validation failed", file=sys.stderr)
                hard_failed = True

        return 0 if not hard_failed else 1

    print("Unsupported command", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
