#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Deterministic contractRefs coverage check for a single task.

Goal:
- Prevent "wiring leaks" where a task consumes/publishes an event type but the view task
  contractRefs does not list it (contractRefs is the view SSoT per repo rulebook).

Method (deterministic):
- Resolve the view task entry by taskmaster_id from tasks_back/tasks_gameplay.
- Collect candidate files from:
  - view.test_refs
  - master task testRefs
  - acceptance "Refs:" entries
  - any *.cs/*.gd files containing ACC:T<id>. anchors (bounded scan)
- Extract event types used in those files via:
  - string literals "core.*", "ui.menu.*", "screen.*"
  - references to <TypeName>.EventType resolved from Game.Core/Contracts/** constants
- Fail if any extracted event type is missing from view.contractRefs.

Outputs:
- logs/ci/<YYYY-MM-DD>/task-contractrefs-coverage/task-<id>.json (+ .txt)
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any


def _today() -> str:
    return dt.date.today().strftime("%Y-%m-%d")


def _repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


def _write_json(path: Path, obj: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")


def _write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text.rstrip() + "\n", encoding="utf-8", newline="\n")


def _load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def _normalize_str_list(value: Any) -> list[str]:
    if value is None:
        return []
    if isinstance(value, list):
        out: list[str] = []
        for item in value:
            s = str(item).strip().replace("\\", "/")
            if s:
                out.append(s)
        return out
    s = str(value).strip().replace("\\", "/")
    return [s] if s else []


def _split_refs_blob(blob: str) -> list[str]:
    s = str(blob or "").strip()
    s = s.replace("`", "")
    s = s.replace(",", " ")
    s = s.replace(";", " ")
    return [p.strip().replace("\\", "/") for p in s.split() if p.strip()]


REFS_RE = re.compile(r"\bRefs\s*:\s*(.+)$", flags=re.IGNORECASE)


def _extract_acceptance_ref_paths(acceptance: Any) -> list[str]:
    if not isinstance(acceptance, list):
        return []
    out: list[str] = []
    for raw in acceptance:
        text = str(raw or "").strip()
        m = REFS_RE.search(text)
        if not m:
            continue
        out.extend(_split_refs_blob(m.group(1)))
    return out


@dataclass(frozen=True)
class Occurrence:
    file: str
    line: int
    kind: str  # literal|eventtype_ref
    token: str
    event_type: str


EVENT_TYPE_LITERAL_RE = re.compile(r'["\']((?:core|ui\.menu|screen)\.[a-z0-9_.]+)["\']')
EVENTTYPE_REF_RE = re.compile(r"\b([A-Za-z0-9_.]+)\.EventType\b")
ACC_ANCHOR_RE_TEMPLATE = r"\bACC:T{task_id}\.\d+\b"

# Deterministic ignore list for placeholder/test-only event types used in unit tests.
# These should NOT be required by contractRefs (contractRefs is for domain event contracts).
IGNORE_EVENTTYPE_PREFIXES = (
    "core.test.",
    "core.tests.",
    "core.example.",
    "core.sample.",
)


def _normalize_ref_path(rel: str) -> str:
    s = str(rel or '').strip().replace('\\', '/')
    if s.startswith('./'):
        s = s[2:]
    return s


def _safe_task_ref_path(*, root: Path, rel: str) -> tuple[Path | None, str | None]:
    """Validate and resolve a repo-relative path from task refs.

    Defensive-only: rejects absolute/UNC paths and path traversal (..), and
    ensures the resolved path stays within repo root.
    """
    norm = _normalize_ref_path(rel)
    if not norm:
        return None, 'empty path'

    if norm.startswith('/') or norm.startswith('\\') or re.match(r'^[A-Za-z]:', norm):
        return None, f'illegal absolute path: {rel!r}'

    parts = [p for p in norm.split('/') if p not in ('', '.')]
    if any(p == '..' for p in parts):
        return None, f'illegal path traversal: {rel!r}'

    root_resolved = root.resolve()
    candidate = (root / norm).resolve()
    try:
        candidate.relative_to(root_resolved)
    except ValueError:
        return None, f'path escapes repo root: {rel!r}'

    return candidate, None


def _is_ignored_event_type(event_type: str) -> bool:
    v = (event_type or "").strip()
    return any(v.startswith(p) for p in IGNORE_EVENTTYPE_PREFIXES)


def _collect_contract_eventtype_map(contracts_root: Path) -> dict[str, str]:
    # Map: TypeName -> EventType string
    # Deterministic and intentionally simple (no full C# parsing).
    type_re = re.compile(r"\b(?:sealed\s+)?(?:partial\s+)?(?:record|class)\s+([A-Za-z0-9_]+)\b")
    etype_re = re.compile(r'\bpublic\s+const\s+string\s+EventType\s*=\s*"([^"]+)"')

    out: dict[str, str] = {}
    for p in contracts_root.rglob("*.cs"):
        try:
            lines = p.read_text(encoding="utf-8", errors="ignore").splitlines()
        except OSError:
            continue

        current_type: str | None = None
        for line in lines:
            m_t = type_re.search(line)
            if m_t:
                current_type = m_t.group(1)
            m_e = etype_re.search(line)
            if m_e and current_type:
                value = m_e.group(1).strip()
                if value:
                    out[current_type] = value
    return out


def _extract_event_types_from_text(
    *,
    text: str,
    file_rel: str,
    eventtype_map: dict[str, str],
) -> list[Occurrence]:
    occ: list[Occurrence] = []

    lines = text.splitlines()
    for idx, line in enumerate(lines, start=1):
        for m in EVENT_TYPE_LITERAL_RE.finditer(line):
            val = m.group(1)
            if _is_ignored_event_type(val):
                continue
            occ.append(Occurrence(file=file_rel, line=idx, kind="literal", token=m.group(0), event_type=val))

        for m in EVENTTYPE_REF_RE.finditer(line):
            raw = m.group(1)
            type_name = raw.split(".")[-1]
            resolved = eventtype_map.get(type_name)
            if resolved and (resolved.startswith("core.") or resolved.startswith("ui.") or resolved.startswith("screen.")):
                if _is_ignored_event_type(resolved):
                    continue
                occ.append(Occurrence(file=file_rel, line=idx, kind="eventtype_ref", token=m.group(0), event_type=resolved))

    return occ


def _find_anchor_files(*, root: Path, task_id: str) -> list[Path]:
    needle_re = re.compile(ACC_ANCHOR_RE_TEMPLATE.format(task_id=re.escape(task_id)))
    scan_roots = [
        root / "Game.Core",
        root / "Game.Godot",
        root / "Game.Core.Tests",
        root / "Tests.Godot" / "tests",
    ]
    exts = {".cs", ".gd"}

    out: list[Path] = []
    for r in scan_roots:
        if not r.exists():
            continue
        for p in r.rglob("*"):
            if not p.is_file() or p.suffix.lower() not in exts:
                continue
            try:
                text = p.read_text(encoding="utf-8", errors="ignore")
            except OSError:
                continue
            if needle_re.search(text):
                out.append(p)
    return out


def _load_master_task(task_id: str, root: Path) -> dict[str, Any]:
    data = _load_json(root / ".taskmaster" / "tasks" / "tasks.json")
    tasks = data.get("master", {}).get("tasks", [])
    for t in tasks:
        if str(t.get("id")) == str(task_id):
            return t
    raise KeyError(f"Task {task_id} not found in .taskmaster/tasks/tasks.json")


def _load_view_tasks(root: Path, which: str) -> list[dict[str, Any]]:
    path = root / ".taskmaster" / "tasks" / f"tasks_{which}.json"
    data = _load_json(path)
    if not isinstance(data, list):
        raise ValueError(f"Unsupported schema: {path} (expected list root)")
    return data


def _find_view_entries(*, root: Path, task_id: str, which: str) -> list[dict[str, Any]]:
    out: list[dict[str, Any]] = []
    for t in _load_view_tasks(root, which):
        if str(t.get("taskmaster_id")) == str(task_id):
            out.append(t)
    return out


def _run_for_view(*, root: Path, task_id: str, which: str, strict: bool) -> dict[str, Any]:
    entries = _find_view_entries(root=root, task_id=task_id, which=which)
    if not entries:
        return {"view": which, "status": "skipped", "reason": "no_entry"}
    if len(entries) > 1:
        return {"view": which, "status": "fail", "reason": "multiple_entries", "count": len(entries)}

    entry = entries[0]
    contract_refs = sorted(set(_normalize_str_list(entry.get("contractRefs"))))

    master = _load_master_task(task_id, root)
    acceptance_refs = _extract_acceptance_ref_paths(master.get("acceptance"))
    master_test_refs = _normalize_str_list(master.get("testRefs"))
    view_test_refs = _normalize_str_list(entry.get("test_refs"))
    anchor_files = _find_anchor_files(root=root, task_id=str(task_id))

    candidate_paths: set[Path] = set()
    invalid_ref_paths: list[str] = []
    for rel in acceptance_refs + master_test_refs + view_test_refs:
        p, err = _safe_task_ref_path(root=root, rel=rel)
        if err:
            invalid_ref_paths.append(err)
            continue
        candidate_paths.add(p)
    for p in anchor_files:
        candidate_paths.add(p)

    existing_files: list[Path] = []
    missing_files: list[str] = []
    for p in sorted(candidate_paths, key=lambda x: str(x)):
        if p.exists() and p.is_file():
            existing_files.append(p)
        else:
            missing_files.append(str(p.relative_to(root)).replace("\\", "/"))

    eventtype_map = _collect_contract_eventtype_map(root / "Game.Core" / "Contracts")

    occ: list[Occurrence] = []
    for p in existing_files:
        try:
            text = p.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        rel = str(p.relative_to(root)).replace("\\", "/")
        occ.extend(_extract_event_types_from_text(text=text, file_rel=rel, eventtype_map=eventtype_map))

    used_event_types = sorted(set(o.event_type for o in occ))
    missing_in_contractrefs = sorted([t for t in used_event_types if t not in contract_refs])
    unused_contractrefs = sorted([t for t in contract_refs if t not in used_event_types])

    ok = (not missing_in_contractrefs) and (not invalid_ref_paths)
    status = "ok" if ok else ("fail" if strict else "warn")

    return {
        "view": which,
        "status": status,
        "taskmaster_id": str(entry.get("taskmaster_id") or ""),
        "id": entry.get("id"),
        "layer": entry.get("layer"),
        "contractRefs_count": len(contract_refs),
        "used_event_types_count": len(used_event_types),
        "missing_in_contractRefs": missing_in_contractrefs,
        "unused_contractRefs": unused_contractrefs,
        "missing_files": missing_files,
        "invalid_ref_paths": invalid_ref_paths,
        "occurrences_sample": [
            {
                "file": o.file,
                "line": o.line,
                "kind": o.kind,
                "token": o.token,
                "event_type": o.event_type,
            }
            for o in occ[:50]
        ],
    }


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--task-id", required=True)
    ap.add_argument("--view", choices=["auto", "back", "gameplay"], default="auto")
    ap.add_argument("--strict", action="store_true", help="Fail on missing coverage (default).")
    ap.add_argument("--no-strict", dest="strict", action="store_false")
    ap.set_defaults(strict=True)
    args = ap.parse_args()

    root = _repo_root()
    task_id = str(args.task_id).strip()
    out_dir = root / "logs" / "ci" / _today() / "task-contractrefs-coverage"
    out_dir.mkdir(parents=True, exist_ok=True)

    views = ["back", "gameplay"] if args.view == "auto" else [args.view]
    results = [_run_for_view(root=root, task_id=task_id, which=v, strict=bool(args.strict)) for v in views]

    any_fail = any(r.get("status") == "fail" for r in results)
    ok = not any_fail

    report = {
        "date": _today(),
        "task_id": task_id,
        "status": "ok" if ok else "fail",
        "results": results,
    }

    json_path = out_dir / f"task-{task_id}.json"
    _write_json(json_path, report)

    lines: list[str] = [f"TASK_CONTRACTREFS_COVERAGE status={'ok' if ok else 'fail'} task_id={task_id} out={out_dir}"]
    for r in results:
        lines.append(f"- view={r.get('view')} status={r.get('status')}")
        missing = r.get("missing_in_contractRefs") or []
        if missing:
            lines.append(f"  missing_in_contractRefs={missing}")
    _write_text(out_dir / f"task-{task_id}.txt", "\n".join(lines))

    print(lines[0])
    return 0 if ok else 1


if __name__ == "__main__":  # pragma: no cover
    raise SystemExit(main())
