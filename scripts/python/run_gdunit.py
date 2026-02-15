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
import re
import shutil
import subprocess
import json
import time
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

from godot_cli import build_userdir_args, default_user_dir
from playability_artifacts import (
    DEFAULT_ROUTE2_ARTIFACT_NAME,
    DEFAULT_ROUTE2_TEST_MARKER,
    collect_playability_artifacts,
    decide_route2_requirement,
    detect_route2_execution,
    is_route2_suite_requested,
    resolve_route2_artifact_requirement,
)


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


def tail_text(path: str, max_chars: int = 12_000) -> str:
    try:
        p = Path(path)
        if not p.is_file():
            return f"(missing) {path}"
        txt = p.read_text(encoding='utf-8', errors='ignore')
        if len(txt) <= max_chars:
            return txt
        return txt[-max_chars:]
    except Exception as e:
        return f"(failed to read) {path}: {e}"


def run_cmd_failfast(args, cwd=None, timeout=600_000, break_markers=None, env=None):
    """Run a process and stream stdout; kill early only for known hang conditions.

    In Godot headless/script mode, a Debugger Break (for example GdUnit4 failing
    to preload a script and printing `Debugger Break, Reason: 'Parser Error: ...'`)
    will block waiting for interactive input and never exit by itself.

    To avoid long CI timeouts we terminate early only for known hang patterns:
    - Debugger Break
    - Parser Error:

    For regular `SCRIPT ERROR` we keep streaming output so the full backtrace
    can be captured in CI logs (it usually exits by itself).
    """
    kill_markers = break_markers or [
        'Debugger Break',
        'Parser Error:',
    ]
    failure_markers = [
        'SCRIPT ERROR',
    ]
    p = subprocess.Popen(args, cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                         text=True, encoding='utf-8', errors='ignore', env=env)
    buf_lines = []
    hit_kill = False
    hit_failure = False
    try:
        # Poll line-by-line up to timeout
        end_ts = dt.datetime.now().timestamp() + (timeout/1000.0)
        while True:
            line = p.stdout.readline()
            if line:
                buf_lines.append(line)
                low = line.lower()
                if any(m.lower() in low for m in failure_markers):
                    hit_failure = True
                if any(m.lower() in low for m in kill_markers):
                    hit_kill = True
                    p.kill()
                    break
            else:
                if p.poll() is not None:
                    break
            if dt.datetime.now().timestamp() > end_ts:
                p.kill()
                return 124, ''.join(buf_lines)
        out = ''.join(buf_lines)
        if hit_kill:
            return 1, out
        rc = p.returncode or 0
        if rc == 0 and hit_failure:
            rc = 1
        return rc, out
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


def _safe_rmtree(path: str) -> None:
    try:
        shutil.rmtree(path, ignore_errors=True)
    except Exception:
        pass


def _list_failed_testcases_from_junit_xml(xml_path: Path) -> list[dict]:
    failures: list[dict] = []
    try:
        root = ET.parse(xml_path).getroot()
    except Exception:
        return failures

    for tc in root.iter("testcase"):
        failure = tc.find("failure")
        error = tc.find("error")
        if failure is None and error is None:
            continue
        node = failure if failure is not None else error
        failures.append(
            {
                "classname": tc.attrib.get("classname") or "",
                "name": tc.attrib.get("name") or "",
                "kind": "failure" if failure is not None else "error",
                "message": (node.attrib.get("message") if node is not None else None) or "",
                "xml": str(xml_path).replace("\\", "/"),
            }
        )
    return failures


def _collect_failed_testcases_from_reports(dest_dir: str) -> list[dict]:
    dest = Path(dest_dir)
    if not dest.is_dir():
        return []
    failures: list[dict] = []
    for xml_path in sorted(dest.glob("**/results.xml")):
        failures.extend(_list_failed_testcases_from_junit_xml(xml_path))
    return failures


