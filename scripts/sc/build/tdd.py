#!/usr/bin/env python3
"""
sc-build tdd: TDD gatekeeper (non-generative).

This script does NOT synthesize business logic. It enforces a repeatable
red/green/refactor loop with logs under logs/ci/<date>/.

Usage (Windows):
  py -3 scripts/sc/build/tdd.py --stage green
  py -3 scripts/sc/build/tdd.py --stage red --generate-red-test
"""

from __future__ import annotations

import argparse
import os
import sys
from pathlib import Path
from typing import Any


def _bootstrap_imports() -> None:
    # scripts/sc/build/tdd.py -> scripts/sc/build -> scripts/sc
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))


_bootstrap_imports()

from _taskmaster import resolve_triplet  # noqa: E402
from _util import ci_dir, repo_root, run_cmd, write_json, write_text  # noqa: E402


def build_parser() -> argparse.ArgumentParser:
    ap = argparse.ArgumentParser(description="sc-build tdd gatekeeper")
    ap.add_argument("--stage", choices=["red", "green", "refactor"], default="green")
    ap.add_argument("--task-id", default=None, help="task id; defaults to first status=in-progress in tasks.json")
    ap.add_argument("--solution", default="Game.sln")
    ap.add_argument("--configuration", default="Debug")
    ap.add_argument("--generate-red-test", action="store_true", help="create a failing test skeleton if missing")
    ap.add_argument("--no-coverage-gate", action="store_true", help="do not enforce default coverage thresholds")
    return ap


def snapshot_contract_files() -> set[str]:
    root = repo_root() / "Game.Core" / "Contracts"
    if not root.exists():
        return set()
    return {str(p.relative_to(repo_root())).replace("\\", "/") for p in root.rglob("*.cs")}


def assert_no_new_contract_files(before: set[str]) -> None:
    after = snapshot_contract_files()
    new_files = sorted(after - before)
    if new_files:
        joined = "\n".join(f"- {p}" for p in new_files)
        raise RuntimeError(f"New contract files were created, which is not allowed:\n{joined}")


def default_task_test_path(task_id: str) -> Path:
    return repo_root() / "Game.Core.Tests" / "Tasks" / f"Task{task_id}RedTests.cs"


def ensure_red_test_exists(task_id: str, title: str, *, allow_create: bool, out_dir: Path) -> Path | None:
    target = default_task_test_path(task_id)
    if target.exists():
        return target
    if not allow_create:
        return None

    target.parent.mkdir(parents=True, exist_ok=True)
    class_name = f"Task{task_id}RedTests"
    safe_title = " ".join(str(title).split())
    content = f"""using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.Tasks;

public class {class_name}
{{
    [Fact]
    public void Red_IsFailingUntilTaskIsImplemented()
    {{
        // This test is intentionally failing to start a TDD cycle.
        true.Should().BeFalse(\"Task {task_id} not implemented yet: {safe_title}\");
    }}
}}
"""
    target.write_text(content, encoding="utf-8")
    write_text(out_dir / "generated-red-test.txt", str(target))
    return target


def run_dotnet_test_filtered(task_id: str, *, solution: str, configuration: str, out_dir: Path) -> dict[str, Any]:
    # Best-effort filter to keep the red stage scoped.
    filter_expr = f"FullyQualifiedName~Game.Core.Tests.Tasks.Task{task_id}"
    cmd = ["dotnet", "test", solution, "-c", configuration, "--filter", filter_expr]
    rc, out = run_cmd(cmd, cwd=repo_root(), timeout_sec=900)
    log_path = out_dir / "dotnet-test-filtered.log"
    write_text(log_path, out)
    return {"name": "dotnet-test-filtered", "cmd": cmd, "rc": rc, "log": str(log_path), "filter": filter_expr}


def run_green_gate(*, solution: str, configuration: str, out_dir: Path, no_coverage_gate: bool) -> dict[str, Any]:
    if not no_coverage_gate:
        os.environ.setdefault("COVERAGE_LINES_MIN", "90")
        os.environ.setdefault("COVERAGE_BRANCHES_MIN", "85")

    cmd = ["py", "-3", "scripts/python/run_dotnet.py", "--solution", solution, "--configuration", configuration]
    rc, out = run_cmd(cmd, cwd=repo_root(), timeout_sec=1_800)
    log_path = out_dir / "run_dotnet.log"
    write_text(log_path, out)
    return {"name": "run_dotnet", "cmd": cmd, "rc": rc, "log": str(log_path)}


