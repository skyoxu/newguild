#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Fill missing "Refs:" for every acceptance item in task views.

Derived from the sanguo repository workflow tooling and decoupled for newguild:
  - No Sanguo-specific paths or keywords.
  - Deterministic (no LLM).
  - Uses existing view.test_refs as the primary ref pool (keeps intent),
    falling back to default naming when missing.

Rules:
  - If an acceptance item already contains "Refs:", it is left unchanged unless --rewrite-existing.
  - Otherwise append: " Refs: <path>"
  - Refs are repo-relative paths to test files (.cs/.gd).
  - Task-level test_refs is updated to include referenced paths.
  - Invalid existing test_refs entries (non-test paths) are moved into description
    under "Evidence Artifacts (not test_refs)" and removed from test_refs.

This script does NOT create test files. It only prepares deterministic mapping targets.

Windows:
  py -3 scripts/python/llm_fill_acceptance_refs.py --task-id-start 27 --task-id-end 43 --rewrite-existing --rebuild-test-refs --write --write-logs
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import re
from pathlib import Path
from typing import Any


REFS_RE = re.compile(r"\bRefs\s*:\s*(.+)$", flags=re.IGNORECASE)


def repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


def load_text(path: Path) -> str:
    if not path.exists():
        return ""
    return path.read_text(encoding="utf-8")


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def save_json(path: Path, data: Any) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def is_abs_path(p: str) -> bool:
    s = (p or "").strip()
    if not s:
        return False
    if s.startswith(("/", "\\")):
        return True
    if len(s) >= 2 and s[1] == ":":
        return True
    return False


def is_allowed_test_ref(p: str) -> bool:
    s = (p or "").strip().replace("\\", "/")
    if is_abs_path(s):
        return False
    if not (s.endswith(".cs") or s.endswith(".gd")):
        return False
    allowed_prefixes = (
        "Game.Core.Tests/",
        "Tests.Godot/tests/",
        "Tests/",
    )
    return s.startswith(allowed_prefixes)


GD_KEYWORDS = (
    "gdunit",
    "headless",
    ".tscn",
    "scene",
    "signal",
    "tween",
    "animation",
    "control",
    "ui",
    "click",
    "mousefilter",
    "scroll",
)


def should_use_gd_ref(*texts: str) -> bool:
    blob = " ".join([str(t or "") for t in texts]).lower()
    return any(k in blob for k in GD_KEYWORDS)


def default_cs_ref(task_id: int) -> str:
    return f"Game.Core.Tests/Tasks/Task{task_id}AcceptanceTests.cs"


def default_gd_ref(task_id: int) -> str:
    return f"Tests.Godot/tests/Scenes/test_task{task_id}_acceptance.gd"


def append_description_artifacts(description: str, artifacts: list[str]) -> str:
    artifacts = [a.strip() for a in artifacts if str(a).strip()]
    if not artifacts:
        return description
    desc = str(description or "").rstrip()
    marker = "Evidence Artifacts (not test_refs)"
    if marker in desc:
        return desc
    lines = [desc, "", f"{marker}:"]
    for a in artifacts:
        lines.append(f"- {a}")
    return "\n".join([l for l in lines if l is not None]).rstrip()


def _pick_ref_from_existing_test_refs(*, task_id: int, test_refs: list[str], prefer_gd: bool, acceptance_text: str) -> str:
    norm = [str(x).strip().replace("\\", "/") for x in test_refs if str(x).strip()]
    cs = [r for r in norm if r.endswith(".cs") and is_allowed_test_ref(r)]
    gd = [r for r in norm if r.endswith(".gd") and is_allowed_test_ref(r)]

    lower = str(acceptance_text or "").lower()
    # Deterministic override: if acceptance explicitly talks about Game.Core, bind to a C# test when possible.
    if "game.core" in lower and cs:
        return cs[0]

    use_gd = prefer_gd or should_use_gd_ref(acceptance_text)
    if use_gd and gd:
        return gd[0]
    if (not use_gd) and cs:
        return cs[0]
    if gd:
        return gd[0]
    if cs:
        return cs[0]
    return default_gd_ref(task_id) if use_gd else default_cs_ref(task_id)


