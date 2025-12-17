#!/usr/bin/env python3
"""
Archive and prune Godot user:// logs on Windows.

Why:
- Godot/GdUnit4 can write very large files into:
  %APPDATA%\\Godot\\app_userdata\\<ProjectName>\\logs
- This script copies "key" logs into the repo's logs/ folder and then
  applies retention / size limits to prevent disk exhaustion.

Usage (Windows):
  py -3 scripts/python/godot_userlog_manager.py --project Tests.Godot

Optional:
  py -3 scripts/python/godot_userlog_manager.py --project Tests.Godot --dry-run
  py -3 scripts/python/godot_userlog_manager.py --project . --retention-days 7 --max-file-mb 256 --tail-mb 4
  py -3 scripts/python/godot_userlog_manager.py --project Tests.Godot --source-logs-dir "logs/_godot_userdir/Tests.Godot/logs"
  py -3 scripts/python/godot_userlog_manager.py --project Tests.Godot --purge --confirm PURGE
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import re
import shutil
from dataclasses import dataclass
from pathlib import Path
from typing import Any


@dataclass(frozen=True)
class UserLogPolicy:
    retention_days: int
    max_file_bytes: int
    tail_bytes: int
    max_full_copy_bytes: int


def _read_project_name(project_dir: Path) -> str | None:
    project_godot = project_dir / "project.godot"
    if not project_godot.is_file():
        return None

    try:
        text = project_godot.read_text(encoding="utf-8", errors="ignore")
    except Exception:
        return None

    # Example: config/name="Godot Tests"
    m = re.search(r'^\s*config/name\s*=\s*"([^"]+)"\s*$', text, flags=re.M)
    if not m:
        return None
    return m.group(1)


def _get_windows_user_logs_dir(project_name: str) -> Path | None:
    appdata = os.environ.get("APPDATA")
    if not appdata:
        return None
    return Path(appdata) / "Godot" / "app_userdata" / project_name / "logs"


def _read_tail_bytes(path: Path, max_bytes: int, *, align_to_line_start: bool) -> bytes:
    if max_bytes <= 0:
        return b""

    with path.open("rb") as f:
        f.seek(0, os.SEEK_END)
        size = f.tell()
        start = max(0, size - max_bytes)
        f.seek(start, os.SEEK_SET)
        data = f.read()

    if align_to_line_start and start > 0:
        nl = data.find(b"\n")
        if nl >= 0 and nl + 1 < len(data):
            data = data[nl + 1 :]
    return data


def _write_bytes(dest: Path, data: bytes) -> None:
    dest.parent.mkdir(parents=True, exist_ok=True)
    with dest.open("wb") as f:
        f.write(data)


def _copy_full(src: Path, dest: Path) -> None:
    dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dest)


def _is_jsonl(path: Path) -> bool:
    return path.suffix.lower() == ".jsonl"


def _is_json(path: Path) -> bool:
    return path.suffix.lower() == ".json"


def _is_text_log(path: Path) -> bool:
    return path.suffix.lower() in {".log", ".txt"}


def archive_and_prune_user_logs(
    *,
    project_dir: Path,
    dest_dir: Path,
    policy: UserLogPolicy,
    dry_run: bool,
    source_logs_dir: Path | None = None,
) -> dict[str, Any]:
    project_name = _read_project_name(project_dir)
    if source_logs_dir is not None:
        logs_dir = source_logs_dir
    else:
        logs_dir = _get_windows_user_logs_dir(project_name) if project_name else None

    summary: dict[str, Any] = {
        "ts": dt.datetime.now().isoformat(timespec="seconds"),
        "project_dir": str(project_dir),
        "project_name": project_name,
        "source_logs_dir": str(logs_dir) if logs_dir else None,
        "dest_dir": str(dest_dir),
        "policy": {
            "retention_days": policy.retention_days,
            "max_file_bytes": policy.max_file_bytes,
            "tail_bytes": policy.tail_bytes,
            "max_full_copy_bytes": policy.max_full_copy_bytes,
        },
        "archived": [],
        "pruned": [],
        "errors": [],
    }

    if logs_dir is None:
        summary["errors"].append({"error": "APPDATA or project_name missing; cannot locate user logs dir"})
        return summary

    if not logs_dir.exists():
        summary["note"] = "source logs dir not found; nothing to do"
        return summary

    dest_dir.mkdir(parents=True, exist_ok=True)
    cutoff = dt.datetime.now() - dt.timedelta(days=policy.retention_days)

    # Archive: copy JSON/JSONL fully (bounded), copy *.log tail.
    for src in sorted(p for p in logs_dir.rglob("*") if p.is_file()):
        try:
            rel = src.relative_to(logs_dir)
        except Exception:
            rel = Path(src.name)

        try:
            st = src.stat()
        except Exception as e:
            summary["errors"].append({"file": str(src), "op": "stat", "error": str(e)})
            continue

        dst_base = dest_dir / rel
        if _is_text_log(src):
            tail = _read_tail_bytes(src, policy.tail_bytes, align_to_line_start=False)
            dst = dst_base.with_name(dst_base.name + ".tail.txt")
            if not dry_run:
                _write_bytes(dst, tail)
            summary["archived"].append(
                {
                    "src": str(src),
                    "dst": str(dst),
                    "mode": "tail",
                    "src_bytes": st.st_size,
                    "dst_bytes": len(tail),
                }
            )
            continue

        if (_is_json(src) or _is_jsonl(src)) and st.st_size <= policy.max_full_copy_bytes:
            if not dry_run:
                _copy_full(src, dst_base)
            summary["archived"].append(
                {
                    "src": str(src),
                    "dst": str(dst_base),
                    "mode": "full",
                    "src_bytes": st.st_size,
                    "dst_bytes": st.st_size if not dry_run else 0,
                }
            )
            continue

        # If a JSONL file is too large to copy in full, archive a tail slice (aligned to line start).
        if _is_jsonl(src) and st.st_size > policy.max_full_copy_bytes:
            tail = _read_tail_bytes(src, policy.tail_bytes, align_to_line_start=True)
            dst = dst_base.with_name(dst_base.name + ".tail.jsonl")
            if not dry_run:
                _write_bytes(dst, tail)
            summary["archived"].append(
                {
                    "src": str(src),
                    "dst": str(dst),
                    "mode": "tail-jsonl",
                    "src_bytes": st.st_size,
                    "dst_bytes": len(tail),
                }
            )

    # Prune: enforce retention and size limits in source logs dir.
    for src in sorted(p for p in logs_dir.rglob("*") if p.is_file()):
        try:
            st = src.stat()
        except Exception:
            continue

        mtime = dt.datetime.fromtimestamp(st.st_mtime)
        should_prune = mtime < cutoff or st.st_size > policy.max_file_bytes
        if not should_prune:
            continue

        action = "delete"
        if _is_jsonl(src) and st.st_size > policy.max_file_bytes:
            action = "truncate"
        if _is_text_log(src) and st.st_size > policy.max_file_bytes:
            action = "truncate"

        if not dry_run:
            try:
                if action == "delete":
                    src.unlink(missing_ok=True)
                else:
                    tail = _read_tail_bytes(src, policy.tail_bytes, align_to_line_start=_is_jsonl(src))
                    _write_bytes(src, tail)
            except Exception as e:
                summary["errors"].append({"file": str(src), "op": action, "error": str(e)})
                continue

        summary["pruned"].append(
            {
                "file": str(src),
                "action": action,
                "mtime": mtime.isoformat(timespec="seconds"),
                "bytes_before": st.st_size,
            }
        )

    return summary


def _default_dest_dir() -> Path:
    date = dt.date.today().strftime("%Y-%m-%d")
    return Path("logs") / "ci" / date / "godot-userlogs"


def main() -> int:
    ap = argparse.ArgumentParser(description="Archive and prune Godot user:// logs (Windows)")
    ap.add_argument("--project", default="Tests.Godot", help="Godot project directory (contains project.godot)")
    ap.add_argument("--dest", default=str(_default_dest_dir()), help="Destination under repo logs/")
    ap.add_argument(
        "--source-logs-dir",
        default=None,
        help='Override source logs directory (e.g. "logs/_godot_userdir/Tests.Godot/logs")',
    )
    ap.add_argument("--retention-days", type=int, default=int(os.environ.get("GODOT_USERLOG_RETENTION_DAYS", "7")))
    ap.add_argument("--max-file-mb", type=int, default=int(os.environ.get("GODOT_USERLOG_MAX_FILE_MB", "256")))
    ap.add_argument("--tail-mb", type=int, default=int(os.environ.get("GODOT_USERLOG_TAIL_MB", "4")))
    ap.add_argument("--max-full-copy-mb", type=int, default=int(os.environ.get("GODOT_USERLOG_MAX_FULL_COPY_MB", "16")))
    ap.add_argument("--dry-run", action="store_true", help="Do not write/delete; only report actions")
    ap.add_argument(
        "--purge",
        action="store_true",
        help="Delete the entire user logs directory after archiving (requires --confirm PURGE)",
    )
    ap.add_argument(
        "--confirm",
        default="",
        help="Safety confirmation token for destructive actions (use: --confirm PURGE)",
    )
    args = ap.parse_args()

    project_dir = Path(args.project).resolve()
    policy = UserLogPolicy(
        retention_days=max(0, args.retention_days),
        max_file_bytes=max(0, args.max_file_mb) * 1024 * 1024,
        tail_bytes=max(0, args.tail_mb) * 1024 * 1024,
        max_full_copy_bytes=max(0, args.max_full_copy_mb) * 1024 * 1024,
    )

    dest_dir = Path(args.dest)
    source_logs_dir = Path(args.source_logs_dir).resolve() if args.source_logs_dir else None
    summary = archive_and_prune_user_logs(
        project_dir=project_dir,
        dest_dir=dest_dir,
        policy=policy,
        dry_run=args.dry_run,
        source_logs_dir=source_logs_dir,
    )

    # Optional purge: remove the entire source logs directory after archiving.
    if args.purge:
        summary["purge"] = {"requested": True, "executed": False}
        if args.dry_run:
            summary["purge"]["note"] = "dry-run: purge skipped"
        elif args.confirm != "PURGE":
            summary["purge"]["note"] = "confirmation missing: pass --confirm PURGE to execute purge"
        else:
            source_dir = summary.get("source_logs_dir")
            try:
                if source_dir:
                    shutil.rmtree(source_dir, ignore_errors=False)
                    summary["purge"]["executed"] = True
                    summary["purge"]["deleted_dir"] = source_dir
            except Exception as e:
                summary["errors"].append({"file": str(source_dir), "op": "purge", "error": str(e)})

    dest_dir.mkdir(parents=True, exist_ok=True)
    out_path = dest_dir / "userlogs-summary.json"
    out_path.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")

    archived = len(summary.get("archived", []))
    pruned = len(summary.get("pruned", []))
    errors = len(summary.get("errors", []))
    print(f"USERLOGS archived={archived} pruned={pruned} errors={errors} dest={dest_dir}")
    return 0 if errors == 0 else 2


if __name__ == "__main__":
    raise SystemExit(main())
