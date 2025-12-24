#!/usr/bin/env python3
"""
Run GdUnit4 tests headless and archive reports to logs/e2e/<date>/.

Usage:
  py -3 scripts/python/run_gdunit.py \
    --godot-bin "C:\\Godot\\Godot_v4.5.1-stable_mono_win64_console.exe" \
    --project Tests.Godot \
    --add tests/Adapters --add tests/OtherSuite \
    --timeout-sec 300
"""
import argparse
import datetime as dt
import os
import shutil
import subprocess
import json
import time
from pathlib import Path

from godot_cli import build_userdir_args, default_user_dir


def run_cmd(args, cwd=None, timeout=600_000, env=None):
    p = subprocess.Popen(args, cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                         text=True, encoding='utf-8', errors='ignore', env=env)
    try:
        out, _ = p.communicate(timeout=timeout/1000.0)
    except subprocess.TimeoutExpired:
        p.kill()
        out, _ = p.communicate()
        return 124, out
    return p.returncode, out


def run_cmd_failfast(args, cwd=None, timeout=600_000, break_markers=None, env=None):
    """Run a process and stream stdout; if any line contains a break marker, kill early.

    In Godot headless/script mode, a Debugger Break (for example GdUnit4 failing
    to preload a script and printing `Debugger Break, Reason: 'Parser Error: ...'`)
    will block waiting for interactive input and never exit by itself.

    To avoid long CI timeouts we treat the following patterns as hard failures
    and terminate the process early:
    - SCRIPT ERROR
    - Debugger Break
    - Parser Error:
    """
    break_markers = break_markers or [
        'SCRIPT ERROR',
        'Debugger Break',
        'Parser Error:',
    ]
    p = subprocess.Popen(args, cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                         text=True, encoding='utf-8', errors='ignore', env=env)
    buf_lines = []
    hit_break = False
    try:
        # Poll line-by-line up to timeout
        end_ts = dt.datetime.now().timestamp() + (timeout/1000.0)
        while True:
            line = p.stdout.readline()
            if line:
                buf_lines.append(line)
                low = line.lower()
                if any(m.lower() in low for m in break_markers):
                    hit_break = True
                    p.kill()
                    break
            else:
                if p.poll() is not None:
                    break
            if dt.datetime.now().timestamp() > end_ts:
                p.kill()
                return 124, ''.join(buf_lines)
        out = ''.join(buf_lines)
        if hit_break:
            return 1, out
        return (p.returncode or 0), out
    except Exception:
        try:
            p.kill()
        except Exception:
            pass
        return 1, ''.join(buf_lines)


def write_text(path: str, content: str) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'w', encoding='utf-8') as f:
        f.write(content)


