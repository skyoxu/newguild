from __future__ import annotations

import hashlib
import json
import os
from datetime import datetime, timezone
from pathlib import Path


def read_text_utf8(path: Path) -> str:
    # Use utf-8-sig to tolerate BOM while still treating content as UTF-8.
    return path.read_text(encoding="utf-8-sig")


def safe_read_text(path: Path) -> tuple[str, str | None]:
    try:
        return read_text_utf8(path), None
    except UnicodeDecodeError as exc:
        # Keep going with replacement to surface drift without crashing.
        raw = path.read_bytes()
        return raw.decode("utf-8", errors="replace"), f"UnicodeDecodeError: {exc}"


def sha256_utf8(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def normalize(text: str) -> str:
    # Conservative normalization for "semantic equality":
    # - normalize newlines
    # - strip trailing whitespace per line
    text = text.replace("\r\n", "\n").replace("\r", "\n")
    return "\n".join(line.rstrip() for line in text.split("\n"))


def compare(a: str, b: str) -> dict:
    na = normalize(a)
    nb = normalize(b)
    return {
        "exact_equal": a == b,
        "normalized_equal": na == nb,
        "a_chars": len(a),
        "b_chars": len(b),
        "a_sha256_utf8": sha256_utf8(a),
        "b_sha256_utf8": sha256_utf8(b),
        "a_norm_sha256_utf8": sha256_utf8(na),
        "b_norm_sha256_utf8": sha256_utf8(nb),
    }


def main() -> int:
    repo_root = Path(__file__).resolve().parents[2]
    docs_prd = repo_root / "docs" / "prd.txt"
    tm_docs = repo_root / ".taskmaster" / "docs"

    if not docs_prd.exists():
        raise SystemExit(f"missing: {docs_prd}")
    if not tm_docs.exists():
        raise SystemExit(f"missing: {tm_docs}")

    include_ext = {".txt", ".md", ".json", ".yml", ".yaml", ".xml", ".index"}
    include_names = {"prd.txt", "prd.md"}

    tm_files: list[Path] = []
    for path in tm_docs.rglob("*"):
        if path.is_dir():
            continue
        if path.name.lower() in include_names or path.suffix.lower() in include_ext:
            tm_files.append(path)

    tm_files = sorted(tm_files, key=lambda p: str(p).lower())

    docs_text, docs_warn = safe_read_text(docs_prd)

    tm_prd_path = tm_docs / "prd.txt"
    tm_prd_text = None
    tm_prd_warn = None
    if tm_prd_path.exists():
        tm_prd_text, tm_prd_warn = safe_read_text(tm_prd_path)

    agg_parts: list[str] = []
    agg_files_meta: list[dict] = []
    for file_path in tm_files:
        content, warn = safe_read_text(file_path)
        rel = file_path.relative_to(repo_root).as_posix()
        agg_parts.append(f"\n\n===== FILE: {rel} =====\n")
        agg_parts.append(content)
        agg_files_meta.append(
            {
                "path": rel,
                "chars": len(content),
                "sha256_utf8": sha256_utf8(content),
                "read_warning": warn,
            }
        )
    agg_text = "".join(agg_parts)

    report = {
        "ts": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "docs_prd": {
            "path": docs_prd.as_posix(),
            "chars": len(docs_text),
            "sha256_utf8": sha256_utf8(docs_text),
            "read_warning": docs_warn,
        },
        "taskmaster_docs": {
            "root": tm_docs.as_posix(),
            "file_count": len(tm_files),
            "files": agg_files_meta,
        },
        "taskmaster_prd_txt": {
            "path": tm_prd_path.as_posix(),
            "exists": tm_prd_path.exists(),
            "chars": None if tm_prd_text is None else len(tm_prd_text),
            "sha256_utf8": None if tm_prd_text is None else sha256_utf8(tm_prd_text),
            "read_warning": tm_prd_warn,
        },
        "taskmaster_docs_aggregate": {
            "chars": len(agg_text),
            "sha256_utf8": sha256_utf8(agg_text),
        },
        "comparisons": {
            "docs_vs_taskmaster_prd_txt": None
            if tm_prd_text is None
            else compare(docs_text, tm_prd_text),
            "docs_vs_taskmaster_aggregate": compare(docs_text, agg_text),
        },
    }

    ci_date = os.environ.get("CI_DATE") or datetime.now().strftime("%Y-%m-%d")
    out_dir = repo_root / "logs" / "ci" / ci_date
    out_dir.mkdir(parents=True, exist_ok=True)
    out_path = out_dir / "prd-equivalence.json"
    out_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

    print(f"docs/prd.txt chars: {len(docs_text)}")
    print(f".taskmaster/docs included files: {len(tm_files)}")
    print(f"report: {out_path.as_posix()}")

    c1 = report["comparisons"]["docs_vs_taskmaster_prd_txt"]
    if c1 is None:
        print("compare docs vs .taskmaster/docs/prd.txt: SKIP (missing)")
    else:
        status = "EXACT" if c1["exact_equal"] else ("NORMALIZED" if c1["normalized_equal"] else "DIFF")
        print(f"compare docs vs .taskmaster/docs/prd.txt: {status}")

    c2 = report["comparisons"]["docs_vs_taskmaster_aggregate"]
    status2 = "EXACT" if c2["exact_equal"] else ("NORMALIZED" if c2["normalized_equal"] else "DIFF")
    print(f"compare docs vs .taskmaster/docs/* aggregate: {status2}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
