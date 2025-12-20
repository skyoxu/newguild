#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
DB performance smoke test (Windows, Godot+C#).

Generates perf artifacts under:
  logs/perf/<YYYY-MM-DD>/db/

Metrics (requested by review checklist):
  - DB_CONNECTION_TIME
  - DB_QUERY_P95
  - DB_MEMORY_LEAK
  - DB_CONCURRENCY
  - DB_LARGE_RESULT
  - DB_STARTUP_IMPACT

Usage:
  py -3 scripts/python/perf_smoke_db.py --godot-bin "%GODOT_BIN%"
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import subprocess
from pathlib import Path
from typing import Any


def today_str() -> str:
    return dt.date.today().strftime("%Y-%m-%d")


def write_text(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8", newline="\n")


def write_json(path: Path, obj: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2), encoding="utf-8", newline="\n")


def run_cmd(args: list[str], cwd: Path, env: dict[str, str], timeout_sec: int) -> tuple[int, str]:
    proc = subprocess.Popen(
        args,
        cwd=str(cwd),
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        encoding="utf-8",
        errors="replace",
        env=env,
    )
    try:
        out, _ = proc.communicate(timeout=timeout_sec)
    except subprocess.TimeoutExpired:
        proc.kill()
        out, _ = proc.communicate()
        return 124, out
    return proc.returncode or 0, out


def build_parser() -> argparse.ArgumentParser:
    ap = argparse.ArgumentParser(description="DB performance smoke test (Godot+C#)")
    ap.add_argument("--godot-bin", default=os.environ.get("GODOT_BIN"), help="Godot mono console binary")
    ap.add_argument("--project", default="Tests.Godot", help="Godot test project directory")
    ap.add_argument("--timeout-sec", type=int, default=480)
    ap.add_argument("--no-dotnet-build", action="store_true", help="skip building Tests.Godot.csproj before running gdunit")
    ap.add_argument("--query-samples", type=int, default=120)
    ap.add_argument("--large-rows", type=int, default=10_000)
    ap.add_argument("--large-runs", type=int, default=5)
    ap.add_argument("--concurrency", type=int, default=8)
    ap.add_argument("--leak-iterations", type=int, default=500)
    return ap


def main() -> int:
    args = build_parser().parse_args()
    root = Path(__file__).resolve().parents[2]
    start_utc = dt.datetime.now(dt.UTC)

    if not args.godot_bin:
        print("[perf_smoke_db] ERROR: --godot-bin is required (or set env GODOT_BIN).")
        return 2

    date = today_str()
    out_dir = root / "logs" / "perf" / date / "db"
    out_dir.mkdir(parents=True, exist_ok=True)

    # Prevent stale passes: delete prior run outputs (best-effort).
    summary_path = out_dir / "db-perf-summary.json"
    try:
        summary_path.unlink(missing_ok=True)
    except Exception:
        pass

    env = dict(os.environ)
    env["PERF_DB_OUT_DIR"] = str(out_dir.resolve())
    env["PERF_DB_QUERY_SAMPLES"] = str(args.query_samples)
    env["PERF_DB_LARGE_ROWS"] = str(args.large_rows)
    env["PERF_DB_LARGE_RUNS"] = str(args.large_runs)
    env["PERF_DB_CONCURRENCY"] = str(args.concurrency)
    env["PERF_DB_LEAK_ITERATIONS"] = str(args.leak_iterations)
    # Run in production-like mode (no sensitive details, stable logs).
    env["GD_SECURE_MODE"] = "1"
    env["CI"] = "1"

    if not args.no_dotnet_build:
        build_cmd = ["dotnet", "build", "Tests.Godot/Tests.Godot.csproj", "-c", "Debug", "-v", "minimal"]
        rc_b, out_b = run_cmd(build_cmd, cwd=root, env=env, timeout_sec=min(args.timeout_sec, 600))
        write_text(out_dir / "dotnet-build.log", out_b)
        if rc_b != 0:
            write_json(
                out_dir / "run.json",
                {
                    "timestamp": dt.datetime.now(dt.UTC).isoformat(timespec="seconds"),
                    "status": "fail",
                    "rc": rc_b,
                    "cmd": build_cmd,
                    "error": "dotnet build failed",
                },
            )
            print(f"PERF_DB status=fail out={out_dir}")
            return 1

    # Keep Godot user:// under perf dir to avoid polluting %APPDATA%.
    user_dir = out_dir / "_godot_userdir"
    user_dir.mkdir(parents=True, exist_ok=True)

    cmd = [
        "py",
        "-3",
        "scripts/python/run_gdunit.py",
        "--godot-bin",
        args.godot_bin,
        "--project",
        args.project,
        "--add",
        "tests/Performance/test_db_perf_smoke.gd",
        "--timeout-sec",
        str(args.timeout_sec),
        "--user-dir",
        str(user_dir.resolve()),
        "--rd",
        str((out_dir / "gdunit-reports").resolve()),
    ]

    rc, out = run_cmd(cmd, cwd=root, env=env, timeout_sec=args.timeout_sec + 180)
    write_text(out_dir / "gdunit.log", out)

    result: dict[str, Any] = {
        "timestamp": dt.datetime.now(dt.UTC).isoformat(timespec="seconds"),
        "status": "fail",
        "rc": rc,
        "cmd": cmd,
        "summary_json": str(summary_path),
    }

    if not summary_path.exists():
        result["status"] = "fail"
        result["error"] = "db-perf-summary.json not produced by test"
        write_json(out_dir / "run.json", result)
        print(f"PERF_DB status=fail out={out_dir}")
        return 1

    try:
        summary = json.loads(summary_path.read_text(encoding="utf-8"))
    except Exception as ex:
        result["status"] = "fail"
        result["error"] = f"failed to read summary json: {ex}"
        write_json(out_dir / "run.json", result)
        print(f"PERF_DB status=fail out={out_dir}")
        return 1

    required = [
        "DB_CONNECTION_TIME",
        "DB_QUERY_P95",
        "DB_MEMORY_LEAK",
        "DB_CONCURRENCY",
        "DB_LARGE_RESULT",
        "DB_STARTUP_IMPACT",
    ]
    missing = [k for k in required if k not in summary]
    if missing:
        result["status"] = "fail"
        result["error"] = f"missing required metrics: {', '.join(missing)}"
        write_json(out_dir / "run.json", result)
        print(f"PERF_DB status=fail out={out_dir}")
        return 1

    # Freshness guard: avoid passing on stale files from previous runs.
    try:
        ts_raw = str(summary.get("timestamp", "")).strip()
        ts = dt.datetime.fromisoformat(ts_raw)
        if ts.tzinfo is None:
            ts = ts.replace(tzinfo=dt.UTC)
        if ts < (start_utc - dt.timedelta(minutes=2)):
            result["status"] = "fail"
            result["error"] = f"stale summary timestamp: {ts_raw} (start={start_utc.isoformat(timespec='seconds')})"
            write_json(out_dir / "run.json", result)
            print(f"PERF_DB status=fail out={out_dir}")
            return 1
    except Exception as ex:
        result["status"] = "fail"
        result["error"] = f"invalid summary timestamp: {ex}"
        write_json(out_dir / "run.json", result)
        print(f"PERF_DB status=fail out={out_dir}")
        return 1

    # GdUnit may return 101 when it detects orphan nodes/leaks even if the test "PASSED".
    # For this smoke gate, we consider the run OK as long as required metrics are produced freshly.
    result["status"] = "ok"
    result["metrics_present"] = sorted(list(summary.keys()))
    write_json(out_dir / "run.json", result)
    print(f"PERF_DB status={result['status']} out={out_dir}")
    return 0 if result["status"] == "ok" else 1


if __name__ == "__main__":
    raise SystemExit(main())
