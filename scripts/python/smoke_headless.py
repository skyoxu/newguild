#!/usr/bin/env python3
"""Headless smoke test runner for Godot (Windows, Godot+C# template).

This is a Python equivalent of scripts/ci/smoke_headless.ps1 with two
behaviours:

- mode="loose" (default): never fails the build, only prints PASS hints;
- mode="strict": returns non-zero if we没有看到核心标记输出。

Heuristics与 PowerShell 版本保持一致：
- 优先匹配 "[TEMPLATE_SMOKE_READY]"；
- 其次匹配 "[DB] opened"；
- 最后只要有任何输出也视为 smoke 通过（loose 模式）。

用法示例（Windows）：

  py -3 scripts/python/smoke_headless.py \
    --godot-bin "C:\\Godot\\Godot_v4.5.1-stable_mono_win64_console.exe" \
    --project "." --scene "res://Game.Godot/Scenes/Main.tscn" \
    --timeout-sec 5 --mode loose

在 CI 中可以选择 --mode strict，并根据返回码决定是否作为门禁。
"""

from __future__ import annotations

import argparse
import datetime as _dt
import json
import os
import subprocess
import sys
from pathlib import Path

from godot_cli import build_userdir_args, default_user_dir


def _read_project_name(project_dir: Path) -> str | None:
    project_godot = project_dir / "project.godot"
    if not project_godot.is_file():
        return None

    try:
        for raw in project_godot.read_text(encoding="utf-8", errors="ignore").splitlines():
            line = raw.strip()
            if line.startswith("config/name=") and "\"" in line:
                return line.split("\"", 2)[1]
    except Exception:
        return None

    return None


