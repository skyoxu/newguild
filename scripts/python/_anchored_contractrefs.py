#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Anchor-scoped event type extraction for contractRefs coverage.

Stop false positives:
This module extracts event type mentions only from code segments bound to
ACC:T<id>.<n> anchors, instead of scanning the whole referenced file.
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from typing import Any


@dataclass(frozen=True)
class Occurrence:
    file: str
    line: int
    kind: str  # literal|eventtype_ref
    token: str
    event_type: str


EVENT_TYPE_LITERAL_RE = re.compile(r'["\']((?:core|ui\.menu|screen)\.[a-z0-9_.]+)["\']')
EVENTTYPE_REF_RE = re.compile(r"\b([A-Za-z0-9_.]+)\.EventType\b")

IGNORE_EVENTTYPE_PREFIXES = (
    "core.test.",
    "core.tests.",
    "core.example.",
    "core.sample.",
)


def _is_ignored_event_type(event_type: str) -> bool:
    value = (event_type or "").strip()
    return any(value.startswith(prefix) for prefix in IGNORE_EVENTTYPE_PREFIXES)


def _extract_occurrences_from_lines(
    *,
    lines: list[str],
    file_rel: str,
    start_line_number: int,
    eventtype_map: dict[str, str],
) -> list[Occurrence]:
    occurrences: list[Occurrence] = []
    for offset, line in enumerate(lines):
        line_number = start_line_number + offset

        for match in EVENT_TYPE_LITERAL_RE.finditer(line):
            event_type = match.group(1)
            if _is_ignored_event_type(event_type):
                continue
            occurrences.append(Occurrence(file=file_rel, line=line_number, kind="literal", token=match.group(0), event_type=event_type))

        for match in EVENTTYPE_REF_RE.finditer(line):
            raw = match.group(1)
            type_name = raw.split(".")[-1]
            resolved = eventtype_map.get(type_name)
            if not resolved:
                continue
            if not (resolved.startswith("core.") or resolved.startswith("ui.") or resolved.startswith("screen.")):
                continue
            if _is_ignored_event_type(resolved):
                continue
            occurrences.append(Occurrence(file=file_rel, line=line_number, kind="eventtype_ref", token=match.group(0), event_type=resolved))

    return occurrences


def _extract_cs_method_block(lines: list[str], anchor_index: int) -> tuple[int, int] | None:
    """
    Returns (start_index, end_index) for the method following the anchor.
    Best-effort, deterministic (simple brace counting).
    """
    max_lookahead = min(len(lines), anchor_index + 60)

    method_sig_re = re.compile(r"^\s*(public|private|internal|protected)\s+.+\(")

    signature_index: int | None = None
    for idx in range(anchor_index, max_lookahead):
        candidate = lines[idx].strip()
        if not candidate or candidate.startswith("//"):
            continue
        if candidate.startswith("["):
            continue
        if method_sig_re.search(lines[idx]):
            signature_index = idx
            break

    if signature_index is None:
        return None

    brace_index: int | None = None
    brace_line_pos: int | None = None
    for idx in range(signature_index, max_lookahead):
        pos = lines[idx].find("{")
        if pos >= 0:
            brace_index = idx
            brace_line_pos = pos
            break
        if "=>" in lines[idx] and ";" in lines[idx]:
            return signature_index, idx

    if brace_index is None or brace_line_pos is None:
        return None

    depth = 0
    for idx in range(brace_index, len(lines)):
        line = lines[idx]
        for char in line:
            if char == "{":
                depth += 1
            elif char == "}":
                depth -= 1
                if depth == 0:
                    return signature_index, idx

    return signature_index, len(lines) - 1


def _extract_gd_function_block(lines: list[str], anchor_index: int) -> tuple[int, int] | None:
    max_lookahead = min(len(lines), anchor_index + 40)

    func_index: int | None = None
    func_indent: str = ""
    for idx in range(anchor_index, max_lookahead):
        raw = lines[idx]
        stripped = raw.lstrip(" \t")
        if not stripped:
            continue
        if stripped.startswith("#"):
            continue
        if stripped.startswith("func "):
            func_index = idx
            func_indent = raw[: len(raw) - len(stripped)]
            break

    if func_index is None:
        return None

    for idx in range(func_index + 1, len(lines)):
        raw = lines[idx]
        stripped = raw.lstrip(" \t")
        if stripped.startswith("func ") and raw.startswith(func_indent):
            return func_index, idx - 1

    return func_index, len(lines) - 1


def extract_event_occurrences_for_task(
    *,
    text: str,
    file_rel: str,
    task_id: str,
    eventtype_map: dict[str, str],
) -> list[Any]:
    """
    Extract occurrences for the given task id from anchor-bound segments only.
    Returns a list of objects with fields matching check_task_contractrefs_coverage.Occurrence.
    """
    task_id = str(task_id).strip()
    if not task_id:
        return []

    anchor_re = re.compile(rf"\bACC:T{re.escape(task_id)}\.\d+\b")
    lines = text.splitlines()
    anchor_indexes = [idx for idx, line in enumerate(lines) if anchor_re.search(line)]
    if not anchor_indexes:
        return []

    occurrences: list[Occurrence] = []
    file_lower = file_rel.lower()

    for anchor_index in anchor_indexes:
        block: tuple[int, int] | None = None
        if file_lower.endswith(".cs"):
            block = _extract_cs_method_block(lines, anchor_index)
        elif file_lower.endswith(".gd"):
            block = _extract_gd_function_block(lines, anchor_index)
        else:
            continue

        if block is None:
            window_start = max(0, anchor_index - 5)
            window_end = min(len(lines) - 1, anchor_index + 50)
            segment_lines = lines[window_start : window_end + 1]
            occurrences.extend(
                _extract_occurrences_from_lines(
                    lines=segment_lines,
                    file_rel=file_rel,
                    start_line_number=window_start + 1,
                    eventtype_map=eventtype_map,
                )
            )
            continue

        start_index, end_index = block
        segment_lines = lines[start_index : end_index + 1]
        occurrences.extend(
            _extract_occurrences_from_lines(
                lines=segment_lines,
                file_rel=file_rel,
                start_line_number=start_index + 1,
                eventtype_map=eventtype_map,
            )
        )

    return occurrences