def fill_for_view(
    *,
    root: Path,
    view_path: Path,
    prd_text: str,
    rewrite_existing: bool,
    rebuild_test_refs: bool,
    task_id_start: int | None,
    task_id_end: int | None,
) -> dict[str, Any]:
    _ = prd_text  # reserved for future heuristics; keep signature stable for upstream parity

    view = load_json(view_path)
    if not isinstance(view, list):
        raise ValueError(f"Expected list in {view_path}")

    changed_tasks = 0
    changed_acceptance_items = 0
    moved_invalid_test_refs = 0
    skipped_by_range = 0

    for entry in view:
        if not isinstance(entry, dict):
            continue
        task_id = entry.get("taskmaster_id")
        if not isinstance(task_id, int):
            continue

        if task_id_start is not None and task_id < task_id_start:
            skipped_by_range += 1
            continue
        if task_id_end is not None and task_id > task_id_end:
            skipped_by_range += 1
            continue

        layer = str(entry.get("layer") or "").strip().lower()
        title = str(entry.get("title") or "")
        description = str(entry.get("description") or "")
        test_strategy = entry.get("test_strategy") or []
        test_strategy_blob = " ".join([str(x) for x in test_strategy]) if isinstance(test_strategy, list) else str(test_strategy)

        # Keep layer-based preference only. Item-level keywords decide .gd vs .cs for mixed tasks.
        prefer_gd = layer in {"ui", "scene", "scenes"}

        # Normalize and sanitize test_refs
        test_refs = entry.get("test_refs")
        if not isinstance(test_refs, list):
            test_refs = []

        norm_refs = [str(x).strip().replace("\\", "/") for x in test_refs if str(x).strip()]
        invalid_refs = [r for r in norm_refs if not is_allowed_test_ref(r)]
        kept_refs = [r for r in norm_refs if is_allowed_test_ref(r)]

        if invalid_refs:
            entry["description"] = append_description_artifacts(description, invalid_refs)
            moved_invalid_test_refs += len(invalid_refs)

        entry["test_refs"] = kept_refs

        acceptance = entry.get("acceptance") or []
        if not isinstance(acceptance, list):
            continue

        updated = False
        new_acceptance: list[str] = []
        used_refs: list[str] = []

        for raw in acceptance:
            s = str(raw or "").strip()
            if not s:
                new_acceptance.append(s)
                continue

            if REFS_RE.search(s):
                if not rewrite_existing:
                    new_acceptance.append(s)
                    m = REFS_RE.search(s)
                    if m:
                        blob = m.group(1).replace("`", "").replace(",", " ").replace(";", " ")
                        for rr in blob.split():
                            used_refs.append(rr.strip().replace("\\", "/"))
                    continue
                # Rewrite mode: strip the suffix and recompute.
                s = REFS_RE.sub("", s).rstrip()

            r = _pick_ref_from_existing_test_refs(task_id=task_id, test_refs=entry["test_refs"], prefer_gd=prefer_gd, acceptance_text=s)
            new_acceptance.append(f"{s} Refs: {r}")
            changed_acceptance_items += 1
            updated = True
            used_refs.append(r)

        # Rebuild / update task-level test_refs based on the refs actually used by acceptance items.
        if rebuild_test_refs:
            uniq: list[str] = []
            seen = set()
            for rr in used_refs:
                rr = str(rr).strip().replace("\\", "/")
                if not rr:
                    continue
                if rr in seen:
                    continue
                seen.add(rr)
                uniq.append(rr)
            entry["test_refs"] = [rr for rr in uniq if is_allowed_test_ref(rr)]
        else:
            for rr in used_refs:
                rr = str(rr).strip().replace("\\", "/")
                if not rr:
                    continue
                if rr not in entry["test_refs"] and is_allowed_test_ref(rr):
                    entry["test_refs"].append(rr)

        # Ensure at least one test_refs exists for the task.
        if not entry["test_refs"]:
            entry["test_refs"].append(default_gd_ref(task_id) if prefer_gd else default_cs_ref(task_id))

        if updated:
            entry["acceptance"] = new_acceptance
            changed_tasks += 1

    save_json(view_path, view)
    return {
        "file": str(view_path.relative_to(root)).replace("\\", "/"),
        "changed_tasks": changed_tasks,
        "changed_acceptance_items": changed_acceptance_items,
        "moved_invalid_test_refs": moved_invalid_test_refs,
        "skipped_by_range": skipped_by_range,
        "tasks_total": len(view),
    }


