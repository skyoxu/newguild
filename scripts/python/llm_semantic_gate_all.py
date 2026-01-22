#!/usr/bin/env python3
# -*- coding: utf-8 -*-

from __future__ import annotations

import argparse
import datetime as _dt
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable


RE_TASK_DETAILS_ADR = re.compile(r"^\s*ADR\s+Refs\s*:\s*(?P<value>.+?)\s*$", re.IGNORECASE)
RE_TASK_DETAILS_CHAPTERS = re.compile(r"^\s*Chapters\s*:\s*(?P<value>.+?)\s*$", re.IGNORECASE)
RE_TASK_DETAILS_OVERLAYS = re.compile(r"^\s*Overlays\s*:\s*(?P<value>.+?)\s*$", re.IGNORECASE)

RE_BACKTICK_PATH = re.compile(r"`(?P<path>[^`]+?)`")
RE_EVENT_TYPE = re.compile(r"\b(?P<evt>(core|ui|screen|game)\.[a-z0-9][a-z0-9_.-]*)\b")


@dataclass(frozen=True)
class GateFinding:
    task_id: int
    view_id: str | None
    kind: str  # "error" | "warning"
    code: str
    message: str


def _today_ymd() -> str:
    return _dt.datetime.now().strftime("%Y-%m-%d")


def _read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def _ensure_list(value: Any) -> list[Any]:
    if value is None:
        return []
    if isinstance(value, list):
        return value
    return [value]


def _split_refs(value: str) -> list[str]:
    parts = [p.strip() for p in re.split(r"[;,]", value)]
    return [p for p in parts if p]


def _parse_task_details_refs(details: str) -> dict[str, list[str]]:
    adr: list[str] = []
    chapters: list[str] = []
    overlays: list[str] = []
    for line in (details or "").splitlines():
        m = RE_TASK_DETAILS_ADR.match(line)
        if m:
            adr = _split_refs(m.group("value"))
            continue
        m = RE_TASK_DETAILS_CHAPTERS.match(line)
        if m:
            chapters = _split_refs(m.group("value"))
            continue
        m = RE_TASK_DETAILS_OVERLAYS.match(line)
        if m:
            overlays = _split_refs(m.group("value"))
            continue
    return {"adrRefs": adr, "chapterRefs": chapters, "overlayRefs": overlays}


def _index_view_by_taskmaster_id(view_items: list[dict[str, Any]]) -> dict[int, dict[str, Any]]:
    out: dict[int, dict[str, Any]] = {}
    for item in view_items:
        tid = item.get("taskmaster_id")
        if isinstance(tid, int):
            out[tid] = item
    return out


def _collect_ref_paths_from_acceptance(acceptance_items: Iterable[str]) -> list[str]:
    paths: list[str] = []
    for s in acceptance_items:
        for m in RE_BACKTICK_PATH.finditer(s or ""):
            p = m.group("path").strip()
            if p:
                paths.append(p)
    return paths


def _collect_event_types_from_acceptance(acceptance_items: Iterable[str]) -> list[str]:
    events: list[str] = []
    for s in acceptance_items:
        for m in RE_EVENT_TYPE.finditer(s or ""):
            evt = m.group("evt").strip()
            if evt:
                events.append(evt.lower())
    # preserve order but de-dup
    seen: set[str] = set()
    dedup: list[str] = []
    for e in events:
        if e not in seen:
            seen.add(e)
            dedup.append(e)
    return dedup


def _file_exists(repo_root: Path, rel_or_abs: str) -> bool:
    p = Path(rel_or_abs)
    if p.is_absolute():
        return p.exists()
    return (repo_root / rel_or_abs).exists()


def _as_str_list(value: Any) -> list[str]:
    items = _ensure_list(value)
    out: list[str] = []
    for x in items:
        if x is None:
            continue
        out.append(str(x))
    return out


