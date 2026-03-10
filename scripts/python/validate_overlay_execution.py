#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Validate Lastking overlay execution readiness.

This validator is deterministic and checks:
1) Required overlay files exist.
2) Front matter fields are present and aligned.
3) Required execution sections exist in each page.
4) Backtick path references resolve on disk when they are concrete paths.

Outputs:
- logs/ci/<YYYY-MM-DD>/overlay-lint/report.json
- logs/ci/<YYYY-MM-DD>/overlay-lint/report.md
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import re
from pathlib import Path
from typing import Any


def repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


def today_str() -> str:
    return dt.date.today().strftime("%Y-%m-%d")


def ci_out_dir() -> Path:
    out = repo_root() / "logs" / "ci" / today_str() / "overlay-lint"
    out.mkdir(parents=True, exist_ok=True)
    return out


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def write_json(path: Path, payload: Any) -> None:
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")


def write_text(path: Path, text: str) -> None:
    path.write_text(text.replace("\r\n", "\n") + "\n", encoding="utf-8", newline="\n")


def parse_front_matter(md: str) -> dict[str, Any]:
    lines = md.splitlines()
    if len(lines) < 3 or lines[0].strip() != "---":
        return {}

    end_idx = None
    for i in range(1, len(lines)):
        if lines[i].strip() == "---":
            end_idx = i
            break
    if end_idx is None:
        return {}

    block = lines[1:end_idx]
    result: dict[str, Any] = {}
    current_key: str | None = None

    for raw in block:
        line = raw.rstrip()
        if not line.strip():
            continue

        if re.match(r"^\s+-\s+", line) and current_key:
            result.setdefault(current_key, [])
            if isinstance(result[current_key], list):
                result[current_key].append(re.sub(r"^\s+-\s+", "", line).strip())
            continue

        m = re.match(r"^([A-Za-z0-9_\-]+)\s*:\s*(.*)$", line)
        if not m:
            continue

        key = m.group(1).strip()
        value = m.group(2).strip()
        current_key = key

        if value.startswith("[") and value.endswith("]"):
            body = value[1:-1].strip()
            if not body:
                result[key] = []
            else:
                result[key] = [x.strip() for x in body.split(",") if x.strip()]
        elif value == "":
            result[key] = []
        else:
            result[key] = value

    return result


def has_markdown_heading(md: str, heading: str) -> bool:
    pattern = rf"^##\s+{re.escape(heading)}\s*$"
    return re.search(pattern, md, flags=re.MULTILINE) is not None


def extract_backtick_paths(md: str) -> list[str]:
    refs = re.findall(r"`([^`]+)`", md)
    out: list[str] = []
    for ref in refs:
        txt = ref.strip()
        if not txt:
            continue
        if txt.startswith("py -3 ") or txt.startswith("dotnet "):
            continue
        if "/" not in txt and "\\" not in txt:
            continue
        out.append(txt.replace("\\", "/"))
    return out


def should_check_path(path: str) -> bool:
    if "<" in path or ">" in path:
        return False
    if path.startswith("logs/"):
        return False
    prefixes = (
        "docs/",
        "scripts/",
        "Game.Core/",
        "Game.Core.Tests/",
        "Tests.Godot/",
        ".taskmaster/",
    )
    return path.startswith(prefixes)


def validate_file_paths(root: Path, md_text: str, rel: str) -> tuple[list[str], list[str]]:
    errors: list[str] = []
    warnings: list[str] = []
    refs = extract_backtick_paths(md_text)

    for ref in refs:
        if not should_check_path(ref):
            continue
        p = root / ref
        if not p.exists():
            errors.append(f"{rel}: missing referenced path: {ref}")

    if not refs:
        warnings.append(f"{rel}: no backtick references found")

    return errors, warnings