def main() -> int:
    ap = argparse.ArgumentParser(description="Fill acceptance Refs: in tasks_back/tasks_gameplay.")
    ap.add_argument(
        "--prd",
        default=".taskmaster/docs/prd.txt",
        help="PRD file path (utf-8). Optional input for future heuristics. Default: .taskmaster/docs/prd.txt",
    )
    ap.add_argument("--tasks-dir", default=".taskmaster/tasks", help="Taskmaster tasks dir. Default: .taskmaster/tasks")
    ap.add_argument("--task-id-start", type=int, default=None)
    ap.add_argument("--task-id-end", type=int, default=None)
    ap.add_argument("--rewrite-existing", action="store_true", help="Rewrite existing acceptance Refs: based on heuristics.")
    ap.add_argument("--rebuild-test-refs", action="store_true", help="Rebuild test_refs from acceptance-used Refs.")
    ap.add_argument("--write", action="store_true", help="Write changes in-place (otherwise dry-run backup only).")
    ap.add_argument("--write-logs", action="store_true", help="Write a summary JSON under logs/ci/<date>/fill-acceptance-refs/")
    args = ap.parse_args()

    root = repo_root()
    prd_path = root / args.prd
    prd_text = load_text(prd_path)

    tasks_dir = root / args.tasks_dir
    back_path = tasks_dir / "tasks_back.json"
    gameplay_path = tasks_dir / "tasks_gameplay.json"

    today = dt.date.today().strftime("%Y-%m-%d")
    out_dir = root / "logs" / "ci" / today / "fill-acceptance-refs"
    out_dir.mkdir(parents=True, exist_ok=True)

    # Always snapshot current state for forensics.
    run_id = dt.datetime.now().strftime("%H%M%S")
    save_json(out_dir / f"tasks_back.before.{run_id}.json", load_json(back_path))
    save_json(out_dir / f"tasks_gameplay.before.{run_id}.json", load_json(gameplay_path))

    if not args.write:
        print("FILL_ACCEPTANCE_REFS status=dry_run (backups written)")
        return 0

    back_summary = fill_for_view(
        root=root,
        view_path=back_path,
        prd_text=prd_text,
        rewrite_existing=bool(args.rewrite_existing),
        rebuild_test_refs=bool(args.rebuild_test_refs),
        task_id_start=args.task_id_start,
        task_id_end=args.task_id_end,
    )
    gameplay_summary = fill_for_view(
        root=root,
        view_path=gameplay_path,
        prd_text=prd_text,
        rewrite_existing=bool(args.rewrite_existing),
        rebuild_test_refs=bool(args.rebuild_test_refs),
        task_id_start=args.task_id_start,
        task_id_end=args.task_id_end,
    )

    summary = {
        "date": today,
        "task_range": {"start": args.task_id_start, "end": args.task_id_end},
        "prd_used": str((root / args.prd).as_posix()),
        "prd_found": prd_path.exists(),
        "views": [back_summary, gameplay_summary],
        "rewrite_existing": bool(args.rewrite_existing),
        "rebuild_test_refs": bool(args.rebuild_test_refs),
    }

    if args.write_logs:
        (out_dir / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
