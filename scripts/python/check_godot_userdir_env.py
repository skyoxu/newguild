#!/usr/bin/env python3
"""
Check GODOT_USERDIR / GODOT_USER_DIR for invalid values on Windows.

Why:
- Godot CLI userdir expects a filesystem directory path.
- If userdir is misconfigured to a Godot virtual path like "user://" or "user:",
  ProjectSettings.GlobalizePath("user://...") can produce invalid Windows paths
  (e.g. "...\\user:"), causing System.IO.IOException during tests.

This script checks three scopes:
- process: current process environment variables
- user: persisted environment variables in HKCU\\Environment
- machine: persisted environment variables in HKLM\\...\\Environment

Reports:
- Writes JSON and TXT reports under logs/ci/<YYYY-MM-DD>/ by default.
- Prints a short summary to stdout.

Safety:
- This script does NOT change any environment variables unless you pass
  --unset-user and --confirm UNSET.
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

try:
    import winreg  # type: ignore
except Exception:  # pragma: no cover
    winreg = None  # type: ignore


_WIN_ABS_DRIVE_RE = re.compile(r"^[A-Za-z]:[\\/]")
_WIN_DRIVE_ONLY_RE = re.compile(r"^[A-Za-z]:$")
_WIN_DRIVE_REL_RE = re.compile(r"^[A-Za-z]:[^\\/]")


def _now_iso() -> str:
    return dt.datetime.now().isoformat(timespec="seconds")


def _today() -> str:
    return dt.date.today().strftime("%Y-%m-%d")


def _normalize_raw(value: Optional[str]) -> Optional[str]:
    if value is None:
        return None
    v = value.strip()
    if not v:
        return None
    if len(v) >= 2 and v[0] == v[-1] and v[0] in {"'", '"'}:
        v = v[1:-1].strip()
    return v or None


def _validate_userdir_value(value: str) -> Tuple[bool, Optional[str]]:
    v = value.strip()
    low = v.lower()

    # Godot virtual paths are invalid here.
    if low.startswith("user:") or low.startswith("res:"):
        return False, "Godot virtual path is not allowed for userdir (use a filesystem path)"

    # Windows colon rules:
    # - Allowed as drive prefix only: C:\...
    # - Not allowed: user:, C:, C:relative\path
    if ":" in v:
        if v.startswith("\\\\"):
            # UNC path; ':' should not appear, but allow it as we can't fully validate here.
            return True, None

        if not _WIN_ABS_DRIVE_RE.match(v):
            return False, "Contains ':' but is not an absolute drive path like C:\\path\\to\\dir"
        if _WIN_DRIVE_ONLY_RE.match(v) or _WIN_DRIVE_REL_RE.match(v):
            return False, "Drive-relative path like 'C:folder' or 'C:' is not allowed"

    return True, None


def _read_reg_env_value(scope: str, name: str) -> Tuple[Optional[str], Optional[str]]:
    if winreg is None:
        return None, "winreg_unavailable"

    if scope == "user":
        root = winreg.HKEY_CURRENT_USER
        subkey = r"Environment"
    elif scope == "machine":
        root = winreg.HKEY_LOCAL_MACHINE
        subkey = r"SYSTEM\CurrentControlSet\Control\Session Manager\Environment"
    else:
        return None, f"unsupported_scope:{scope}"

    try:
        with winreg.OpenKey(root, subkey, 0, winreg.KEY_READ) as k:
            val, _typ = winreg.QueryValueEx(k, name)
            return str(val), None
    except FileNotFoundError:
        return None, None
    except OSError as e:
        return None, f"{type(e).__name__}: {e}"


def _delete_user_reg_env_value(name: str) -> Optional[str]:
    if winreg is None:
        return "winreg_unavailable"
    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Environment", 0, winreg.KEY_SET_VALUE) as k:
            try:
                winreg.DeleteValue(k, name)
                return None
            except FileNotFoundError:
                return None
    except OSError as e:
        return f"{type(e).__name__}: {e}"


def _broadcast_env_change() -> Optional[str]:
    try:
        import ctypes
        from ctypes import wintypes

        HWND_BROADCAST = 0xFFFF
        WM_SETTINGCHANGE = 0x001A
        SMTO_ABORTIFHUNG = 0x0002

        SendMessageTimeoutW = ctypes.windll.user32.SendMessageTimeoutW  # type: ignore[attr-defined]
        SendMessageTimeoutW.argtypes = [
            wintypes.HWND,
            wintypes.UINT,
            wintypes.WPARAM,
            wintypes.LPCWSTR,
            wintypes.UINT,
            wintypes.UINT,
            ctypes.POINTER(wintypes.DWORD),
        ]
        SendMessageTimeoutW.restype = wintypes.LPARAM

        result = wintypes.DWORD(0)
        SendMessageTimeoutW(
            HWND_BROADCAST,
            WM_SETTINGCHANGE,
            0,
            "Environment",
            SMTO_ABORTIFHUNG,
            5000,
            ctypes.byref(result),
        )
        return None
    except Exception as e:  # pragma: no cover
        return f"{type(e).__name__}: {e}"


@dataclass(frozen=True)
class CheckItem:
    scope: str  # process | user | machine
    name: str
    raw: Optional[str]
    normalized: Optional[str]
    ok: bool
    reason: Optional[str]
    exists: Optional[bool] = None
    read_error: Optional[str] = None


def _check_one(scope: str, name: str) -> CheckItem:
    raw: Optional[str] = None
    read_error: Optional[str] = None

    if scope == "process":
        raw = os.environ.get(name)
    else:
        raw, read_error = _read_reg_env_value(scope, name)

    normalized = _normalize_raw(raw)
    if normalized is None:
        return CheckItem(scope=scope, name=name, raw=raw, normalized=None, ok=True, reason=None, exists=None, read_error=read_error)

    ok, reason = _validate_userdir_value(normalized)
    exists: Optional[bool] = None
    try:
        exists = Path(normalized).exists()
    except Exception:
        exists = None
    return CheckItem(
        scope=scope,
        name=name,
        raw=raw,
        normalized=normalized,
        ok=ok,
        reason=reason,
        exists=exists,
        read_error=read_error,
    )


def _write_report(out_dir: Path, items: List[CheckItem]) -> Tuple[Path, Path]:
    out_dir.mkdir(parents=True, exist_ok=True)
    json_path = out_dir / "godot-userdir-env-check.json"
    txt_path = out_dir / "godot-userdir-env-check.txt"

    invalid = [it for it in items if not it.ok]
    warnings: List[str] = []
    for it in items:
        if it.normalized and it.exists is False:
            warnings.append(f"{it.scope}:{it.name} points to a non-existent path: {it.normalized}")

    data: Dict[str, Any] = {
        "ts": _now_iso(),
        "cwd": os.getcwd(),
        "python": sys.version,
        "items": [it.__dict__ for it in items],
        "summary": {
            "ok": len(invalid) == 0,
            "invalid_count": len(invalid),
            "warnings": warnings,
        },
    }
    json_path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")

    lines: List[str] = []
    lines.append(f"ts={data['ts']}")
    lines.append(f"cwd={data['cwd']}")
    lines.append("")
    for it in items:
        v = it.normalized if it.normalized is not None else "<unset>"
        status = "OK" if it.ok else "INVALID"
        extra = ""
        if it.read_error:
            extra += f" read_error={it.read_error}"
        if it.exists is False:
            extra += " path_exists=false"
        if it.reason:
            extra += f" reason={it.reason}"
        lines.append(f"{status} scope={it.scope} name={it.name} value={v}{extra}")
    if warnings:
        lines.append("")
        lines.append("WARNINGS:")
        lines.extend([f"- {w}" for w in warnings])

    txt_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return json_path, txt_path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--report-dir", default=None, help="Output directory for reports (default: logs/ci/<YYYY-MM-DD>/)")
    parser.add_argument("--strict", action="store_true", help="Treat warnings (missing path) as failure")
    parser.add_argument("--unset-user", action="store_true", help="Delete user-level persisted env vars (HKCU) for GODOT_USERDIR and GODOT_USER_DIR")
    parser.add_argument("--confirm", default=None, help="Required confirmation token for destructive operations")
    args = parser.parse_args()

    out_dir = Path(args.report_dir) if args.report_dir else (Path.cwd() / "logs" / "ci" / _today())

    names = ["GODOT_USERDIR", "GODOT_USER_DIR"]
    scopes = ["process", "user", "machine"]
    items = [_check_one(scope, name) for scope in scopes for name in names]

    json_path, txt_path = _write_report(out_dir, items)

    invalid = [it for it in items if not it.ok]
    warnings = [it for it in items if it.normalized and it.exists is False]

    print(f"[REPORT] {json_path}")
    print(f"[REPORT] {txt_path}")

    if args.unset_user:
        if args.confirm != "UNSET":
            print("[ERROR] Refusing to unset user env vars without --confirm UNSET", file=sys.stderr)
            return 2
        for name in names:
            err = _delete_user_reg_env_value(name)
            if err:
                print(f"[ERROR] Failed to delete HKCU Environment value: {name} ({err})", file=sys.stderr)
                return 2
            print(f"[OK] Deleted HKCU Environment value: {name}")
        b_err = _broadcast_env_change()
        if b_err:
            print(f"[WARN] Failed to broadcast env change: {b_err}", file=sys.stderr)
        print("[NOTE] Restart your PowerShell terminal to ensure environment changes are applied.")

    if invalid:
        print(f"[FAIL] Invalid values detected: {len(invalid)}", file=sys.stderr)
        for it in invalid:
            v = it.normalized if it.normalized else "<unset>"
            r = it.reason or "unknown"
            print(f"  - scope={it.scope} name={it.name} value={v} reason={r}", file=sys.stderr)
        return 1

    if args.strict and warnings:
        print(f"[FAIL] Warnings detected in strict mode: {len(warnings)}", file=sys.stderr)
        for it in warnings:
            print(f"  - scope={it.scope} name={it.name} value={it.normalized} path_exists=false", file=sys.stderr)
        return 1

    print("[OK] No invalid values detected.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

