#!/usr/bin/env python3
"""
CI pipeline driver (Python): dotnet tests+coverage (soft gate), Godot self-check, encoding scan.

Usage (Windows):
  py -3 scripts/python/ci_pipeline.py all \
    --solution Game.sln --configuration Debug \
    --godot-bin "C:\\Godot\\Godot_v4.5.1-stable_mono_win64_console.exe" \
    --build-solutions

Exit codes:
  0  success (or only soft gates failed)
  1  hard failure (dotnet tests failed or self-check failed)
"""
import argparse
import datetime as dt
import io
import json
import os
import shutil
import subprocess
import sys


def run_cmd(args, cwd=None, timeout=900_000):
    p = subprocess.Popen(args, cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                         text=True, encoding='utf-8', errors='ignore')
    try:
        out, _ = p.communicate(timeout=timeout/1000.0)
    except subprocess.TimeoutExpired:
        p.kill()
        out, _ = p.communicate()
        return 124, out
    return p.returncode, out


def read_json(path):
    try:
        with io.open(path, 'r', encoding='utf-8') as f:
            return json.load(f)
    except Exception:
        return None


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest='cmd', required=True)
    ap_all = sub.add_parser('all')
    ap_all.add_argument('--solution', default='Game.sln')
    ap_all.add_argument('--configuration', default='Debug')
    ap_all.add_argument('--godot-bin', required=True)
    ap_all.add_argument('--project', default='project.godot')
    ap_all.add_argument('--build-solutions', action='store_true')

    args = ap.parse_args()
    if args.cmd != 'all':
        print('Unsupported command')
        return 1

    root = os.getcwd()
    date = dt.date.today().strftime('%Y-%m-%d')
    ci_dir = os.path.join('logs', 'ci', date)
    os.makedirs(ci_dir, exist_ok=True)

    summary = {
        'dotnet': {},
        'selfcheck': {},
        'perf_db': {},
        'sql_scan': {},
        'ui_menu_types': {},
        'task_links': {},
        'encoding': {},
        'status': 'ok'
    }
    hard_fail = False

    # 1) Dotnet tests + coverage (hard gate on coverage; ADR-0005)
    rc, out = run_cmd(
        [
            "py",
            "-3",
            "scripts/python/run_dotnet.py",
            "--solution",
            args.solution,
            "--configuration",
            args.configuration,
        ],
        cwd=root,
    )
    # Always persist dotnet stdout/stderr for forensics; workflow must upload logs/unit/** for root cause.
    try:
        dotnet_out_path = os.path.join("logs", "ci", date, "ci-pipeline-dotnet-stdout.txt")
        ensure_dir(os.path.dirname(dotnet_out_path))
        with io.open(dotnet_out_path, "w", encoding="utf-8") as f:
            f.write(out)
    except Exception as ex:
        # Keep CI moving; this is only for diagnostics.
        print(f"CI_PIPELINE WARN: failed to write dotnet stdout log: {type(ex).__name__}")

    if rc != 0 and out.strip():
        # Surface the first evidence to Actions log for faster triage when artifacts are missing/misconfigured.
        print(out.strip())
    dotnet_sum = read_json(os.path.join('logs', 'unit', date, 'summary.json')) or {}
    summary['dotnet'] = {
        'rc': rc,
        'line_pct': (dotnet_sum.get('coverage') or {}).get('line_pct'),
        'branch_pct': (dotnet_sum.get('coverage') or {}).get('branch_pct'),
        'status': dotnet_sum.get('status')
    }
    # run_dotnet.py returns:
    # - 0: ok (or coverage_overridden)
    # - 1: tests failed or other hard failure
    # - 2: coverage failed (tests passed but below thresholds)
    #
    # ADR-0005 requires coverage thresholds as a hard gate by default.
    if rc != 0:
        hard_fail = True

    # 2) Godot self-check (hard gate)
    # ensure autoload fixed (explicit project path)
    _ = run_cmd(['py', '-3', 'scripts/python/godot_selfcheck.py', 'fix-autoload', '--project', args.project], cwd=root)
    sc_args = ['py', '-3', 'scripts/python/godot_selfcheck.py', 'run', '--godot-bin', args.godot_bin, '--project', args.project]
    if args.build_solutions:
        sc_args.append('--build-solutions')
    rc2, out2 = run_cmd(sc_args, cwd=root, timeout=600_000)
    # persist raw stdout for diagnosis
    os.makedirs(os.path.join('logs', 'ci', date), exist_ok=True)
    with io.open(os.path.join('logs', 'ci', date, 'selfcheck-stdout.txt'), 'w', encoding='utf-8') as f:
        f.write(out2)
    sc_sum = read_json(os.path.join('logs', 'e2e', date, 'selfcheck-summary.json')) or {}
    # fallback: parse status from stdout if summary missing
    if not sc_sum:
        import re
        m = re.search(r"SELF_CHECK status=([a-z]+).*? out=([^\r\n]+)", out2)
        if m:
            sc_status = m.group(1)
            sc_out = m.group(2)
            sc_sum = {'status': sc_status, 'out': sc_out, 'note': 'parsed-from-stdout'}
    # as ultimate fallback, trust process rc (0==ok)
    # Copy Godot selfcheck raw console/stderr into ci logs if present
    try:
        e2e_dir = os.path.join('logs', 'e2e', date)
        ci_dir = os.path.join('logs', 'ci', date)
        cons = [p for p in os.listdir(e2e_dir) if p.startswith('godot-selfcheck-console-')]
        if cons:
            cons.sort()
            src = os.path.join(e2e_dir, cons[-1])
            with io.open(src, 'r', encoding='utf-8', errors='ignore') as rf, io.open(os.path.join(ci_dir, 'selfcheck-console.txt'), 'w', encoding='utf-8') as wf:
                wf.write(rf.read())
        errs = [p for p in os.listdir(e2e_dir) if p.startswith('godot-selfcheck-stderr-')]
        if errs:
            errs.sort()
            src = os.path.join(e2e_dir, errs[-1])
            with io.open(src, 'r', encoding='utf-8', errors='ignore') as rf, io.open(os.path.join(ci_dir, 'selfcheck-stderr.txt'), 'w', encoding='utf-8') as wf:
                wf.write(rf.read())
    except Exception:
        pass

    sc_ok = (sc_sum.get('status') == 'ok') or (rc2 == 0)
    summary['selfcheck'] = sc_sum or {'status': 'fail', 'note': 'no-summary'}
    if not sc_ok:
        hard_fail = True

    # 3) SQL static scan (hard gate)
    rc_sql, out_sql = run_cmd(['py', '-3', 'scripts/python/scan_sql_misuse.py', '--fail-on-findings'], cwd=root)
    with io.open(os.path.join('logs', 'ci', date, 'sql-scan-stdout.txt'), 'w', encoding='utf-8') as f:
        f.write(out_sql)
    sql_report = read_json(os.path.join('logs', 'ci', date, 'sql-scan', 'report.json')) or {}
    summary['sql_scan'] = {
        'rc': rc_sql,
        'status': sql_report.get('status') or ('ok' if rc_sql == 0 else 'fail'),
        'findings': sql_report.get('findings_count'),
    }
    if rc_sql != 0:
        hard_fail = True

    # 4) DB perf smoke (hard gate: prevents "missing perf data" regressions)
    rc_perf, out_perf = run_cmd(
        ['py', '-3', 'scripts/python/perf_smoke_db.py', '--godot-bin', args.godot_bin],
        cwd=root,
        timeout=900_000,
    )
    with io.open(os.path.join('logs', 'ci', date, 'perf-db-stdout.txt'), 'w', encoding='utf-8') as f:
        f.write(out_perf)
    perf_sum = read_json(os.path.join('logs', 'perf', date, 'db', 'run.json')) or {}
    # Copy perf artifacts into logs/ci for easier artifact upload/debugging.
    try:
        perf_dir = os.path.join('logs', 'perf', date, 'db')
        ci_dir2 = os.path.join('logs', 'ci', date)
        for name in ('run.json', 'db-perf-summary.json', 'dotnet-build.log', 'gdunit.log'):
            src = os.path.join(perf_dir, name)
            if os.path.isfile(src):
                shutil.copy2(src, os.path.join(ci_dir2, f'perf-db-{name}'))
        # Copy the most useful GdUnit console log if present.
        gdunit_console = os.path.join(perf_dir, 'gdunit-reports', 'gdunit-console.txt')
        if os.path.isfile(gdunit_console):
            shutil.copy2(gdunit_console, os.path.join(ci_dir2, 'perf-db-gdunit-console.txt'))
    except Exception:
        pass
    summary['perf_db'] = {
        'rc': rc_perf,
        'status': perf_sum.get('status') or ('ok' if rc_perf == 0 else 'fail'),
        'out': perf_sum.get('summary_json') or os.path.join('logs', 'perf', date, 'db'),
    }
    if rc_perf != 0:
        hard_fail = True

    # 5) Encoding scan (soft gate)
    rc3, out3 = run_cmd(['py', '-3', 'scripts/python/check_encoding.py', '--since-today'], cwd=root)
    enc_sum = read_json(os.path.join('logs', 'ci', date, 'encoding', 'session-summary.json')) or {}
    summary['encoding'] = enc_sum

    # 6) Content assets validation (hard gate)
    rc_content, out_content = run_cmd(['py', '-3', 'scripts/python/validate_content_assets.py'], cwd=root)
    with io.open(os.path.join('logs', 'ci', date, 'content-validation-stdout.txt'), 'w', encoding='utf-8') as f:
        f.write(out_content)
    content_report = read_json(os.path.join('logs', 'ci', date, 'content-validation', 'report.json')) or {}
    summary['content_validation'] = {
        'rc': rc_content,
        'status': content_report.get('status') or ('ok' if rc_content == 0 else 'fail'),
        'errors': content_report.get('errors'),
    }
    if rc_content != 0:
        hard_fail = True

    # 7) UI menu event types generation + sync (hard gate)
    rc_ui_gen, out_ui_gen = run_cmd(['py', '-3', 'scripts/python/generate_ui_menu_event_types.py'], cwd=root)
    with io.open(os.path.join('logs', 'ci', date, 'ui-menu-types-generate-stdout.txt'), 'w', encoding='utf-8') as f:
        f.write(out_ui_gen)
    if rc_ui_gen != 0:
        hard_fail = True

    rc_ui, out_ui = run_cmd(['py', '-3', 'scripts/python/validate_ui_menu_event_types.py'], cwd=root)
    with io.open(os.path.join('logs', 'ci', date, 'ui-menu-types-stdout.txt'), 'w', encoding='utf-8') as f:
        f.write(out_ui)
    ui_report = read_json(os.path.join('logs', 'ci', date, 'ui-menu-event-types', 'report.json')) or {}
    summary['ui_menu_types'] = {
        'rc': rc_ui if rc_ui_gen == 0 else 1,
        'status': 'ok' if rc_ui == 0 else 'fail',
        'out': os.path.join('logs', 'ci', date, 'ui-menu-event-types', 'report.json'),
        'errors': ui_report.get('errors'),
        'generated': rc_ui_gen == 0,
    }
    if rc_ui != 0:
        hard_fail = True

    # 8) Task links / view semantics (hard gate)
    rc_links, out_links = run_cmd(['py', '-3', 'scripts/python/task_links_validate.py'], cwd=root)
    with io.open(os.path.join('logs', 'ci', date, 'task-links-stdout.txt'), 'w', encoding='utf-8') as f:
        f.write(out_links)
    summary['task_links'] = {
        'rc': rc_links,
        'status': 'ok' if rc_links == 0 else 'fail',
        'out': os.path.join('logs', 'ci', date, 'task-links-stdout.txt'),
    }
    if rc_links != 0:
        hard_fail = True

    summary['status'] = 'ok' if not hard_fail else 'fail'
    with io.open(os.path.join(ci_dir, 'ci-pipeline-summary.json'), 'w', encoding='utf-8') as f:
        json.dump(summary, f, ensure_ascii=False, indent=2)

    print(
        f"CI_PIPELINE status={summary['status']} "
        f"dotnet={summary['dotnet'].get('status')} "
        f"selfcheck={summary['selfcheck'].get('status')} "
        f"sql_scan={summary['sql_scan'].get('status')} "
        f"perf_db={summary['perf_db'].get('status')} "
        f"content={summary.get('content_validation', {}).get('status')} "
        f"ui_menu={summary.get('ui_menu_types', {}).get('status')} "
        f"task_links={summary.get('task_links', {}).get('status')} "
        f"encoding_bad={summary['encoding'].get('bad', 'n/a')}"
    )
    return 0 if not hard_fail else 1


if __name__ == '__main__':
    sys.exit(main())
