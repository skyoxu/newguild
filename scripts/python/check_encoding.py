#!/usr/bin/env python3
"""
Check encodings for repository text files and verify they are UTF-8 decodable.
Also flag common mojibake patterns. Results are written under logs/ci/<YYYY-MM-DD>/encoding/.

Usage:
  py -3 scripts/python/check_encoding.py --root docs
  py -3 scripts/python/check_encoding.py --since-today
  py -3 scripts/python/check_encoding.py --since "2025-11-13 00:00:00"
  py -3 scripts/python/check_encoding.py --files path1 path2 ...
"""

import argparse
import datetime as dt
import io
import json
import os
import subprocess
import sys
from typing import List

TEXT_EXT = {
    ".md",
    ".txt",
    ".json",
    ".yml",
    ".yaml",
    ".xml",
    ".cs",
    ".csproj",
    ".sln",
    ".gd",
    ".tscn",
    ".tres",
    ".gitattributes",
    ".gitignore",
    ".ps1",
    ".py",
    ".ini",
    ".cfg",
    ".toml",
}

# Explicit binary extensions to skip from UTF-8 validation
BINARY_EXT = {
    ".png",
    ".jpg",
    ".jpeg",
    ".gif",
    ".bmp",
    ".ico",
    ".webp",
    ".ogg",
    ".wav",
    ".mp3",
    ".mp4",
    ".avi",
    ".mov",
    ".zip",
    ".7z",
    ".rar",
    ".gz",
    ".tar",
    ".tgz",
    ".dll",
    ".exe",
    ".pdb",
    ".pck",
    ".import",
    ".ttf",
    ".otf",
    ".db",
    ".sqlite",
    ".sav",
    ".bak",
}

# Exclude vendor/test asset folders and known binaries
EXCLUDE_PATTERNS = [
    "Tests.Godot/addons/gdUnit4/src/core/assets/",
    "Tests.Godot/addons/gdUnit4/src/update/assets/",
    "Tests.Godot/addons/gdUnit4/src/reporters/html/template/css/",
    "Tests.Godot/addons/gdUnit4/src/ui/settings/",
    "gitlog/export-logs.zip",
]

# Common mojibake fragments that usually indicate UTF-8 bytes were mis-decoded/saved.
# This is heuristic-only and intended for triage, not as a hard gate.
MOJIBAKE_TOKENS = [
    "CafÃ",  # "Café" -> "CafÃ©"
    "Ã",
    "Â",
    "â€™",
    "â€œ",
    "â€",
    "ðŸ",  # emoji bytes interpreted as text
    "æ–",  # Chinese UTF-8 bytes interpreted as CP1252 (very common)
    "å¤",
    "å­",
    "è¿",
]


def run_cmd(args: List[str]) -> str:
    p = subprocess.Popen(
        args,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        encoding="utf-8",
        errors="ignore",
    )
    out, _ = p.communicate()
    return out


def git_changed_since(since: str) -> List[str]:
    try:
        out = run_cmd(["git", "log", f"--since={since}", "--name-only", "--pretty=format:"])
    except Exception:
        return []
    files = [ln.strip() for ln in out.splitlines() if ln.strip() and not ln.startswith(" ")]
    return sorted(set(files))


def git_changed_today() -> List[str]:
    today = dt.date.today().strftime("%Y-%m-%d")
    return git_changed_since(today + " 00:00:00")


def is_text_file(path: str) -> bool:
    _, ext = os.path.splitext(path)
    ext = ext.lower()
    norm = path.replace("\\", "/")
    if any(p in norm for p in EXCLUDE_PATTERNS):
        return False
    if ext in BINARY_EXT:
        return False
    if ext in TEXT_EXT:
        return True

    # Heuristic: treat small unknown files as text; larger files likely binary.
    try:
        sz = os.path.getsize(path)
        return sz < 128 * 1024
    except Exception:
        return False


def iter_root_files(root: str) -> List[str]:
    root = os.path.normpath(root)
    out: List[str] = []
    for dirpath, dirnames, filenames in os.walk(root):
        base = os.path.basename(dirpath).lower()
        if base in {".git", ".godot", "logs", "node_modules", "build", "reports", "testresults"}:
            dirnames[:] = []
            continue
        for fn in filenames:
            out.append(os.path.join(dirpath, fn))
    return out


def detect_mojibake(text: str) -> List[str]:
    hits: List[str] = []
    for tok in MOJIBAKE_TOKENS:
        if tok in text:
            hits.append(tok)
        if len(hits) >= 10:
            break
    return hits


def check_utf8(path: str) -> dict:
    result = {
        "path": path,
        "utf8_ok": False,
        "has_bom": False,
        "mojibake_hits": [],
        "error": None,
    }
    try:
        raw = io.open(path, "rb").read()
        result["has_bom"] = raw.startswith(b"\xef\xbb\xbf")
        text = raw.decode("utf-8", errors="strict")
        result["utf8_ok"] = True
        result["mojibake_hits"] = detect_mojibake(text)
    except UnicodeDecodeError as e:
        result["error"] = f"UnicodeDecodeError: {e}"
    except Exception as e:
        result["error"] = str(e)
    return result


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=None, help="Scan all text-like files under this root directory")
    ap.add_argument("--since-today", action="store_true")
    ap.add_argument("--since", default=None)
    ap.add_argument("--files", nargs="*")
    args = ap.parse_args()

    if args.files:
        files = args.files
    elif args.root:
        files = iter_root_files(args.root)
    elif args.since:
        files = git_changed_since(args.since)
    else:
        files = git_changed_today()

    files = [f for f in files if os.path.isfile(f) and is_text_file(f)]

    date = dt.date.today().strftime("%Y-%m-%d")
    out_dir = os.path.join("logs", "ci", date, "encoding")
    os.makedirs(out_dir, exist_ok=True)

    results = []
    bad = []
    skipped = 0
    for fpath in files:
        if not is_text_file(fpath):
            skipped += 1
            continue
        r = check_utf8(fpath)
        results.append(r)
        if not r["utf8_ok"]:
            bad.append(r)

    summary = {
        "generated": dt.datetime.now().isoformat(),
        "mode": "root" if args.root else ("files" if args.files else "git"),
        "root": args.root,
        "scanned": len(results),
        "bad": len(bad),
        "bad_paths": [b["path"] for b in bad],
        "mojibake_paths": [r["path"] for r in results if r.get("mojibake_hits")],
        "skipped": skipped,
    }

    with io.open(os.path.join(out_dir, "session-details.json"), "w", encoding="utf-8") as f:
        json.dump(results, f, ensure_ascii=False, indent=2)
    with io.open(os.path.join(out_dir, "session-summary.json"), "w", encoding="utf-8") as f:
        json.dump(summary, f, ensure_ascii=False, indent=2)

    # Optional: quick human summary
    with io.open(os.path.join(out_dir, "session-summary.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write(f"ENCODING_CHECK scanned={summary['scanned']} bad={summary['bad']}\n")
        if summary["bad_paths"]:
            f.write("BAD_FILES:\n")
            for p in summary["bad_paths"]:
                f.write(f"  {p}\n")
        if summary["mojibake_paths"]:
            f.write("MOJIBAKE_SUSPECTS:\n")
            for p in summary["mojibake_paths"]:
                f.write(f"  {p}\n")

    print(f"ENCODING_CHECK scanned={summary['scanned']} bad={summary['bad']} out={out_dir}")
    return 0 if summary["bad"] == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