def validate_overlay(prd_id: str, overlay_dir: Path) -> dict[str, Any]:
    root = repo_root()
    errors: list[str] = []
    warnings: list[str] = []

    required_files = [
        "_index.md",
        "08-Feature-Slice-T2-Core-Loop.md",
        "08-Contracts-T2.md",
        "08-Testing-T2.md",
        "08-Observability-T2.md",
        "ACCEPTANCE_CHECKLIST.md",
    ]

    required_sections: dict[str, list[str]] = {
        "08-Feature-Slice-T2-Core-Loop.md": [
            "Runtime Boundary",
            "Domain Entities",
            "Event Contracts",
            "Runtime State Machine",
            "Failure Paths",
            "Acceptance Anchors",
            "Task Mapping",
        ],
        "08-Contracts-T2.md": [
            "Contract Inventory",
            "Field Constraints",
            "Versioning and Migration",
            "Breaking Change Policy",
            "Local Validation",
        ],
        "08-Testing-T2.md": [
            "Test Layers",
            "Requirement-to-Test Mapping",
            "Test Execution Matrix (Windows)",
            "Evidence Policy",
        ],
        "08-Observability-T2.md": [
            "Artifact Naming",
            "Mandatory JSON Fields",
            "Gate Failure Handling",
            "Release Health Linkage",
        ],
        "ACCEPTANCE_CHECKLIST.md": [
            "Quantified Pass/Fail Criteria",
            "Required Commands (Windows)",
            "DoD Anchors",
        ],
        "_index.md": [
            "Scope",
            "Canonical Pages",
            "Execution Invariants",
            "Validation Commands (Windows)",
        ],
    }

    for name in required_files:
        p = overlay_dir / name
        rel = str(p.relative_to(root)).replace("\\", "/")
        if not p.exists():
            errors.append(f"missing required overlay file: {rel}")
            continue

        text = read_text(p)
        fm = parse_front_matter(text)

        if name != "ACCEPTANCE_CHECKLIST.md":
            if not fm:
                errors.append(f"{rel}: missing front matter block")
            else:
                if str(fm.get("PRD-ID", "")).strip() != prd_id:
                    errors.append(f"{rel}: PRD-ID mismatch, expected {prd_id}")
                if not str(fm.get("Title", "")).strip():
                    errors.append(f"{rel}: missing Title in front matter")
                if name != "_index.md" and not fm.get("ADR-Refs"):
                    errors.append(f"{rel}: missing ADR-Refs in front matter")
                if name in {
                    "08-Feature-Slice-T2-Core-Loop.md",
                    "08-Contracts-T2.md",
                    "08-Testing-T2.md",
                    "08-Observability-T2.md",
                } and not fm.get("Test-Refs"):
                    errors.append(f"{rel}: missing Test-Refs in front matter")

        for heading in required_sections.get(name, []):
            if not has_markdown_heading(text, heading):
                errors.append(f"{rel}: missing section heading '## {heading}'")

        e2, w2 = validate_file_paths(root, text, rel)
        errors.extend(e2)
        warnings.extend(w2)

    checklist = overlay_dir / "ACCEPTANCE_CHECKLIST.md"
    if checklist.exists():
        body = read_text(checklist)
        if "| Check ID | Pass Criterion | Fail Condition | Evidence |" not in body:
            errors.append("ACCEPTANCE_CHECKLIST.md: missing quantified acceptance table header")
        if body.count("| AC-") < 5:
            errors.append("ACCEPTANCE_CHECKLIST.md: expected at least 5 AC-* checklist rows")

    return {
        "prd_id": prd_id,
        "overlay_dir": str(overlay_dir.relative_to(root)).replace("\\", "/"),
        "errors": errors,
        "warnings": warnings,
        "status": "ok" if not errors else "fail",
    }


def main() -> int:
    ap = argparse.ArgumentParser(description="Validate overlay execution readiness for Lastking.")
    ap.add_argument("--prd-id", default="PRD-lastking-T2")
    ap.add_argument("--overlay-dir", default="")
    args = ap.parse_args()

    root = repo_root()
    overlay_dir = (
        (root / args.overlay_dir)
        if args.overlay_dir
        else (root / "docs" / "architecture" / "overlays" / args.prd_id / "08")
    )

    report = validate_overlay(args.prd_id, overlay_dir)
    out_dir = ci_out_dir()

    report_json = out_dir / "report.json"
    report_md = out_dir / "report.md"

    write_json(report_json, report)

    md_lines = [
        "# Overlay Execution Validation",
        "",
        f"- prd_id: {report['prd_id']}",
        f"- overlay_dir: {report['overlay_dir']}",
        f"- status: {report['status']}",
        f"- errors: {len(report['errors'])}",
        f"- warnings: {len(report['warnings'])}",
        "",
    ]

    if report["errors"]:
        md_lines.append("## Errors")
        for err in report["errors"]:
            md_lines.append(f"- {err}")
        md_lines.append("")

    if report["warnings"]:
        md_lines.append("## Warnings")
        for w in report["warnings"]:
            md_lines.append(f"- {w}")
        md_lines.append("")

    write_text(report_md, "\n".join(md_lines).strip())

    print(
        f"OVERLAY_EXEC_VALIDATION status={report['status']} errors={len(report['errors'])} "
        f"warnings={len(report['warnings'])} out={out_dir}"
    )

    return 0 if report["status"] == "ok" else 1


if __name__ == "__main__":
    raise SystemExit(main())
