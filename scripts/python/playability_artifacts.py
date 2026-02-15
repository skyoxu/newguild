#!/usr/bin/env python3
"""Playability artifact helpers for GdUnit runners."""

from __future__ import annotations

import datetime as dt
import os
from pathlib import Path
import shutil
import xml.etree.ElementTree as ET


DEFAULT_ROUTE2_ARTIFACT_NAME = "playability-route2-summary.json"
DEFAULT_ROUTE2_TEST_MARKER = "phase2_play_route"


def resolve_route2_artifact_requirement(mode_raw: str) -> tuple[str, str]:
    value = (mode_raw or "auto").strip().lower()
    if value in {"1", "true", "yes", "on", "always", "required"}:
        return "always", "explicit-enabled"
    if value in {"0", "false", "no", "off", "never", "disabled"}:
        return "never", "explicit-disabled"
    if value == "auto":
        return "auto", "auto-deferred"
    raise ValueError(
        f"Unsupported route2 artifact requirement mode: '{mode_raw}'. "
        "Use auto|always|never (or true|false, 1|0)."
    )


def is_route2_suite_requested(add_paths: list[str], route2_test_marker: str) -> bool:
    normalized_paths = [value.replace("\\", "/").lower() for value in add_paths]
    marker = (route2_test_marker or DEFAULT_ROUTE2_TEST_MARKER).strip().lower()

    if len(normalized_paths) == 0:
        return True

    for path in normalized_paths:
        if marker and marker in path:
            return True
        if "tests/playability/phase2" in path:
            return True
        if path.endswith("tests/playability"):
            return True
        if path.endswith("tests.godot/tests"):
            return True
    return False


def decide_route2_requirement(
    *,
    route2_requirement_mode: str,
    route2_requirement_base_reason: str,
    route2_test_executed: bool,
    route2_detection_error: str | None,
    route2_suite_requested: bool,
) -> tuple[bool, str, list[str], list[str]]:
    warnings: list[str] = []
    errors: list[str] = []

    if route2_requirement_mode == "always":
        return True, route2_requirement_base_reason, warnings, errors
    if route2_requirement_mode == "never":
        return False, route2_requirement_base_reason, warnings, errors

    if not route2_detection_error:
        if route2_suite_requested and not route2_test_executed:
            return True, "auto-requested-not-detected-fail-closed", warnings, errors
        reason = "auto-route2-executed" if route2_test_executed else "auto-route2-not-executed"
        return route2_test_executed, reason, warnings, errors

    detection_tokens = [token.strip() for token in route2_detection_error.split(";") if token.strip()]
    limit_only_warning = len(detection_tokens) > 0 and all(
        token.startswith("xml_file_limit_exceeded:") for token in detection_tokens
    )

    if route2_suite_requested:
        if limit_only_warning:
            warnings.append(f"route2 detection window warning: {route2_detection_error}")
            return True, "auto-requested-detection-limit-warning", warnings, errors
        errors.append(f"route2 auto-detection failed: {route2_detection_error}")
        return True, "auto-requested-detection-error-fail-closed", warnings, errors

    warnings.append(f"route2 auto-detection warning: {route2_detection_error}")
    return False, "auto-detection-warning", warnings, errors


def _read_console_tail(console_path: Path, max_console_bytes: int) -> str:
    safe_limit = max(1, max_console_bytes)
    with console_path.open("rb") as file_obj:
        file_obj.seek(0, 2)
        file_size = file_obj.tell()
        file_obj.seek(max(0, file_size - safe_limit), 0)
        payload = file_obj.read(safe_limit)
    return payload.decode("utf-8", errors="ignore")


def _iter_xml_files(report_root: Path):
    for current_root, dir_names, file_names in os.walk(report_root):
        dir_names.sort()
        for file_name in sorted(file_names):
            if file_name.lower().endswith(".xml"):
                yield Path(current_root) / file_name


def detect_route2_execution(
    report_dir: str,
    console_path: str,
    route2_test_marker: str,
    *,
    run_started_at_utc: dt.datetime,
    max_xml_files: int,
    max_xml_bytes: int,
    max_console_bytes: int,
) -> tuple[bool, str | None]:
    marker = (route2_test_marker or DEFAULT_ROUTE2_TEST_MARKER).strip().lower()
    if not marker:
        marker = DEFAULT_ROUTE2_TEST_MARKER

    xml_file_limit = max(1, max_xml_files)
    xml_size_limit = max(1, max_xml_bytes)
    console_size_limit = max(1, max_console_bytes)

    parse_errors: list[str] = []
    try:
        report_root = Path(report_dir)
        if report_root.is_dir():
            scanned_xml_files = 0
            for xml_path in _iter_xml_files(report_root):
                try:
                    xml_stat = xml_path.stat()
                except Exception as ex:
                    parse_errors.append(f"xml_stat_failed:{xml_path.name}:{type(ex).__name__}")
                    continue

                xml_modified_at = dt.datetime.fromtimestamp(xml_stat.st_mtime, tz=dt.timezone.utc)
                if xml_modified_at < (run_started_at_utc - dt.timedelta(seconds=5)):
                    continue

                scanned_xml_files += 1
                if scanned_xml_files > xml_file_limit:
                    parse_errors.append(
                        f"xml_file_limit_exceeded:count>{xml_file_limit}:limit={xml_file_limit}"
                    )
                    break
                if xml_stat.st_size > xml_size_limit:
                    parse_errors.append(f"xml_size_limit_exceeded:{xml_path.name}:limit={xml_size_limit}")
                    continue

                try:
                    xml_head = xml_path.read_bytes()[:4096]
                    if b"<!DOCTYPE" in xml_head.upper():
                        parse_errors.append(f"xml_doctype_rejected:{xml_path.name}")
                        continue
                except Exception as ex:
                    parse_errors.append(f"xml_head_read_failed:{xml_path.name}:{type(ex).__name__}")
                    continue
                try:
                    root = ET.parse(xml_path).getroot()
                except Exception as ex:
                    parse_errors.append(f"xml_parse_failed:{xml_path.name}:{type(ex).__name__}")
                    continue
                for test_case in root.iter("testcase"):
                    class_name = (test_case.attrib.get("classname") or "").lower()
                    case_name = (test_case.attrib.get("name") or "").lower()
                    if marker in class_name or marker in case_name:
                        return True, None
    except Exception as ex:
        parse_errors.append(f"report_scan_failed:{type(ex).__name__}")

    console_error = None
    try:
        console_text = _read_console_tail(Path(console_path), console_size_limit).lower()
        if marker in console_text:
            return True, None
    except Exception as ex:
        console_error = f"console_read_failed:{type(ex).__name__}"

    if parse_errors or console_error:
        issues = list(parse_errors)
        if console_error:
            issues.append(console_error)
        return False, "; ".join(issues)

    return False, None


