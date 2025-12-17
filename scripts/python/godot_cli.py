#!/usr/bin/env python3
"""
Godot CLI helpers (Windows-friendly).

Goals:
- Redirect Godot `user://` to a repo-local directory under `logs/` to avoid polluting %APPDATA%.
- Detect the supported Godot CLI flag for user dir via `--help` (best-effort).

Environment overrides:
- GODOT_USERDIR / GODOT_USER_DIR: explicit user dir path to use (filesystem path only; do NOT use 'user://')
- GODOT_USERDIR_FLAG: force the CLI flag name (e.g. --user-dir / --user-data-dir)
"""

from __future__ import annotations

import os
import re
import subprocess
from pathlib import Path
from typing import List, Optional, Tuple


USERDIR_FLAG_CANDIDATES: List[str] = [
    "--user-dir",
    "--userdir",
    "--user-data-dir",
    "--user-data-dir",
]


_WIN_ABS_DRIVE_RE = re.compile(r"^[A-Za-z]:[\\/]")
_WIN_DRIVE_ONLY_RE = re.compile(r"^[A-Za-z]:$")
_WIN_DRIVE_REL_RE = re.compile(r"^[A-Za-z]:[^\\/]")


def validate_user_dir_path(user_dir: str, *, source: str = "user_dir") -> str:
    """Validate a filesystem path for Godot user dir on Windows.

    This MUST be a real filesystem path (absolute or relative), NOT a Godot virtual path.
    """

    user_dir = (user_dir or "").strip()
    if not user_dir:
        raise ValueError(f"{source} is empty")

    lowered = user_dir.lower()
    if lowered.startswith("user://") or lowered.startswith("res://"):
        raise ValueError(
            f"{source} must be a filesystem path, not a Godot virtual path like 'user://' (got: {user_dir!r})"
        )

    # Windows: ':' is only valid as a drive separator like 'C:\\'.
    if ":" in user_dir:
        if user_dir.startswith("\\\\"):
            # UNC path; ':' should never appear here.
            raise ValueError(f"{source} contains ':' which is invalid for UNC paths (got: {user_dir!r})")

        if not _WIN_ABS_DRIVE_RE.match(user_dir):
            raise ValueError(
                f"{source} contains ':' but is not an absolute drive path like 'C:\\\\...' (got: {user_dir!r})"
            )
        if _WIN_DRIVE_ONLY_RE.match(user_dir) or _WIN_DRIVE_REL_RE.match(user_dir):
            raise ValueError(
                f"{source} must be absolute like 'C:\\\\path\\\\to\\\\dir' (got: {user_dir!r})"
            )

    return user_dir


def default_user_dir(project_path: str, *, root_dir: Optional[str] = None, suffix: Optional[str] = None) -> str:
    """Compute a default repo-local user dir under logs/_godot_userdir/<project>[/suffix]."""

    env_dir = os.environ.get("GODOT_USERDIR") or os.environ.get("GODOT_USER_DIR")
    if env_dir:
        return validate_user_dir_path(env_dir, source="env GODOT_USERDIR/GODOT_USER_DIR")

    base = Path(root_dir) if root_dir else Path.cwd()
    name = Path(project_path).name if project_path else "project"
    if name in {"", "."}:
        name = "project"

    out = base / "logs" / "_godot_userdir" / name
    if suffix:
        out = out / suffix
    return str(out)


def detect_userdir_flag(godot_bin: str, preferred_flag: str = "auto") -> Optional[str]:
    """Detect the CLI flag for setting Godot user dir via `godot --help`.

    Returns a flag string like '--user-dir' or None if detection failed.
    """

    forced = os.environ.get("GODOT_USERDIR_FLAG")
    if forced:
        return forced.strip()

    if preferred_flag and preferred_flag != "auto":
        return preferred_flag.strip()

    try:
        proc = subprocess.run(
            [godot_bin, "--help"],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="ignore",
            timeout=8,
            check=False,
        )
        help_text = (proc.stdout or "") + "\n" + (proc.stderr or "")
    except Exception:
        return None

    for flag in USERDIR_FLAG_CANDIDATES:
        if flag in help_text:
            return flag
    return None


def build_userdir_args(
    godot_bin: str,
    user_dir: Optional[str],
    preferred_flag: str = "auto",
) -> Tuple[List[str], Optional[str]]:
    """Build Godot CLI args for user dir. Returns (args, flag_used)."""

    if not user_dir:
        return [], None

    user_dir = validate_user_dir_path(user_dir, source="--user-dir/GODOT_USERDIR")
    flag = detect_userdir_flag(godot_bin, preferred_flag=preferred_flag)
    if not flag:
        return [], None
    return [flag, user_dir], flag