def _print_failure_summary(console_path: str, dest_dir: str, out_dir: str) -> None:
    print("GDUNIT_FAILURE_SUMMARY begin")
    print(f"console_path={console_path}")
    print(f"reports_dest={dest_dir}")
    print(f"out_dir={out_dir}")
    print("---- gdunit-console tail ----")
    print(tail_text(console_path, max_chars=20_000))

    failures = _collect_failed_testcases_from_reports(dest_dir)
    if failures:
        print("---- junit failures ----")
        # Print a bounded list to keep CI logs readable
        max_items = 50
        for i, f in enumerate(failures[:max_items], start=1):
            cls = f.get("classname", "")
            name = f.get("name", "")
            kind = f.get("kind", "")
            msg = f.get("message", "")
            xml = f.get("xml", "")
            print(f"{i:02d}. {kind} {cls}::{name}")
            if msg:
                print(f"    message={msg}")
            print(f"    results={xml}")
        if len(failures) > max_items:
            print(f"... truncated ({len(failures)} total failing testcases)")
    else:
        print("---- junit failures ----")
        print("(none found under reports dest; check gdunit-console and godot logs)")

    godot_log = Path(out_dir) / "gdunit-godot.log"
    if godot_log.is_file():
        print("---- gdunit-godot.log tail ----")
        print(tail_text(str(godot_log), max_chars=20_000))

    print("GDUNIT_FAILURE_SUMMARY end")


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


def repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