def run_gate(
    repo_root: Path,
    task_id_start: int,
    task_id_end: int,
    tasks_path: Path,
    gameplay_view_path: Path,
    back_view_path: Path,
) -> dict[str, Any]:
    tasks_json = _read_json(tasks_path)
    tasks: list[dict[str, Any]] = tasks_json["master"]["tasks"]
    tasks_by_id: dict[int, dict[str, Any]] = {}
    for t in tasks:
        try:
            tasks_by_id[int(t["id"])] = t
        except Exception:
            continue

    gameplay_items = _read_json(gameplay_view_path)
    back_items = _read_json(back_view_path)
    if not isinstance(gameplay_items, list) or not isinstance(back_items, list):
        raise SystemExit("Expected view json to be a list of tasks.")

    gameplay_by_tid = _index_view_by_taskmaster_id(gameplay_items)
    back_by_tid = _index_view_by_taskmaster_id(back_items)

    findings: list[GateFinding] = []

    for tid in range(task_id_start, task_id_end + 1):
        task = tasks_by_id.get(tid)
        if not task:
            findings.append(
                GateFinding(
                    task_id=tid,
                    view_id=None,
                    kind="error",
                    code="TASK_NOT_FOUND",
                    message=f"T{tid} is not present in {tasks_path.as_posix()}",
                )
            )
            continue

        view = gameplay_by_tid.get(tid) or back_by_tid.get(tid)
        view_id = None
        if view:
            view_id = str(view.get("id") or "")
        if not view:
            findings.append(
                GateFinding(
                    task_id=tid,
                    view_id=None,
                    kind="error",
                    code="VIEW_MAPPING_MISSING",
                    message=f"T{tid} has no mapping in {gameplay_view_path.as_posix()} or {back_view_path.as_posix()}",
                )
            )
            continue

        # 1) Cross-check details metadata vs view refs (ADR/CH/Overlay).
        parsed = _parse_task_details_refs(str(task.get("details") or ""))
        view_adr = [x.strip() for x in _as_str_list(view.get("adr_refs")) if x.strip()]
        view_ch = [x.strip() for x in _as_str_list(view.get("chapter_refs")) if x.strip()]
        view_ov = [x.strip() for x in _as_str_list(view.get("overlay_refs")) if x.strip()]

        def _missing(expected: list[str], actual: list[str]) -> list[str]:
            a = {x.strip() for x in actual if x.strip()}
            return [x for x in expected if x.strip() and x.strip() not in a]

        miss_adr = _missing(parsed["adrRefs"], view_adr)
        miss_ch = _missing(parsed["chapterRefs"], view_ch)
        miss_ov = _missing(parsed["overlayRefs"], view_ov)
        if miss_adr:
            findings.append(
                GateFinding(
                    task_id=tid,
                    view_id=view_id,
                    kind="warning",
                    code="ADR_REFS_MISMATCH",
                    message=f"T{tid} details ADR Refs not fully present in view. Missing in view: {miss_adr}",
                )
            )
        if miss_ch:
            findings.append(
                GateFinding(
                    task_id=tid,
                    view_id=view_id,
                    kind="warning",
                    code="CHAPTER_REFS_MISMATCH",
                    message=f"T{tid} details Chapters not fully present in view. Missing in view: {miss_ch}",
                )
            )
        if miss_ov:
            findings.append(
                GateFinding(
                    task_id=tid,
                    view_id=view_id,
                    kind="warning",
                    code="OVERLAY_REFS_MISMATCH",
                    message=f"T{tid} details Overlays not fully present in view. Missing in view: {miss_ov}",
                )
            )

        # 2) Acceptance must exist (for gameplay view tasks).
        acceptance_items = _ensure_list(view.get("acceptance"))
        acceptance_items_str = [str(x) for x in acceptance_items if str(x).strip()]
        if not acceptance_items_str:
            findings.append(
                GateFinding(
                    task_id=tid,
                    view_id=view_id,
                    kind="error",
                    code="ACCEPTANCE_MISSING",
                    message=f"T{tid} view acceptance is empty; cannot audit detail/acceptance alignment.",
                )
            )
            continue

        # 3) Refs in acceptance must exist and be included in test_refs.
        view_status = str(view.get("status") or "").strip().lower()
        strict_files = view_status in {"in_progress", "in-progress", "doing", "done", "completed"}
        test_refs = [x.strip() for x in _as_str_list(view.get("test_refs")) if x.strip()]
        ref_paths = _collect_ref_paths_from_acceptance(acceptance_items_str)
        for rp in ref_paths:
            if rp.startswith("logs/") or rp.startswith("logs\\"):
                findings.append(
                    GateFinding(
                        task_id=tid,
                        view_id=view_id,
                        kind="warning",
                        code="ACCEPTANCE_REF_IS_LOGS",
                        message=f"T{tid} acceptance ref points into logs/** (should reference source tests/files): {rp}",
                    )
                )
            if not _file_exists(repo_root, rp):
                findings.append(
                    GateFinding(
                        task_id=tid,
                        view_id=view_id,
                        kind="error" if strict_files else "warning",
                        code="ACCEPTANCE_REF_MISSING_FILE",
                        message=f"T{tid} acceptance Refs points to missing file: {rp} (status={view_status or 'n/a'})",
                    )
                )
            if rp not in test_refs:
                findings.append(
                    GateFinding(
                        task_id=tid,
                        view_id=view_id,
                        kind="warning",
                        code="ACCEPTANCE_REF_NOT_IN_TEST_REFS",
                        message=f"T{tid} acceptance Refs not listed in test_refs: {rp}",
                    )
                )

        # 4) Event-type mentions in acceptance should be covered by contractRefs.
        contract_refs = [x.strip().lower() for x in _as_str_list(view.get("contractRefs")) if x.strip()]
        evt_mentions = _collect_event_types_from_acceptance(acceptance_items_str)
        for evt in evt_mentions:
            if evt not in contract_refs:
                findings.append(
                    GateFinding(
                        task_id=tid,
                        view_id=view_id,
                        kind="warning",
                        code="EVENT_MENTION_NOT_IN_CONTRACTREFS",
                        message=f"T{tid} acceptance mentions event type not present in contractRefs: {evt}",
                    )
                )

        # 5) Minimum fields sanity for new task view schema.
        required_fields = ["layer", "adr_refs", "chapter_refs", "overlay_refs", "depends_on"]
        for f in required_fields:
            if f not in view:
                findings.append(
                    GateFinding(
                        task_id=tid,
                        view_id=view_id,
                        kind="error",
                        code="VIEW_FIELD_MISSING",
                        message=f"T{tid} view is missing required field: {f}",
                    )
                )

    errors = [f for f in findings if f.kind == "error"]
    warnings = [f for f in findings if f.kind == "warning"]
    result = {
        "date": _today_ymd(),
        "task_range": {"start": task_id_start, "end": task_id_end},
        "errors": [f.__dict__ for f in errors],
        "warnings": [f.__dict__ for f in warnings],
        "summary": {
            "tasks_checked": (task_id_end - task_id_start + 1),
            "error_count": len(errors),
            "warning_count": len(warnings),
            "status": "ok" if len(errors) == 0 else "fail",
        },
    }
    return result


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="Semantic gate for new Taskmaster tasks vs view acceptance/test refs.")
    parser.add_argument("--task-id-start", type=int, required=True)
    parser.add_argument("--task-id-end", type=int, required=True)
    args = parser.parse_args(argv)

    repo_root = Path(__file__).resolve().parents[2]
    tasks_path = repo_root / ".taskmaster" / "tasks" / "tasks.json"
    gameplay_view_path = repo_root / ".taskmaster" / "tasks" / "tasks_gameplay.json"
    back_view_path = repo_root / ".taskmaster" / "tasks" / "tasks_back.json"

    out_dir = repo_root / "logs" / "ci" / _today_ymd() / "task-mapping"
    out_dir.mkdir(parents=True, exist_ok=True)
    out_path = out_dir / f"semantic-gate--T{args.task_id_start:02d}-T{args.task_id_end:02d}.json"

    result = run_gate(
        repo_root=repo_root,
        task_id_start=args.task_id_start,
        task_id_end=args.task_id_end,
        tasks_path=tasks_path,
        gameplay_view_path=gameplay_view_path,
        back_view_path=back_view_path,
    )
    out_path.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"SEMANTIC_GATE_ALL status={result['summary']['status']} out={out_path}")
    return 0 if result["summary"]["status"] == "ok" else 2


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
