#!/usr/bin/env python3
"""
Run dotnet restore/test with coverage and archive artifacts under logs/unit/<date>/.
Exits non-zero on test failure or when coverage thresholds (if provided) are not met.

Env thresholds (optional):
  COVERAGE_LINES_MIN   e.g., "90" (percent)
  COVERAGE_BRANCHES_MIN e.g., "85" (percent)

Usage (Windows):
  py -3 scripts/python/run_dotnet.py --solution Game.sln --configuration Debug
"""
import argparse
import datetime as dt
import io
import json
import os.path
import os
import locale
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
import re


def _decode_output(data: bytes) -> str:
    if not data:
        return ""

    preferred = locale.getpreferredencoding(False) or "utf-8"
    candidates = []
    for enc in (preferred, "utf-8", "utf-16", "utf-16le"):
        try:
            text = data.decode(enc, errors="replace")
        except Exception:
            continue
        candidates.append((text.count("\ufffd"), len(text), text))

    if not candidates:
        return data.decode("utf-8", errors="replace")

    candidates.sort(key=lambda t: (t[0], -t[1]))
    return candidates[0][2]


def run_cmd(args, cwd=None, timeout=900_000, env=None):
    p = subprocess.Popen(
        args,
        cwd=cwd,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        env=env,
        text=False,
    )
    try:
        out, _ = p.communicate(timeout=timeout / 1000.0)
    except subprocess.TimeoutExpired:
        p.kill()
        out, _ = p.communicate()
        return 124, _decode_output(out)
    return p.returncode, _decode_output(out)


def ensure_dir(path):
    os.makedirs(path, exist_ok=True)


def parse_cobertura(path):
    try:
        tree = ET.parse(path)
        root = tree.getroot()
        # Cobertura schema with attributes lines-covered/lines-valid etc.
        lc = int(root.attrib.get('lines-covered', '0'))
        lv = int(root.attrib.get('lines-valid', '0'))
        bc = int(root.attrib.get('branches-covered', '0'))
        bv = int(root.attrib.get('branches-valid', '0'))
        line_pct = round((lc*100.0)/lv, 2) if lv > 0 else 0.0
        branch_pct = round((bc*100.0)/bv, 2) if bv > 0 else 0.0
        return {
            'lines_covered': lc,
            'lines_valid': lv,
            'branches_covered': bc,
            'branches_valid': bv,
            'line_pct': line_pct,
            'branch_pct': branch_pct,
        }
    except Exception as e:
        return {'error': str(e)}


_CC_RE = re.compile(r"\((\d+)\s*/\s*(\d+)\)")


def parse_cobertura_union(paths: list[str]) -> dict:
    """Compute union coverage across multiple cobertura XMLs.

    Union model:
      - line coverage is union by (filename, line_number) with max(hits>0)
      - branch coverage is union by (filename, line_number) with max(covered)/max(valid) from condition-coverage

    This avoids double-counting when multiple test projects cover the same source files.
    """

    line_hits: dict[tuple[str, int], bool] = {}
    branch_counts: dict[tuple[str, int], tuple[int, int]] = {}
    errors: list[dict] = []

    for p in paths:
        try:
            tree = ET.parse(p)
            root = tree.getroot()

            for class_el in root.findall(".//class"):
                filename = class_el.attrib.get("filename")
                if not filename:
                    continue

                for line_el in class_el.findall(".//line"):
                    try:
                        num = int(line_el.attrib.get("number", "0"))
                    except Exception:
                        continue
                    if num <= 0:
                        continue

                    key = (filename, num)
                    try:
                        hits = int(line_el.attrib.get("hits", "0"))
                    except Exception:
                        hits = 0
                    prev = line_hits.get(key, False)
                    line_hits[key] = prev or (hits > 0)

                    if str(line_el.attrib.get("branch", "")).lower() == "true":
                        cc = line_el.attrib.get("condition-coverage")
                        if cc:
                            m = _CC_RE.search(cc)
                            if m:
                                covered = int(m.group(1))
                                valid = int(m.group(2))
                                prev_cov, prev_valid = branch_counts.get(key, (0, 0))
                                branch_counts[key] = (max(prev_cov, covered), max(prev_valid, valid))
        except Exception as exc:
            errors.append({"path": p, "error": str(exc)})

    lines_valid = len(line_hits)
    lines_covered = sum(1 for v in line_hits.values() if v)
    branches_valid = sum(v for (_k, (_c, v)) in branch_counts.items())
    branches_covered = sum(c for (_k, (c, _v)) in branch_counts.items())
    line_pct = round((lines_covered * 100.0) / lines_valid, 2) if lines_valid > 0 else 0.0
    branch_pct = round((branches_covered * 100.0) / branches_valid, 2) if branches_valid > 0 else 0.0

    out = {
        "lines_covered": lines_covered,
        "lines_valid": lines_valid,
        "branches_covered": branches_covered,
        "branches_valid": branches_valid,
        "line_pct": line_pct,
        "branch_pct": branch_pct,
        "method": "union_by_file_line",
    }
    if errors:
        out["errors"] = errors
    return out