def read_project_name(project_dir: Path) -> str | None:
    project_godot = project_dir / "project.godot"
    if not project_godot.is_file():
        return None
    try:
        for raw in project_godot.read_text(encoding="utf-8", errors="ignore").splitlines():
            line = raw.strip()
            if line.startswith("config/name=") and "\"" in line:
                return line.split("\"", 2)[1]
    except Exception:
        return None
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--godot-bin', required=True)
    ap.add_argument('--project', default='Tests.Godot')
    ap.add_argument('--add', action='append', default=[], help='Add directory or suite path(s). E.g., tests/Adapters or res://tests/Adapters')
    ap.add_argument('--timeout-sec', type=int, default=600, help='Timeout seconds for test run (default 600)')
    ap.add_argument('--prewarm', action='store_true', help='Prewarm: build solutions before running tests')
    ap.add_argument('--rd', dest='report_dir', default=None, help='Custom destination to copy reports into (defaults to logs/e2e/<date>/gdunit-reports)')
    ap.add_argument('--user-dir', default=None, help='Redirect Godot user:// to this directory (default: logs/_godot_userdir/<project>)')
    ap.add_argument('--userdir-flag', default=os.environ.get('GODOT_USERDIR_FLAG', 'auto'),
                    help='Godot CLI flag for user dir (auto|--user-dir|--user-data-dir); env: GODOT_USERDIR_FLAG')
    ap.add_argument('--no-userdir', action='store_true', help='Disable user dir redirection (writes to default OS location)')
    ap.add_argument('--skip-userlogs', action='store_true', help='Skip archiving/pruning Godot user:// logs (not recommended)')
    ap.add_argument('--userlog-retention-days', type=int, default=int(os.environ.get('GODOT_USERLOG_RETENTION_DAYS', '7')))
    ap.add_argument('--userlog-max-file-mb', type=int, default=int(os.environ.get('GODOT_USERLOG_MAX_FILE_MB', '256')))
    ap.add_argument('--userlog-tail-mb', type=int, default=int(os.environ.get('GODOT_USERLOG_TAIL_MB', '4')))
    ap.add_argument('--userlog-max-full-copy-mb', type=int, default=int(os.environ.get('GODOT_USERLOG_MAX_FULL_COPY_MB', '16')))
    args = ap.parse_args()

    root = os.getcwd()
    proj = os.path.abspath(args.project)
    date = dt.date.today().strftime('%Y-%m-%d')
    out_dir = os.path.join(root, 'logs', 'e2e', date)
    os.makedirs(out_dir, exist_ok=True)

    # Redirect Godot user:// to a repo-local directory (default under logs/).
    user_dir = None
    userdir_flag_used = None
    userdir_args = []
    if not args.no_userdir:
        user_dir = args.user_dir or default_user_dir(proj, root_dir=root)
        try:
            os.makedirs(user_dir, exist_ok=True)
        except Exception:
            # Best-effort; if it fails, we will run without userdir args.
            user_dir = None
        userdir_args, userdir_flag_used = build_userdir_args(args.godot_bin, user_dir, preferred_flag=args.userdir_flag)

    # Godot 4.5 editor builds do not expose a CLI flag to redirect user data.
    # In sandboxed environments, writes to %APPDATA% can be blocked. As a fallback,
    # override APPDATA for the child process to keep user:// writes under the repo.
    env = os.environ.copy()
    appdata_override = None
    project_name = read_project_name(Path(proj)) or Path(proj).name
    if user_dir and userdir_flag_used is None:
        try:
            appdata_override = str((Path(user_dir).resolve() / "_appdata").resolve())
            Path(appdata_override).mkdir(parents=True, exist_ok=True)
            env["APPDATA"] = appdata_override
            # Ensure user://logs exists to avoid engine startup crashes when file logging is enabled.
            (Path(appdata_override) / "Godot" / "app_userdata" / project_name / "logs").mkdir(parents=True, exist_ok=True)
        except Exception:
            appdata_override = None

    # Optional prewarm with fallback
    prewarm_rc = None
    prewarm_note = None
    if args.prewarm:
        pre_cmd = [
            args.godot_bin,
            *userdir_args,
            '--headless',
            '--path',
            proj,
            '--log-file',
            str((Path(out_dir) / 'prewarm-godot.log').resolve()),
            '--build-solutions',
            '--quit',
        ]
        _rcp, _outp = run_cmd(pre_cmd, cwd=proj, timeout=300_000, env=env)
        prewarm_attempts = 1
        prewarm_rc = _rcp
        # Write first attempt
        write_text(os.path.join(out_dir, 'prewarm-godot.txt'), _outp)
        if _rcp != 0:
            # Wait and retry once to mitigate transient C# load issues
            time.sleep(3)
            _rcp2, _outp2 = run_cmd(pre_cmd, cwd=proj, timeout=360_000, env=env)
            prewarm_attempts = 2
            prewarm_rc = _rcp2
            # Append retry log to same file
            try:
                with open(os.path.join(out_dir, 'prewarm-godot.txt'), 'a', encoding='utf-8') as f:
                    f.write("\n=== retry rc=%d ===\n" % _rcp2)
                    f.write(_outp2)
            except Exception:
                pass
            if _rcp2 == 0:
                prewarm_note = 'retry-ok'
            else:
                # Fallback to dotnet build to avoid editor plugin failures
                dotnet_projects = []
                tests_csproj = os.path.join(proj, 'Tests.Godot.csproj')
                if os.path.isfile(tests_csproj):
                    dotnet_projects.append(tests_csproj)
                # Also try solution at repo root if present
                sln = os.path.join(root, 'GodotGame.sln')
                # Prefer project build; if solution exists, add as secondary
                build_logs = []
                for item in (dotnet_projects or [sln] if os.path.isfile(sln) else []):
                    rc_b, out_b = run_cmd(['dotnet', 'build', item, '-c', 'Debug', '-v', 'minimal'], cwd=root, timeout=600_000)
                    build_logs.append((item, rc_b, out_b))
                # Persist build logs
                agg = []
                for item, rc_b, out_b in build_logs:
                    agg.append(f'=== {item} rc={rc_b} ===\n{out_b}\n')
                write_text(os.path.join(out_dir, 'prewarm-dotnet.txt'), '\n'.join(agg) if agg else 'NO_DOTNET_BUILD_TARGETS')
                prewarm_note = 'fallback-dotnet'

    # Run tests (Debugger Break fail-fast)
    # Build command with optional -a filters
    cmd = [
        args.godot_bin,
        *userdir_args,
        '--headless',
        '--path',
        proj,
        '--log-file',
        str((Path(out_dir) / 'gdunit-godot.log').resolve()),
        '-s',
        '-d',
        'res://addons/gdUnit4/bin/GdUnitCmdTool.gd',
        '--ignoreHeadlessMode',
    ]
    for a in args.add:
        apath = a
        if not apath.startswith('res://'):
            # normalize relative tests path to res://
            apath = 'res://' + apath.replace('\\', '/').lstrip('/')
        cmd += ['-a', apath]
    rc, out = run_cmd_failfast(cmd, cwd=proj, timeout=args.timeout_sec*1000, env=env)
    console_path = os.path.join(out_dir, 'gdunit-console.txt')
    with open(console_path, 'w', encoding='utf-8') as f:
        f.write(out)

    # Generate HTML log frame (optional)
    _rc2, _out2 = run_cmd(
        [
            args.godot_bin,
            *userdir_args,
            '--headless',
            '--path',
            proj,
            '--log-file',
            str((Path(out_dir) / 'gdunit-copylog-godot.log').resolve()),
            '--quiet',
            '-s',
            'res://addons/gdUnit4/bin/GdUnitCopyLog.gd',
        ],
        cwd=proj,
        env=env,
    )

    # Archive reports
    reports_dir = os.path.join(proj, 'reports')
    dest = args.report_dir if args.report_dir else os.path.join(out_dir, 'gdunit-reports')
    # Always create a destination folder with at least the console log and a summary
    if os.path.isdir(dest):
        shutil.rmtree(dest, ignore_errors=True)
    os.makedirs(dest, exist_ok=True)
    # Copy console log for diagnosis
    try:
        shutil.copy2(console_path, os.path.join(dest, 'gdunit-console.txt'))
    except Exception:
        pass
    # Copy reports if they exist
    if os.path.isdir(reports_dir):
        for name in os.listdir(reports_dir):
            src = os.path.join(reports_dir, name)
            dst = os.path.join(dest, name)
            if os.path.isdir(src):
                shutil.copytree(src, dst, dirs_exist_ok=True)
            else:
                shutil.copy2(src, dst)
    # Write a small summary json for CI
    summary = {
        'rc': rc,
        'project': proj,
        'added': args.add,
        'timeout_sec': args.timeout_sec,
        'user_dir': user_dir,
        'userdir_flag_used': userdir_flag_used,
        'appdata_override': appdata_override,
    }
    if prewarm_rc is not None:
        summary['prewarm_rc'] = prewarm_rc
        if prewarm_note:
            summary['prewarm_note'] = prewarm_note
        try:
            summary['prewarm_attempts'] = prewarm_attempts
        except NameError:
            pass
    try:
        with open(os.path.join(dest, 'run-summary.json'), 'w', encoding='utf-8') as f:
            json.dump(summary, f, ensure_ascii=False)
    except Exception:
        pass

    # Archive + prune Godot user:// logs (Windows: %APPDATA%/Godot/app_userdata/<ProjectName>/logs).
    # This prevents uncontrolled growth of godot.log files in AppData.
    if not args.skip_userlogs:
        try:
            from godot_userlog_manager import archive_and_prune_user_logs, UserLogPolicy

            userlogs_dest = Path(dest) / 'godot-userlogs'
            source_logs_dir = None
            if userdir_flag_used and user_dir:
                # When userdir redirection is active, Godot logs are expected under <user_dir>/logs.
                source_logs_dir = (Path(user_dir).resolve() / 'logs')
            elif appdata_override:
                # When APPDATA is overridden (sandbox fallback), Godot logs live under:
                # <APPDATA>/Godot/app_userdata/<ProjectName>/logs
                logs_dir = Path(appdata_override) / 'Godot' / 'app_userdata' / project_name / 'logs'
                if logs_dir.exists():
                    source_logs_dir = logs_dir
            userlogs_summary = archive_and_prune_user_logs(
                project_dir=Path(proj),
                dest_dir=userlogs_dest,
                policy=UserLogPolicy(
                    retention_days=max(0, args.userlog_retention_days),
                    max_file_bytes=max(0, args.userlog_max_file_mb) * 1024 * 1024,
                    tail_bytes=max(0, args.userlog_tail_mb) * 1024 * 1024,
                    max_full_copy_bytes=max(0, args.userlog_max_full_copy_mb) * 1024 * 1024,
                ),
                dry_run=False,
                source_logs_dir=source_logs_dir,
            )
            try:
                (userlogs_dest / 'userlogs-summary.json').write_text(
                    json.dumps(userlogs_summary, ensure_ascii=False, indent=2),
                    encoding='utf-8',
                )
            except Exception:
                pass
        except Exception as e:
            write_text(os.path.join(dest, 'godot-userlogs-error.txt'), str(e))
    print(f'GDUNIT_DONE rc={rc} out={out_dir}')
    return 0 if rc == 0 else rc


if __name__ == '__main__':
    raise SystemExit(main())