def sanitize_source_root_for_audit(path: Path, repo_root: Path) -> str:
    try:
        resolved = path.resolve()
        relative = resolved.relative_to(repo_root)
        return str(relative).replace("\\", "/")
    except Exception:
        return f"external:{path.name}"


def collect_playability_artifacts(
    *,
    artifact_names: list[str],
    artifact_source_roots: list[Path],
    artifact_source_root_labels: list[str],
    date: str,
    run_started_at_utc: dt.datetime,
    out_dir: Path,
    repo_root: Path,
    artifact_name_errors: list[str],
    require_route2_artifact: bool,
    route2_artifact_name: str,
    route2_requirement_mode: str,
    route2_requirement_reason: str,
    route2_test_executed: bool,
    artifact_max_mb: int,
    artifact_max_bytes: int,
) -> tuple[dict, list[str], bool]:
    artifacts_to_check = list(artifact_names)
    if require_route2_artifact and route2_artifact_name not in artifacts_to_check:
        artifacts_to_check.append(route2_artifact_name)

    copied: list[str] = []
    copied_names: set[str] = set()
    missing: list[str] = []
    errors: list[str] = []

    for artifact_name in artifacts_to_check:
        copied_one = False
        rejected_size_messages: list[str] = []
        for source_root in artifact_source_roots:
            source_day_dir = (source_root / "logs" / "e2e" / date).resolve()
            candidate = (source_day_dir / artifact_name).resolve()
            try:
                candidate.relative_to(source_day_dir)
            except Exception:
                continue

            if not candidate.is_file():
                continue

            try:
                candidate_stat = candidate.stat()
                modified_at_utc = dt.datetime.fromtimestamp(candidate_stat.st_mtime, tz=dt.timezone.utc)
                if modified_at_utc < (run_started_at_utc - dt.timedelta(seconds=5)):
                    continue

                if artifact_max_bytes > 0 and candidate_stat.st_size > artifact_max_bytes:
                    rejected_size_messages.append(
                        f"artifact too large: {artifact_name} size_bytes={candidate_stat.st_size} max_bytes={artifact_max_bytes}"
                    )
                    continue
            except Exception:
                continue

            target_root = out_dir.resolve()
            target = (target_root / artifact_name).resolve()
            try:
                target.relative_to(target_root)
            except Exception:
                continue

            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(candidate, target)
            try:
                copied_path = str(target.relative_to(repo_root)).replace("\\", "/")
            except Exception:
                try:
                    copied_path = str(target.relative_to(target_root)).replace("\\", "/")
                except Exception:
                    copied_path = f"external:{target.name}"
            copied.append(copied_path)
            copied_names.add(artifact_name)
            copied_one = True
            break

        if not copied_one:
            missing.append(artifact_name)
            errors.extend(rejected_size_messages)

    if artifact_name_errors:
        errors.append("invalid artifact names in SC_PLAYABILITY_ARTIFACTS:\n" + "\n".join(artifact_name_errors))

    if require_route2_artifact and route2_artifact_name not in copied_names:
        errors.append(
            f"required artifact missing: {route2_artifact_name}\n"
            f"hint: ensure route2 playability suite runs and writes user://logs/e2e/<date>/{route2_artifact_name}\n"
        )

    failed = len(errors) > 0
    payload = {
        "date": date,
        "source_roots": artifact_source_root_labels,
        "source_root_hints": [sanitize_source_root_for_audit(path, repo_root) for path in artifact_source_roots],
        "configured_artifacts": artifact_names,
        "artifacts_checked": artifacts_to_check,
        "artifact_max_mb": artifact_max_mb,
        "artifact_max_bytes": artifact_max_bytes,
        "copied": copied,
        "missing": missing,
        "require_route2_artifact": require_route2_artifact,
        "route2_artifact_name": route2_artifact_name,
        "route2_requirement_mode": route2_requirement_mode,
        "route2_requirement_reason": route2_requirement_reason,
        "route2_test_executed": route2_test_executed,
        "invalid_artifact_names": artifact_name_errors,
        "rc_after_artifact_check": 1 if failed else 0,
    }
    return payload, errors, failed