def run_refactor_checks(out_dir: Path) -> list[dict[str, Any]]:
    steps: list[dict[str, Any]] = []
    steps.append(check_no_task_red_test_skeletons(out_dir))
    candidates = [
        ("check_test_naming", ["py", "-3", "scripts/python/check_test_naming.py"], "scripts/python/check_test_naming.py"),
        ("check_tasks_all_refs", ["py", "-3", "scripts/python/check_tasks_all_refs.py"], "scripts/python/check_tasks_all_refs.py"),
        ("validate_contracts", ["py", "-3", "scripts/python/validate_contracts.py"], "scripts/python/validate_contracts.py"),
    ]
    for name, cmd, requires in candidates:
        log_path = out_dir / f"{name}.log"
        if not (repo_root() / requires).exists():
            write_text(log_path, f"SKIP missing: {requires}\n")
            steps.append({"name": name, "cmd": cmd, "rc": 0, "log": str(log_path), "status": "skipped", "reason": f"missing:{requires}"})
            continue

        rc, out = run_cmd(cmd, cwd=repo_root(), timeout_sec=900)
        write_text(log_path, out)
        steps.append({"name": name, "cmd": cmd, "rc": rc, "log": str(log_path), "status": "ok" if rc == 0 else "fail"})
    return steps


def check_no_task_red_test_skeletons(out_dir: Path) -> dict[str, Any]:
    name = "check_no_task_red_test_skeletons"
    log_path = out_dir / f"{name}.log"

    tasks_dir = repo_root() / "Game.Core.Tests" / "Tasks"
    if not tasks_dir.exists():
        write_text(log_path, "OK: Game.Core.Tests/Tasks does not exist.\n")
        return {"name": name, "cmd": ["internal"], "rc": 0, "log": str(log_path), "status": "ok"}

    offenders = sorted(tasks_dir.glob("Task*RedTests.cs"))
    if not offenders:
        write_text(log_path, "OK: no Task<id>RedTests.cs files found.\n")
        return {"name": name, "cmd": ["internal"], "rc": 0, "log": str(log_path), "status": "ok"}

    rel_paths = [str(p.relative_to(repo_root())).replace("\\", "/") for p in offenders]
    details = "\n".join(f"- {p}" for p in rel_paths)

    message = (
        "[FAIL] Found task-scoped red test skeleton(s) which must NOT be kept at refactor stage.\n"
        "These files are generated by sc-build tdd --generate-red-test and should be migrated.\n"
        "Fix:\n"
        "  - Move assertions into stable class-scoped tests named {ClassName}Tests.cs (see docs/testing-framework.md)\n"
        "  - Delete Task<id>RedTests.cs after migration\n"
        "Found:\n"
        f"{details}\n"
    )
    write_text(log_path, message)
    return {"name": name, "cmd": ["internal"], "rc": 1, "log": str(log_path), "status": "fail", "offenders": rel_paths}


def main() -> int:
    args = build_parser().parse_args()
    out_dir = ci_dir("sc-build-tdd")

    before_contracts = snapshot_contract_files()

    summary: dict[str, Any] = {
        "cmd": "sc-build-tdd",
        "stage": args.stage,
        "status": "fail",
        "steps": [],
    }

    triplet = resolve_triplet(task_id=args.task_id)
    summary["task"] = {
        "task_id": triplet.task_id,
        "title": triplet.master.get("title"),
        "status": triplet.master.get("status"),
        "adrRefs": triplet.adr_refs(),
        "archRefs": triplet.arch_refs(),
        "overlay": triplet.overlay(),
        "taskdoc": triplet.taskdoc_path,
    }

    if args.stage == "red":
        test_path = ensure_red_test_exists(
            triplet.task_id,
            str(triplet.master.get("title") or ""),
            allow_create=args.generate_red_test,
            out_dir=out_dir,
        )
        if not test_path:
            write_json(out_dir / "summary.json", summary)
            print("[sc-build-tdd] ERROR: no task-scoped test found. Use --generate-red-test to create one.")
            return 2

        step = run_dotnet_test_filtered(
            triplet.task_id,
            solution=args.solution,
            configuration=args.configuration,
            out_dir=out_dir,
        )
        summary["steps"].append(step)

        # In red stage, we EXPECT a failure.
        summary["status"] = "ok" if step["rc"] != 0 else "unexpected_green"
        write_json(out_dir / "summary.json", summary)
        print(f"SC_BUILD_TDD status={summary['status']} out={out_dir}")
        assert_no_new_contract_files(before_contracts)
        return 0 if summary["status"] == "ok" else 1

    if args.stage == "green":
        step = run_green_gate(
            solution=args.solution,
            configuration=args.configuration,
            out_dir=out_dir,
            no_coverage_gate=args.no_coverage_gate,
        )
        summary["steps"].append(step)
        summary["status"] = "ok" if step["rc"] == 0 else "fail"
        write_json(out_dir / "summary.json", summary)
        print(f"SC_BUILD_TDD status={summary['status']} out={out_dir}")
        assert_no_new_contract_files(before_contracts)
        return 0 if step["rc"] == 0 else 1

    if args.stage == "refactor":
        steps = run_refactor_checks(out_dir)
        summary["steps"].extend(steps)
        summary["status"] = "ok" if all(s["rc"] == 0 for s in steps) else "fail"
        write_json(out_dir / "summary.json", summary)
        print(f"SC_BUILD_TDD status={summary['status']} out={out_dir}")
        assert_no_new_contract_files(before_contracts)
        return 0 if summary["status"] == "ok" else 1

    write_json(out_dir / "summary.json", summary)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
