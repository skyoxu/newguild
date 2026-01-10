#!/usr/bin/env python3
"""
Prepare Tests.Godot to reference runtime directory (e.g., Game.Godot) as res://Game.Godot.
Creates a directory junction (Windows) and verifies it points to the expected runtime.

This script is a hard gate by default:
  - If the link already exists but is not a junction, it fails (to avoid drift).
  - No copy fallback (copying creates a second source of truth).

Usage:
  py -3 scripts/python/prepare_gd_tests.py --project Tests.Godot --runtime Game.Godot
"""
import argparse
import datetime as dt
import os
import subprocess
import sys
from pathlib import Path
import json

try:
    import ctypes
except Exception:  # pragma: no cover
    ctypes = None

FILE_ATTRIBUTE_REPARSE_POINT = 0x0400

def is_windows():
    return os.name == 'nt'

def repo_root() -> Path:
    return Path(__file__).resolve().parents[2]

def today_str_utc() -> str:
    return dt.datetime.now(dt.UTC).strftime("%Y-%m-%d")

def ensure_dir(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)

def write_text(path: Path, content: str) -> None:
    ensure_dir(path.parent)
    path.write_text(content, encoding="utf-8", newline="\n")

def write_json(path: Path, payload: dict) -> None:
    ensure_dir(path.parent)
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")

def _get_file_attributes(path: str) -> int | None:
    if not is_windows() or ctypes is None:
        return None
    try:
        GetFileAttributesW = ctypes.windll.kernel32.GetFileAttributesW  # type: ignore[attr-defined]
        GetFileAttributesW.argtypes = [ctypes.c_wchar_p]
        GetFileAttributesW.restype = ctypes.c_uint32
        attrs = int(GetFileAttributesW(path))
        # INVALID_FILE_ATTRIBUTES = 0xFFFFFFFF
        if attrs == 0xFFFFFFFF:
            return None
        return attrs
    except Exception:
        return None

def is_reparse_point(path: Path) -> bool:
    attrs = _get_file_attributes(str(path))
    if attrs is None:
        return False
    return (attrs & FILE_ATTRIBUTE_REPARSE_POINT) != 0

def run_cmd(cmd: list[str], cwd: Path | None = None, timeout_sec: int = 15) -> tuple[int, str]:
    p = subprocess.Popen(
        cmd,
        cwd=str(cwd) if cwd else None,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        encoding="utf-8",
        errors="ignore",
    )
    try:
        out, _ = p.communicate(timeout=timeout_sec)
    except subprocess.TimeoutExpired:
        p.kill()
        out, _ = p.communicate()
        return 124, out or ""
    return int(p.returncode or 0), out or ""

def expected_link_and_target(*, project: Path, runtime: Path) -> tuple[Path, Path]:
    link = project / runtime.name
    target = runtime
    return link, target

def ensure_junction(*, link: Path, target: Path, allow_existing_dir: bool) -> tuple[int, str, dict]:
    meta: dict = {
        "link": str(link),
        "target": str(target),
        "link_exists": link.exists(),
        "link_is_reparse_point": False,
        "resolved_link": None,
        "resolved_target": None,
        "note": "",
    }

    if link.exists():
        if is_windows():
            meta["link_is_reparse_point"] = bool(is_reparse_point(link))

        if not meta["link_is_reparse_point"]:
            if allow_existing_dir:
                meta["note"] = "Existing directory allowed (allow_existing_dir=1); drift risk accepted."
                return 0, "LINK_EXISTS_DIR_ALLOWED\n", meta
            meta["note"] = "Existing directory is not a junction; refusing to proceed (prevents drift)."
            return 2, "LINK_EXISTS_NOT_JUNCTION\n", meta

        resolved_link = link.resolve()
        resolved_target = target.resolve()
        meta["resolved_link"] = str(resolved_link)
        meta["resolved_target"] = str(resolved_target)
        if resolved_link != resolved_target:
            meta["note"] = "Junction points to unexpected target."
            return 3, "JUNCTION_TARGET_MISMATCH\n", meta
        meta["note"] = "Junction exists and points to expected target."
        return 0, "JUNCTION_OK\n", meta

    if not is_windows():
        meta["note"] = "Windows junction required but OS is not Windows."
        return 4, "NOT_WINDOWS\n", meta

    ensure_dir(link.parent)
    cmd = ["cmd", "/c", "mklink", "/J", str(link), str(target)]
    rc, out = run_cmd(cmd, cwd=repo_root(), timeout_sec=15)
    if rc != 0 or not link.exists():
        meta["note"] = f"mklink failed (rc={rc})."
        return 5, out, meta

    meta["link_exists"] = True
    meta["link_is_reparse_point"] = bool(is_reparse_point(link))
    resolved_link = link.resolve()
    resolved_target = target.resolve()
    meta["resolved_link"] = str(resolved_link)
    meta["resolved_target"] = str(resolved_target)
    if not meta["link_is_reparse_point"] or resolved_link != resolved_target:
        meta["note"] = "Created link but it is not a junction to expected target."
        return 6, out, meta

    meta["note"] = "Junction created and verified."
    return 0, out, meta

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--project', default='Tests.Godot')
    ap.add_argument('--runtime', default='Game.Godot')
    ap.add_argument('--allow-existing-dir', action='store_true',
                    help='Allow an existing non-junction directory at Tests.Godot/<runtime> (NOT recommended; drift risk).')
    args = ap.parse_args()

    root = repo_root()
    proj = (root / args.project).resolve() if not os.path.isabs(args.project) else Path(args.project).resolve()
    runtime = (root / args.runtime).resolve() if not os.path.isabs(args.runtime) else Path(args.runtime).resolve()

    out_dir = root / "logs" / "ci" / today_str_utc() / "prepare-gd-tests"
    ensure_dir(out_dir)
    summary_path = out_dir / "summary.json"
    log_path = out_dir / "prepare.log"

    summary = {
        "cmd": "prepare_gd_tests",
        "project": str(proj),
        "runtime": str(runtime),
        "status": "fail",
        "rc": 1,
        "link": None,
        "target": None,
        "meta": None,
    }

    if not proj.is_dir():
        msg = f"PROJECT_NOT_FOUND: {proj}\n"
        write_text(log_path, msg)
        write_json(summary_path, summary | {"rc": 1, "status": "fail"})
        print(msg.rstrip())
        return 1
    if not runtime.is_dir():
        msg = f"RUNTIME_NOT_FOUND: {runtime}\n"
        write_text(log_path, msg)
        write_json(summary_path, summary | {"rc": 1, "status": "fail"})
        print(msg.rstrip())
        return 1

    link, target = expected_link_and_target(project=proj, runtime=runtime)
    summary["link"] = str(link)
    summary["target"] = str(target)

    rc, out, meta = ensure_junction(link=link, target=target, allow_existing_dir=bool(args.allow_existing_dir))
    summary["rc"] = int(rc)
    summary["status"] = "ok" if rc == 0 else "fail"
    summary["meta"] = meta

    write_text(log_path, out if out.endswith("\n") else (out + "\n"))
    write_json(summary_path, summary)
    print(f"PREPARE_GD_TESTS status={summary['status']} rc={rc} out={out_dir}")
    return 0 if rc == 0 else rc

if __name__ == '__main__':
    raise SystemExit(main())
