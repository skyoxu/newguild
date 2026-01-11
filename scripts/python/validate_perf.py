#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Validate perf summary JSON and enforce a P95 threshold.

Intended for CI quality gates (Task 23).

Inputs:
- A perf summary JSON file (or glob pattern) produced under logs/perf/**.
  This script is intentionally flexible and supports both:
  - A direct numeric metric: {"p95_ms": 12.3}
  - A structured metric object: {"DB_QUERY_P95": {"p95_ms": 12.3, ...}}

Outputs:
- A machine-readable JSON report (default: logs/ci/<date>/quality-gates-perf.json)

Exit codes:
- 0: pass (within budget)
- 1: fail (over budget or invalid input)
- 2: usage/config error
"""

from __future__ import annotations

import argparse
import datetime as dt
import glob
import json
import os
import sys
from pathlib import Path
from typing import Any


def today_str() -> str:
    return dt.date.today().strftime("%Y-%m-%d")


def write_json(path: Path, obj: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2), encoding="utf-8", newline="\n")


def _coerce_float(value: Any) -> float | None:
    if isinstance(value, (int, float)):
        return float(value)
    if isinstance(value, str):
        s = value.strip()
        if not s:
            return None
        try:
            return float(s)
        except ValueError:
            return None
    return None


def _extract_metric_p95_ms(doc: dict[str, Any], metric: str) -> float | None:
    if metric in doc:
        node = doc.get(metric)
        if isinstance(node, dict):
            return _coerce_float(node.get("p95_ms"))
        return _coerce_float(node)

    # Common fallback keys
    if metric.lower() in {"p95", "p95_ms"}:
        return _coerce_float(doc.get("p95_ms"))

    return None


def build_parser() -> argparse.ArgumentParser:
    ap = argparse.ArgumentParser(description="Validate perf P95 thresholds for CI quality gates.")
    ap.add_argument("--summary-path", required=True, help="Path or glob to perf summary JSON (e.g. logs/perf/**/db-perf-summary.json)")
    ap.add_argument("--metric", required=True, help="Metric key to validate (e.g. DB_QUERY_P95)")
    ap.add_argument("--threshold-ms", required=True, help="Threshold in milliseconds (e.g. 16.6)")
    ap.add_argument("--report", default=None, help="Output JSON report path. Default: logs/ci/<date>/quality-gates-perf.json")
    ap.add_argument("--strict", action="store_true", help="Fail on any warning or missing data")
    return ap


def main() -> int:
    args = build_parser().parse_args()
    root = Path(__file__).resolve().parents[2]

    threshold = _coerce_float(args.threshold_ms)
    if threshold is None or threshold < 0:
        print("[validate_perf] ERROR: --threshold-ms must be a non-negative number", file=sys.stderr)
        return 2

    pattern = str(args.summary_path).strip()
    if not pattern:
        print("[validate_perf] ERROR: --summary-path is required", file=sys.stderr)
        return 2

    # Resolve candidates (supports glob).
    matches = glob.glob(pattern, recursive=True)
    if not matches and ("*" not in pattern and "?" not in pattern and "[" not in pattern):
        matches = [pattern]

    files = [Path(p) for p in matches]
    files = [p for p in files if p.is_file()]
    if not files:
        print(f"[validate_perf] ERROR: no summary files found for pattern: {pattern}", file=sys.stderr)
        report_path = Path(args.report) if args.report else (root / "logs" / "ci" / today_str() / "quality-gates-perf.json")
        write_json(
            report_path,
            {
                "status": "fail",
                "metric": str(args.metric),
                "threshold_ms": threshold,
                "error": "no_summary_files",
                "summary_path": pattern,
                "files": [],
            },
        )
        return 1

    metric = str(args.metric).strip()
    if not metric:
        print("[validate_perf] ERROR: --metric is required", file=sys.stderr)
        return 2

    per_file: list[dict[str, Any]] = []
    warnings: list[str] = []
    errors: list[str] = []

    max_p95: float | None = None

    for f in sorted(files, key=lambda p: str(p)):
        try:
            raw = f.read_text(encoding="utf-8")
            doc = json.loads(raw)
        except Exception as exc:  # noqa: BLE001
            errors.append(f"failed_to_read_json:{f}:{exc}")
            per_file.append({"file": str(f), "status": "fail", "error": "invalid_json"})
            continue

        if not isinstance(doc, dict):
            errors.append(f"unexpected_json_root:{f}")
            per_file.append({"file": str(f), "status": "fail", "error": "root_not_object"})
            continue

        p95 = _extract_metric_p95_ms(doc, metric)
        if p95 is None:
            warnings.append(f"missing_metric:{metric}:{f}")
            per_file.append({"file": str(f), "status": "fail", "error": "missing_metric", "metric": metric})
            continue

        max_p95 = p95 if max_p95 is None else max(max_p95, p95)
        per_file.append({"file": str(f), "status": "ok", "metric": metric, "p95_ms": p95})

    over_budget = (max_p95 is not None) and (max_p95 > threshold)
    status = "ok" if (not errors and not over_budget and (not args.strict or not warnings)) else "fail"

    report_path = Path(args.report) if args.report else (root / "logs" / "ci" / today_str() / "quality-gates-perf.json")
    write_json(
        report_path,
        {
            "status": status,
            "timestamp": dt.datetime.now(dt.UTC).isoformat(timespec="seconds"),
            "metric": metric,
            "threshold_ms": threshold,
            "p95_ms": max_p95,
            "over_budget": over_budget,
            "summary_path": pattern,
            "files": per_file,
            "warning_count": len(warnings),
            "error_count": len(errors),
            "warnings": warnings,
            "errors": errors,
        },
    )

    return 0 if status == "ok" else 1


if __name__ == "__main__":
    raise SystemExit(main())