def _run_smoke(
    godot_bin: str,
    project: str,
    scene: str,
    timeout_sec: int,
    mode: str,
    user_dir: str | None,
    userdir_flag: str,
) -> int:
    bin_path = Path(godot_bin)
    if not bin_path.is_file():
        print(f"[smoke_headless] GODOT_BIN not found: {godot_bin}", file=sys.stderr)
        return 1

    ts = _dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    dest = Path("logs") / "ci" / ts / "smoke"
    dest.mkdir(parents=True, exist_ok=True)

    out_path = dest / "headless.out.log"
    err_path = dest / "headless.err.log"
    log_path = dest / "headless.log"
    godot_log_path = dest / "godot.log"

    userdir_args, userdir_flag_used = build_userdir_args(str(bin_path), user_dir, preferred_flag=userdir_flag)

    # Godot 4.5 editor builds do not expose a CLI flag to redirect user data. In sandboxed
    # environments, writes to %APPDATA% may be blocked. As a fallback, override APPDATA for the
    # child process to keep user:// writes under the repo.
    env = os.environ.copy()
    appdata_override = None
    if user_dir and userdir_flag_used is None:
        try:
            appdata_override = str((Path(user_dir).resolve() / "_appdata").resolve())
            Path(appdata_override).mkdir(parents=True, exist_ok=True)
            env["APPDATA"] = appdata_override
            # Ensure user://logs exists; some projects enable file logging under user://logs
            proj_dir = Path(project).resolve()
            proj_name = _read_project_name(proj_dir) or (proj_dir.name if proj_dir.name not in {"", "."} else "project")
            (Path(appdata_override) / "Godot" / "app_userdata" / proj_name / "logs").mkdir(parents=True, exist_ok=True)
        except Exception:
            appdata_override = None

    cmd = [str(bin_path)] + userdir_args + [
        "--headless",
        "--path",
        project,
        "--scene",
        scene,
        "--log-file",
        str(godot_log_path.resolve()),
    ]
    print(f"[smoke_headless] starting Godot: {' '.join(cmd)} (timeout={timeout_sec}s)")

    with out_path.open("w", encoding="utf-8", errors="ignore") as f_out, \
            err_path.open("w", encoding="utf-8", errors="ignore") as f_err:
        try:
            proc = subprocess.Popen(cmd, stdout=f_out, stderr=f_err, text=True, env=env)
        except Exception as exc:  # pragma: no cover - environment issue
            print(f"[smoke_headless] failed to start Godot: {exc}", file=sys.stderr)
            return 1

        try:
            proc.wait(timeout=timeout_sec)
        except subprocess.TimeoutExpired:
            print("[smoke_headless] timeout reached; terminating Godot (expected for smoke)")
            try:
                proc.kill()
            except Exception:
                pass

    content_parts: list[str] = []
    if out_path.is_file():
        content_parts.append(out_path.read_text(encoding="utf-8", errors="ignore"))
    if err_path.is_file():
        content_parts.append("\n" + err_path.read_text(encoding="utf-8", errors="ignore"))
    if godot_log_path.is_file():
        content_parts.append("\n" + godot_log_path.read_text(encoding="utf-8", errors="ignore"))

    combined = "".join(content_parts)
    log_path.write_text(combined, encoding="utf-8", errors="ignore")
    print(f"[smoke_headless] log saved at {log_path} (out={out_path}, err={err_path})")

    text = combined or ""
    has_marker = "[TEMPLATE_SMOKE_READY]" in text
    has_db_open = "[DB] opened" in text
    has_any = bool(text.strip())

    # Determine status and message
    status = "unknown"
    message = ""
    exit_code = 0

    if has_marker:
        status = "pass"
        message = "SMOKE PASS (marker)"
        print(message)
    elif has_db_open:
        status = "pass"
        message = "SMOKE PASS (db opened)"
        print(message)
    elif has_any:
        status = "pass"
        message = "SMOKE PASS (any output)"
        print(message)
    else:
        status = "inconclusive"
        message = "SMOKE INCONCLUSIVE (no output). Check logs."
        print(message)

    if mode == "strict":
        # Strict mode requires a marker or DB opened line.
        if not (has_marker or has_db_open):
            status = "strict-failed"
            message = "SMOKE STRICT-FAILED: Required markers ([TEMPLATE_SMOKE_READY] or [DB] opened) not found"
            print(message, file=sys.stderr)
            exit_code = 1
        else:
            exit_code = 0
    else:
        # loose 模式永不作为硬门禁
        exit_code = 0

    # Generate selfcheck-summary.json
    summary = {
        "timestamp": _dt.datetime.now().isoformat(),
        "mode": mode,
        "status": status,
        "message": message,
        "has_marker": has_marker,
        "has_db_open": has_db_open,
        "has_any_output": has_any,
        "exit_code": exit_code,
        "godot_bin": godot_bin,
        "scene": scene,
        "timeout_sec": timeout_sec,
        "user_dir": user_dir,
        "userdir_flag_used": userdir_flag_used,
        "appdata_override": appdata_override,
        "godot_log_path": str(godot_log_path),
        "log_path": str(log_path),
        "out_path": str(out_path),
        "err_path": str(err_path)
    }

    summary_path = dest / "selfcheck-summary.json"
    summary_path.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"[smoke_headless] summary saved at {summary_path}")

    return exit_code


def main() -> int:
    parser = argparse.ArgumentParser(description="Run Godot headless smoke test (Python variant)")
    parser.add_argument("--godot-bin", required=True, help="Path to Godot executable (mono console)")
    parser.add_argument("--project", default=".", help="Godot project path (default '.')")
    parser.add_argument("--scene", default="res://Game.Godot/Scenes/Main.tscn", help="Scene to load")
    parser.add_argument("--timeout-sec", type=int, default=5, help="Timeout seconds before kill")
    parser.add_argument("--mode", choices=["loose", "strict"], default="loose", help="Gate mode")
    parser.add_argument("--user-dir", default=None, help="Redirect Godot user:// to this directory (default: logs/_godot_userdir/<project>/smoke)")
    parser.add_argument("--userdir-flag", default=os.environ.get("GODOT_USERDIR_FLAG", "auto"),
                        help="Godot CLI flag for user dir (auto|--user-dir|--user-data-dir); env: GODOT_USERDIR_FLAG")
    parser.add_argument("--no-userdir", action="store_true", help="Disable user dir redirection (writes to default OS location)")

    args = parser.parse_args()

    user_dir = None
    if not args.no_userdir:
        user_dir = args.user_dir or default_user_dir(args.project, suffix="smoke")
        try:
            Path(user_dir).mkdir(parents=True, exist_ok=True)
        except Exception:
            user_dir = None

    return _run_smoke(args.godot_bin, args.project, args.scene, args.timeout_sec, args.mode, user_dir, args.userdir_flag)


if __name__ == "__main__":
    sys.exit(main())