def _try_get_solution_test_projects(solution_path: str, root: str, env: dict) -> list[str]:
    rc, out = run_cmd(['dotnet', 'sln', solution_path, 'list'], cwd=root, env=env)
    if rc != 0:
        return []

    projects = []
    for raw in out.splitlines():
        line = raw.strip()
        if not line:
            continue
        if not line.lower().endswith('.csproj'):
            continue

        full_path = os.path.join(root, line)
        if not os.path.exists(full_path):
            continue

        # Unit tests are expected to live in *.Tests.csproj. We explicitly exclude
        # integration harness projects like Tests.Godot.
        if line.lower().endswith('.tests.csproj'):
            projects.append(line)

    return projects


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--solution', default='Game.sln')
    ap.add_argument('--configuration', default='Debug')
    ap.add_argument('--out-dir', default=None)
    args = ap.parse_args()

    root = os.getcwd()
    date = dt.date.today().strftime('%Y-%m-%d')
    out_dir = args.out_dir or os.path.join(root, 'logs', 'unit', date)
    ensure_dir(out_dir)
    ci_dir = os.path.join(root, 'logs', 'ci', date)
    ensure_dir(ci_dir)

    summary = {
        'solution': args.solution,
        'configuration': args.configuration,
        'out_dir': out_dir,
        'status': 'fail',
    }

    env = os.environ.copy()
    env.setdefault("DOTNET_CLI_UI_LANGUAGE", "en")
    env.setdefault("DOTNET_NOLOGO", "1")

    # Restore
    #
    # NOTE: We intentionally avoid parallel restore for solution files because it can fail
    # non-deterministically on Windows (observed as exit code 1 with no output).
    # Running MSBuild restore with -m:1 is slower but stable and still uses NuGet restore.
    rc, out = run_cmd(['dotnet', 'msbuild', args.solution, '-t:Restore', '-m:1', '-v:minimal'], cwd=root, env=env)
    with io.open(os.path.join(out_dir, 'dotnet-restore.log'), 'w', encoding='utf-8') as f:
        f.write(out)
    summary['restore_rc'] = rc
    if rc != 0:
        with io.open(os.path.join(out_dir, 'summary.json'), 'w', encoding='utf-8') as f:
            json.dump(summary, f, ensure_ascii=False, indent=2)
        print(f'RUN_DOTNET status=fail stage=restore out={out_dir}')
        return 1

    # Test with coverage
    #
    # We run an explicit restore step above; skip restore during test to avoid redundant work.
    # Run test projects individually to avoid solution-wide integration harnesses (e.g. Tests.Godot)
    # slowing down unit gates and hitting external tool timeouts.
    test_projects = _try_get_solution_test_projects(args.solution, root, env)
    summary['test_projects'] = test_projects

    test_results_root = os.path.join(out_dir, 'testresults')
    ensure_dir(test_results_root)

    combined_out = []
    combined_rc = 0
    cobertura_paths = []
    trx_outputs: list[str] = []

    targets = test_projects if test_projects else [args.solution]
    for idx, target in enumerate(targets, 1):
        safe_name = os.path.basename(target).replace('.csproj', '').replace('.', '_')
        results_dir = os.path.join(test_results_root, safe_name)
        ensure_dir(results_dir)
        trx_name = f'{safe_name}.trx' if test_projects else 'tests.trx'

        rc, out = run_cmd(
            [
                'dotnet', 'test', target,
                '-c', args.configuration,
                '--no-restore',
                '--collect:XPlat Code Coverage',
                '--logger', f'trx;LogFileName={trx_name}',
                '--results-directory', results_dir,
                '--',
                '-m:1',
            ],
            cwd=root,
            timeout=1_800_000,
            env=env,
        )
        combined_rc = combined_rc or rc
        combined_out.append(f'=== [{idx}] dotnet test {target} rc={rc} ===\n{out}\n')

        trx_path = os.path.join(results_dir, trx_name)
        if os.path.exists(trx_path):
            out_trx = os.path.join(out_dir, trx_name)
            shutil.copyfile(trx_path, out_trx)
            trx_outputs.append(out_trx)

        for cur_root, _, files in os.walk(results_dir):
            for name in files:
                if name == 'coverage.cobertura.xml':
                    cobertura_paths.append(os.path.join(cur_root, name))

    with io.open(os.path.join(out_dir, 'dotnet-test-output.txt'), 'w', encoding='utf-8') as f:
        f.write('\n'.join(combined_out))

    summary['test_rc'] = combined_rc
    summary['trx_files'] = [os.path.basename(p) for p in sorted(trx_outputs)]

    # Compatibility output: some pipelines expect a stable TRX filename at logs/unit/<date>/tests.trx,
    # even when we run multiple test projects.
    stable_trx = os.path.join(out_dir, 'tests.trx')
    if not os.path.exists(stable_trx) and trx_outputs:
        try:
            shutil.copyfile(sorted(trx_outputs)[0], stable_trx)
        except Exception:
            pass

    summary['coverage_sources'] = cobertura_paths

    # Compatibility output: scripts/sc/test.py expects a stable path at logs/unit/<date>/coverage.cobertura.xml
    # (even if the actual files live under TestResults/).
    if cobertura_paths:
        try:
            shutil.copyfile(cobertura_paths[0], os.path.join(out_dir, 'coverage.cobertura.xml'))
        except Exception:
            pass

    coverage = None
    if cobertura_paths:
        coverage = parse_cobertura_union(cobertura_paths)
        summary['coverage'] = coverage

    # Thresholds (default hard gate: lines>=90, branches>=85)
    # Allow overrides via environment variables.
    # Note: setting the env var to an empty string disables that threshold (used by sc-build tdd --no-coverage-gate).
    lines_min = os.environ.get('COVERAGE_LINES_MIN', '90')
    branches_min = os.environ.get('COVERAGE_BRANCHES_MIN', '85')
    threshold_ok = True
    if coverage and (lines_min or branches_min):
        try:
            if lines_min:
                threshold_ok = threshold_ok and (coverage.get('line_pct', 0) >= float(lines_min))
            if branches_min:
                threshold_ok = threshold_ok and (coverage.get('branch_pct', 0) >= float(branches_min))
        except Exception:
            pass
    summary['threshold_ok'] = threshold_ok
    summary['thresholds'] = {
        'lines_min': lines_min if lines_min else None,
        'branches_min': branches_min if branches_min else None,
    }

    # Base status before applying any override
    test_rc = int(summary.get('test_rc') or 0)
    status = 'ok' if (test_rc == 0 and threshold_ok) else ('tests_failed' if test_rc != 0 else 'coverage_failed')

    # Optional coverage override for special PRs: only applies when tests pass but coverage is below thresholds.
    override_allow = os.environ.get('COVERAGE_OVERRIDE_ALLOW')
    override_reason = os.environ.get('COVERAGE_OVERRIDE_REASON')
    override_used = False

    if status == 'coverage_failed' and test_rc == 0 and override_allow and override_allow.strip() not in ('0', '', 'false', 'False'):
        # 只有在提供了明确的原因时才允许覆盖，否则仍然视为失败
        if override_reason and override_reason.strip():
            override_used = True
            status = 'coverage_overridden'
            summary['override'] = {
                'reason': override_reason.strip(),
                'lines_min': lines_min,
                'branches_min': branches_min,
                'line_pct': coverage.get('line_pct', 0) if coverage else None,
                'branch_pct': coverage.get('branch_pct', 0) if coverage else None,
            }

            # 记录覆盖率豁免日志到 logs/ci/<date>/coverage-override.jsonl，便于后续审计
            override_record = {
                'ts': dt.datetime.utcnow().isoformat(timespec='seconds') + 'Z',
                'status': status,
                'solution': args.solution,
                'configuration': args.configuration,
                'line_pct': coverage.get('line_pct', 0) if coverage else None,
                'branch_pct': coverage.get('branch_pct', 0) if coverage else None,
                'lines_min': lines_min,
                'branches_min': branches_min,
                'override_reason': override_reason.strip(),
                'github_ref': os.environ.get('GITHUB_REF'),
                'github_run_id': os.environ.get('GITHUB_RUN_ID'),
            }
            override_log = os.path.join(ci_dir, 'coverage-override.jsonl')
            with io.open(override_log, 'a', encoding='utf-8') as f:
                f.write(json.dumps(override_record, ensure_ascii=False) + '\n')

    summary['status'] = status
    with io.open(os.path.join(out_dir, 'summary.json'), 'w', encoding='utf-8') as f:
        json.dump(summary, f, ensure_ascii=False, indent=2)

    extra = ""
    if override_used:
        extra = f" OVERRIDE reason={override_reason.strip()}"
    print(
        f"RUN_DOTNET status={summary['status']} line={coverage.get('line_pct', 'n/a') if coverage else 'n/a'}% "
        f"branch={coverage.get('branch_pct','n/a') if coverage else 'n/a'} out={out_dir}{extra}"
    )
    if summary['status'] in ('ok', 'coverage_overridden'):
        return 0
    return 2 if summary['status'] == 'coverage_failed' else 1


if __name__ == '__main__':
    sys.exit(main())