def ensure_test_runtime_mount(project: str, runtime_dir: str = "Game.Godot") -> tuple[int, str]:
    """Ensure Tests.Godot/<runtime_dir> is a junction to repo_root/<runtime_dir>.

    This prevents drift between test runtime scripts/resources and the real runtime folder.
    We also mount Game.Core into Tests.Godot so GdUnit tests can reference res://Game.Core/**.
    """
    root = repo_root()
    proj = (root / project).resolve()
    runtime = (root / runtime_dir).resolve()
    core = (root / "Game.Core").resolve()
    if not proj.is_dir() or not runtime.is_dir():
        return 0, "SKIP_PREPARE: project/runtime not found\n"
    if not core.is_dir():
        return 0, "SKIP_PREPARE: Game.Core not found\n"

    outs = []
    for rt in (runtime_dir, "Game.Core"):
        cmd = [
            sys.executable,
            str(root / "scripts" / "python" / "prepare_gd_tests.py"),
            "--project",
            project,
            "--runtime",
            rt,
        ]
        rc, out = run_cmd(cmd, cwd=str(root), timeout=60_000, env=os.environ.copy())
        outs.append(out)
        if rc != 0:
            return rc, "".join(outs)
    return 0, "".join(outs)


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
    ap.add_argument('--no-prepare-runtime', action='store_true', help='Disable preparing Tests.Godot runtime mount (not recommended)')
    ap.add_argument('--skip-userlogs', action='store_true', help='Skip archiving/pruning Godot user:// logs (not recommended)')
    ap.add_argument('--userlog-retention-days', type=int, default=int(os.environ.get('GODOT_USERLOG_RETENTION_DAYS', '7')))
    ap.add_argument('--userlog-max-file-mb', type=int, default=int(os.environ.get('GODOT_USERLOG_MAX_FILE_MB', '256')))
    ap.add_argument('--userlog-tail-mb', type=int, default=int(os.environ.get('GODOT_USERLOG_TAIL_MB', '4')))
    ap.add_argument('--userlog-max-full-copy-mb', type=int, default=int(os.environ.get('GODOT_USERLOG_MAX_FULL_COPY_MB', '16')))
    ap.add_argument(
        '--require-route2-artifact',
        default=os.environ.get('SC_REQUIRE_ROUTE2_ARTIFACT', 'auto'),
        help='Route2 artifact requirement mode: auto|always|never (also supports true/false, 1/0). env: SC_REQUIRE_ROUTE2_ARTIFACT',
    )
    args = ap.parse_args()

    artifact_names_raw = os.environ.get(
        'SC_PLAYABILITY_ARTIFACTS',
        '',
    )
    artifact_name_pattern = re.compile(r'^[A-Za-z0-9._-]+\.json$')
    artifact_name_errors: list[str] = []
    artifact_names: list[str] = []
    for raw_value in artifact_names_raw.split(','):
        artifact_name = raw_value.strip()
        if not artifact_name:
            continue
        if (not artifact_name_pattern.fullmatch(artifact_name)) or ('..' in artifact_name) or ('/' in artifact_name) or ('\\' in artifact_name):
            artifact_name_errors.append(artifact_name)
            continue
        artifact_names.append(artifact_name)

    try:
        route2_requirement_mode, route2_requirement_base_reason = resolve_route2_artifact_requirement(args.require_route2_artifact)
    except ValueError as ex:
        print(f'GDUNIT_CONFIG_ERROR {ex}')
        return 2

    route2_artifact_name = os.environ.get('SC_ROUTE2_ARTIFACT_NAME', DEFAULT_ROUTE2_ARTIFACT_NAME).strip()
    if not route2_artifact_name:
        route2_artifact_name = DEFAULT_ROUTE2_ARTIFACT_NAME
    if (not artifact_name_pattern.fullmatch(route2_artifact_name)) or ('..' in route2_artifact_name) or ('/' in route2_artifact_name) or ('\\' in route2_artifact_name):
        print('GDUNIT_CONFIG_ERROR invalid SC_ROUTE2_ARTIFACT_NAME')
        return 2

    route2_test_marker = os.environ.get('SC_ROUTE2_TEST_MARKER', DEFAULT_ROUTE2_TEST_MARKER).strip().lower()
    if not route2_test_marker:
        route2_test_marker = DEFAULT_ROUTE2_TEST_MARKER

    try:
        artifact_max_mb = int(os.environ.get('SC_PLAYABILITY_ARTIFACT_MAX_MB', '2'))
    except ValueError:
        print('GDUNIT_CONFIG_ERROR invalid SC_PLAYABILITY_ARTIFACT_MAX_MB, expected integer')
        return 2
    artifact_max_mb = max(0, artifact_max_mb)
    artifact_max_bytes = artifact_max_mb * 1024 * 1024

    try:
        route2_detect_max_xml_files = int(os.environ.get('SC_ROUTE2_DETECT_MAX_XML_FILES', '200'))
        route2_detect_max_xml_mb = int(os.environ.get('SC_ROUTE2_DETECT_MAX_XML_MB', '2'))
        route2_detect_max_console_kb = int(os.environ.get('SC_ROUTE2_DETECT_MAX_CONSOLE_KB', '512'))
    except ValueError:
        print('GDUNIT_CONFIG_ERROR invalid route2 detection limits, expected integers')
        return 2

    route2_detect_max_xml_files = max(1, route2_detect_max_xml_files)
    route2_detect_max_xml_bytes = max(1, route2_detect_max_xml_mb) * 1024 * 1024
    route2_detect_max_console_bytes = max(1, route2_detect_max_console_kb) * 1024

    if not args.no_prepare_runtime:
        prc, pout = ensure_test_runtime_mount(args.project, runtime_dir="Game.Godot")
        if pout:
            print(pout.rstrip())
        if prc != 0:
            print(f"GDUNIT_PREPARE status=fail rc={prc}")
            return prc

    root = os.getcwd()
    proj = os.path.abspath(args.project)
    date = dt.date.today().strftime('%Y-%m-%d')
    out_dir = os.path.join(root, 'logs', 'e2e', date)
    os.makedirs(out_dir, exist_ok=True)

    # GdUnit4 writes reports to res://reports by default (i.e., <project>/reports).
    # In clean CI checkouts this folder may not exist and can cause GdUnit4 to crash
    # with "Cannot call method 'seek' on a null value." when opening report files.
    try:
        # Ensure a clean report directory per run to avoid mixing old failures into diagnosis.
        _safe_rmtree(os.path.join(proj, "reports"))
        os.makedirs(os.path.join(proj, "reports"), exist_ok=True)
    except Exception:
        # Best-effort; do not fail the run just because report pre-creation failed.
        pass

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
    env['SC_ARTIFACT_DATE'] = date
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

    artifact_source_roots: list[Path] = []
    artifact_source_root_labels: list[str] = []
    if userdir_flag_used and user_dir:
        artifact_source_roots.append(Path(user_dir).resolve())
        artifact_source_root_labels.append('userdir')
    if appdata_override:
        artifact_source_roots.append((Path(appdata_override) / 'Godot' / 'app_userdata' / project_name).resolve())
        artifact_source_root_labels.append('appdata_override')

    if not artifact_source_roots:
        appdata_default = env.get('APPDATA') or os.environ.get('APPDATA')
        if appdata_default:
            artifact_source_roots.append((Path(appdata_default) / 'Godot' / 'app_userdata' / project_name).resolve())
            artifact_source_root_labels.append('appdata_default')

    # Optional prewarm with fallback
    prewarm_rc = None
    prewarm_note = None
    prewarm_fallback_dotnet_ok = None
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
                # Prefer project build; otherwise fall back to solution if present
                dotnet_targets = []
                if dotnet_projects:
                    dotnet_targets = dotnet_projects
                elif os.path.isfile(sln):
                    dotnet_targets = [sln]
                build_logs = []
                for item in dotnet_targets:
                    rc_b, out_b = run_cmd(['dotnet', 'build', item, '-c', 'Debug', '-v', 'minimal'], cwd=root, timeout=600_000)
                    build_logs.append((item, rc_b, out_b))
                prewarm_fallback_dotnet_ok = (len(build_logs) > 0) and all(rc_b == 0 for _, rc_b, _ in build_logs)
                # Persist build logs
                agg = []
                for item, rc_b, out_b in build_logs:
                    agg.append(f'=== {item} rc={rc_b} ===\n{out_b}\n')
                write_text(os.path.join(out_dir, 'prewarm-dotnet.txt'), '\n'.join(agg) if agg else 'NO_DOTNET_BUILD_TARGETS')
                prewarm_note = 'fallback-dotnet'

    # Prewarm-only mode: if no suites were provided, do not run the full GdUnit suite.
    # This is used by CI to warm up/build C# solutions and validate the project starts.
    if args.prewarm and not args.add:
        rc = prewarm_rc or 0
        if prewarm_note == 'fallback-dotnet' and prewarm_fallback_dotnet_ok is True:
            rc = 0
        # Write a small summary json for CI to archive.
        dest = args.report_dir if args.report_dir else os.path.join(out_dir, 'gdunit-reports')
        if os.path.isdir(dest):
            shutil.rmtree(dest, ignore_errors=True)
        os.makedirs(dest, exist_ok=True)
        summary = {
            'rc': rc,
            'project': proj,
            'added': args.add,
            'timeout_sec': args.timeout_sec,
            'user_dir': user_dir,
            'userdir_flag_used': userdir_flag_used,
            'appdata_override': appdata_override,
            'prewarm_rc': prewarm_rc,
            'prewarm_note': prewarm_note,
            'prewarm_fallback_dotnet_ok': prewarm_fallback_dotnet_ok,
        }
        try:
            with open(os.path.join(dest, 'run-summary.json'), 'w', encoding='utf-8') as f:
                json.dump(summary, f, ensure_ascii=False)
        except Exception:
            pass
        if rc != 0:
            # Print diagnostics into stdout so CI logs show the real root cause.
            prewarm_txt = os.path.join(out_dir, 'prewarm-godot.txt')
            prewarm_log = os.path.join(out_dir, 'prewarm-godot.log')
            prewarm_dotnet = os.path.join(out_dir, 'prewarm-dotnet.txt')
            print('GDUNIT_PREWARM status=fail')
            print(f'  prewarm_rc={prewarm_rc} prewarm_note={prewarm_note} prewarm_fallback_dotnet_ok={prewarm_fallback_dotnet_ok}')
            print(f'  out_dir={out_dir}')
            print(f'  prewarm_txt={prewarm_txt}')
            print(f'  prewarm_log={prewarm_log}')
            if os.path.isfile(prewarm_dotnet):
                print(f'  prewarm_dotnet={prewarm_dotnet}')
            print('--- prewarm-godot.txt (tail) ---')
            print(tail_text(prewarm_txt))
            print('--- prewarm-godot.log (tail) ---')
            print(tail_text(prewarm_log))
            if os.path.isfile(prewarm_dotnet):
                print('--- prewarm-dotnet.txt (tail) ---')
                print(tail_text(prewarm_dotnet))
        print(f'GDUNIT_DONE rc={rc} out={out_dir}')
        return 0 if rc == 0 else rc

    # Run tests (Debugger Break fail-fast)
    run_started_at_utc = dt.datetime.now(dt.timezone.utc)

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

    route2_test_executed, route2_detection_error = detect_route2_execution(
        dest,
        console_path,
        route2_test_marker,
        run_started_at_utc=run_started_at_utc,
        max_xml_files=route2_detect_max_xml_files,
        max_xml_bytes=route2_detect_max_xml_bytes,
        max_console_bytes=route2_detect_max_console_bytes,
    )
    route2_suite_requested = is_route2_suite_requested(args.add, route2_test_marker)
    require_route2_artifact, route2_requirement_reason, artifact_warning_messages, artifact_error_messages = decide_route2_requirement(
        route2_requirement_mode=route2_requirement_mode,
        route2_requirement_base_reason=route2_requirement_base_reason,
        route2_test_executed=route2_test_executed,
        route2_detection_error=route2_detection_error,
        route2_suite_requested=route2_suite_requested,
    )

    artifact_error_path = os.path.join(dest, 'playability-artifacts-error.txt')
    if artifact_error_messages:
        rc = 1

    try:
        artifact_payload, artifact_collection_errors, artifact_failed = collect_playability_artifacts(
            artifact_names=artifact_names,
            artifact_source_roots=artifact_source_roots,
            artifact_source_root_labels=artifact_source_root_labels,
            date=date,
            run_started_at_utc=run_started_at_utc,
            out_dir=Path(out_dir),
            repo_root=Path(root),
            artifact_name_errors=artifact_name_errors,
            require_route2_artifact=require_route2_artifact,
            route2_artifact_name=route2_artifact_name,
            route2_requirement_mode=route2_requirement_mode,
            route2_requirement_reason=route2_requirement_reason,
            route2_test_executed=route2_test_executed,
            artifact_max_mb=artifact_max_mb,
            artifact_max_bytes=artifact_max_bytes,
        )
        if artifact_failed:
            rc = 1
        artifact_error_messages.extend(artifact_collection_errors)
        if artifact_warning_messages:
            artifact_payload['warnings'] = artifact_warning_messages
        artifact_payload['route2_suite_requested'] = route2_suite_requested
        artifact_payload['route2_detection_limits'] = {
            'max_xml_files': route2_detect_max_xml_files,
            'max_xml_bytes': route2_detect_max_xml_bytes,
            'max_console_bytes': route2_detect_max_console_bytes,
        }
        write_text(os.path.join(dest, 'playability-artifacts.json'), json.dumps(artifact_payload, ensure_ascii=False, indent=2))
    except Exception as e:
        rc = 1
        artifact_error_messages.append(f'artifact collection failed: {e}')

    if artifact_error_messages:
        write_text(artifact_error_path, '\n\n'.join(msg.rstrip() for msg in artifact_error_messages if msg).rstrip() + '\n')
    else:
        try:
            Path(artifact_error_path).unlink(missing_ok=True)
        except Exception:
            pass

    print(f'GDUNIT_DONE rc={rc} out={out_dir}')
    if rc != 0:
        try:
            _print_failure_summary(console_path=console_path, dest_dir=dest, out_dir=out_dir)
        except Exception as e:
            print(f"GDUNIT_FAILURE_SUMMARY error={e}")
    return 0 if rc == 0 else rc


if __name__ == '__main__':
    raise SystemExit(main())
