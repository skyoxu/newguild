#!/usr/bin/env python3
"""
Sentry Release Health gate.

Goal:
- Produce an auditable artifact at logs/ci/<YYYY-MM-DD>/release-health.json (JSON).
- Exit with code 0 when the release health threshold is met; non-zero otherwise.

Offline-first behavior:
- If GD_OFFLINE_MODE=1, the script will NOT perform any network calls.
- In offline mode, provide input via:
  - RELEASE_HEALTH_THRESHOLD_JSON
  - RELEASE_HEALTH_METRICS_JSON

Online behavior (best-effort, stdlib only):
- If offline mode is not enabled and RELEASE_HEALTH_METRICS_JSON is missing, the script will attempt to query Sentry.
  Required env:
  - SENTRY_AUTH_TOKEN, SENTRY_ORG, SENTRY_PROJECT
  Optional env:
  - SENTRY_ENVIRONMENT, SENTRY_BASE_URL, RELEASE_HEALTH_WINDOW_HOURS, RELEASE_CRASHFREE_THRESHOLD

This script intentionally avoids writing secrets to disk.
"""

from __future__ import annotations

import datetime as dt
import json
import os
import sys
import pathlib
import urllib.parse
import urllib.request
from typing import Any, Dict, Optional, Tuple


def _utc_now() -> dt.datetime:
    return dt.datetime.now(dt.timezone.utc)


def _local_date_str() -> str:
    return dt.date.today().strftime("%Y-%m-%d")


def _utc_date_str() -> str:
    return _utc_now().date().strftime("%Y-%m-%d")


def _ensure_dir(path: str) -> None:
    os.makedirs(path, exist_ok=True)


def _read_env(name: str, default: Optional[str] = None) -> Optional[str]:
    val = os.environ.get(name)
    if val is None:
        return default
    return val


def _parse_json_env(name: str) -> Tuple[Optional[Dict[str, Any]], Optional[str]]:
    raw = _read_env(name)
    if raw is None or raw.strip() == "":
        return None, None
    try:
        obj = json.loads(raw)
        if not isinstance(obj, dict):
            return None, f"{name} must be a JSON object"
        return obj, None
    except Exception as ex:
        return None, f"{name} invalid JSON: {ex}"


def _as_float(value: Any) -> Optional[float]:
    if value is None:
        return None
    if isinstance(value, (int, float)):
        return float(value)
    if isinstance(value, str):
        try:
            return float(value.strip())
        except Exception:
            return None
    return None


def _is_truthy(value: Optional[str]) -> bool:
    if value is None:
        return False
    v = value.strip().lower()
    return v in ("1", "true", "yes", "on")


def _http_get_json(url: str, token: str, timeout_sec: int) -> Any:
    req = urllib.request.Request(url)
    req.add_header("Authorization", f"Bearer {token}")
    req.add_header("Accept", "application/json")
    with urllib.request.urlopen(req, timeout=timeout_sec) as resp:
        raw = resp.read()
    return json.loads(raw.decode("utf-8", errors="strict"))


def _find_first_float_by_key(obj: Any, key: str) -> Optional[float]:
    if isinstance(obj, dict):
        for k, v in obj.items():
            if k == key:
                fv = _as_float(v)
                if fv is not None:
                    return fv
            found = _find_first_float_by_key(v, key)
            if found is not None:
                return found
    elif isinstance(obj, list):
        for it in obj:
            found = _find_first_float_by_key(it, key)
            if found is not None:
                return found
    return None


def _fetch_crash_free_sessions_pct_from_sentry(
    *,
    base_url: str,
    token: str,
    org: str,
    project: str,
    environment: Optional[str],
    window_hours: int,
    timeout_sec: int,
) -> Tuple[Optional[float], str]:
    if not base_url.startswith("https://"):
        return None, "SENTRY_BASE_URL must be https://"

    base = base_url.rstrip("/")

    project_url = f"{base}/api/0/projects/{urllib.parse.quote(org)}/{urllib.parse.quote(project)}/"
    proj = _http_get_json(project_url, token, timeout_sec)
    project_id = str(proj.get("id") or "").strip()
    if project_id == "":
        return None, "Sentry project id not found"

    params = {
        "project": project_id,
        "field": "crash_free_rate(session)",
        "statsPeriod": f"{window_hours}h",
        "interval": "1h",
    }
    if environment:
        params["environment"] = environment
    q = urllib.parse.urlencode(params, doseq=True)
    sessions_url = f"{base}/api/0/organizations/{urllib.parse.quote(org)}/sessions/?{q}"
    data = _http_get_json(sessions_url, token, timeout_sec)

    rate = _find_first_float_by_key(data, "crash_free_rate(session)")
    if rate is None:
        rate = _find_first_float_by_key(data, "crash_free_rate_session")
    if rate is None:
        return None, "Sentry sessions response did not include crash_free_rate(session)"

    pct = rate * 100.0 if rate <= 1.0 else rate
    return pct, "ok"


def _split_csv(value: Optional[str]) -> list[str]:
    if not value:
        return []
    items = []
    for raw in value.split(","):
        s = raw.strip().lower()
        if s:
            items.append(s)
    return items


def _is_allowed_host(host: str, allow: list[str]) -> bool:
    host_l = host.strip().lower()
    if not host_l:
        return False
    for rule in allow:
        r = rule.strip().lower()
        if not r:
            continue
        if host_l == r:
            return True
        if r.startswith("*.") and host_l.endswith(r[1:]):
            return True
    return False


def _resolve_repo_root() -> str:
    # Prefer a stable root derived from this script location.
    here = pathlib.Path(__file__).resolve()
    cur = here.parent
    for _ in range(12):
        if (cur / "project.godot").is_file() and (cur / "scripts").is_dir():
            return str(cur)
        if cur.parent == cur:
            break
        cur = cur.parent
    return os.getcwd()


def _validate_sentry_base_url_host(base_url: str) -> Tuple[bool, str]:
    try:
        parsed = urllib.parse.urlparse(base_url)
    except Exception as ex:
        return False, f"SENTRY_BASE_URL invalid: {ex}"

    if parsed.scheme.lower() != "https":
        return False, "SENTRY_BASE_URL must be https://"

    host = (parsed.hostname or "").strip().lower()
    allow = _split_csv(_read_env("SENTRY_ALLOWED_HOSTS")) or _split_csv(_read_env("ALLOWED_EXTERNAL_HOSTS"))
    if not allow:
        allow = ["sentry.io"]

    if not _is_allowed_host(host, allow):
        return False, f"SENTRY_BASE_URL host '{host}' is not allowlisted"

    return True, "ok"


def _build_artifact(
    *,
    passed: bool,
    reason: str,
    metrics: Dict[str, Any],
    threshold: Dict[str, Any],
    sentry: Dict[str, Any],
    error: Optional[Dict[str, Any]] = None,
) -> Dict[str, Any]:
    out: Dict[str, Any] = {
        "ts": _utc_now().isoformat(),
        "passed": bool(passed),
        "reason": reason,
        "metrics": metrics,
        "threshold": threshold,
        "sentry": sentry,
    }
    if error:
        out["error"] = error
    return out


def _write_artifact(repo_root: str, artifact: Dict[str, Any]) -> str:
    utc_date = _utc_date_str()
    local_date = _local_date_str()

    rel_paths = [os.path.join("logs", "ci", utc_date, "release-health.json")]
    if local_date != utc_date:
        rel_paths.append(os.path.join("logs", "ci", local_date, "release-health.json"))

    last_written = ""
    for rel in rel_paths:
        path = os.path.join(repo_root, rel)
        _ensure_dir(os.path.dirname(path))
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            json.dump(artifact, f, ensure_ascii=False, indent=2)
            f.write("\n")
        last_written = path

    return last_written


def main() -> int:
    repo_root = _resolve_repo_root()

    offline = _is_truthy(_read_env("GD_OFFLINE_MODE"))
    base_url = _read_env("SENTRY_BASE_URL", "https://sentry.io") or "https://sentry.io"
    token = _read_env("SENTRY_AUTH_TOKEN") or ""
    org = _read_env("SENTRY_ORG") or ""
    project = _read_env("SENTRY_PROJECT") or ""
    environment = _read_env("SENTRY_ENVIRONMENT")

    threshold_obj, threshold_err = _parse_json_env("RELEASE_HEALTH_THRESHOLD_JSON")
    metrics_obj, metrics_err = _parse_json_env("RELEASE_HEALTH_METRICS_JSON")

    default_threshold_pct = _as_float(_read_env("RELEASE_CRASHFREE_THRESHOLD")) or 99.5
    default_window_hours = int(_as_float(_read_env("RELEASE_HEALTH_WINDOW_HOURS")) or 24)

    crash_free_threshold = default_threshold_pct
    window_hours = default_window_hours
    if threshold_obj:
        v = _as_float(threshold_obj.get("crash_free_sessions_threshold"))
        if v is not None:
            crash_free_threshold = v
        wh = _as_float(threshold_obj.get("window_hours"))
        if wh is not None:
            window_hours = int(wh)

    sentry_meta = {
        "offline": offline,
        "base_url": base_url if base_url.startswith("https://") else "",
        "org": org,
        "project": project,
        "environment": environment,
        "fetched": False,
    }

    try:
        ok, msg = _validate_sentry_base_url_host(base_url)
        if not ok and not offline:
            artifact = _build_artifact(
                passed=False,
                reason=msg,
                metrics={"crash_free_sessions": None, "source": "config"},
                threshold={"crash_free_sessions": crash_free_threshold, "window_hours": window_hours},
                sentry=sentry_meta,
                error={"type": "config", "message": msg},
            )
            out_path = _write_artifact(repo_root, artifact)
            print(f"RELEASE_HEALTH_GATE status=fail passed=0 out={out_path}")
            return 2

        if threshold_err:
            artifact = _build_artifact(
                passed=False,
                reason=threshold_err,
                metrics={"crash_free_sessions": None, "source": "env"},
                threshold={"crash_free_sessions": crash_free_threshold, "window_hours": window_hours},
                sentry=sentry_meta,
                error={"type": "config", "message": threshold_err},
            )
            out_path = _write_artifact(repo_root, artifact)
            print(f"RELEASE_HEALTH_GATE status=fail passed=0 out={out_path}")
            return 2

        if metrics_err:
            artifact = _build_artifact(
                passed=False,
                reason=metrics_err,
                metrics={"crash_free_sessions": None, "source": "env"},
                threshold={"crash_free_sessions": crash_free_threshold, "window_hours": window_hours},
                sentry=sentry_meta,
                error={"type": "config", "message": metrics_err},
            )
            out_path = _write_artifact(repo_root, artifact)
            print(f"RELEASE_HEALTH_GATE status=fail passed=0 out={out_path}")
            return 2

        crash_free_sessions: Optional[float] = None
        metrics_source = "env"
        if metrics_obj and isinstance(metrics_obj, dict):
            crash_free_sessions = _as_float(metrics_obj.get("crash_free_sessions"))
            if crash_free_sessions is None:
                crash_free_sessions = _as_float(metrics_obj.get("crash_free_sessions_pct"))
        if crash_free_sessions is None and not offline:
            if token and org and project:
                timeout_sec = int(_as_float(_read_env("SENTRY_TIMEOUT_SEC")) or 20)
                crash_free_sessions, msg = _fetch_crash_free_sessions_pct_from_sentry(
                    base_url=base_url,
                    token=token,
                    org=org,
                    project=project,
                    environment=environment,
                    window_hours=window_hours,
                    timeout_sec=timeout_sec,
                )
                sentry_meta["fetched"] = crash_free_sessions is not None
                metrics_source = "sentry"
                if crash_free_sessions is None:
                    raise RuntimeError(msg)
            else:
                raise RuntimeError("Missing RELEASE_HEALTH_METRICS_JSON and SENTRY_* configuration")

        if crash_free_sessions is None and offline:
            raise RuntimeError("GD_OFFLINE_MODE=1 requires RELEASE_HEALTH_METRICS_JSON")

        passed = bool(crash_free_sessions is not None and crash_free_sessions >= crash_free_threshold)
        cmp = ">=" if passed else "<"
        reason = f"crash_free_sessions {crash_free_sessions:.3g} {cmp} threshold {crash_free_threshold:.3g}"

        artifact = _build_artifact(
            passed=passed,
            reason=reason,
            metrics={
                "crash_free_sessions": crash_free_sessions,
                "source": metrics_source,
                "window_hours": window_hours,
            },
            threshold={
                "crash_free_sessions": crash_free_threshold,
                "window_hours": window_hours,
            },
            sentry=sentry_meta,
        )
        out_path = _write_artifact(repo_root, artifact)
        print(f"RELEASE_HEALTH_GATE status={'ok' if passed else 'fail'} passed={1 if passed else 0} out={out_path}")
        return 0 if passed else 1
    except Exception as ex:
        artifact = _build_artifact(
            passed=False,
            reason=f"error: {type(ex).__name__}: {ex}",
            metrics={"crash_free_sessions": None, "source": "error"},
            threshold={"crash_free_sessions": crash_free_threshold, "window_hours": window_hours},
            sentry=sentry_meta,
            error={"type": type(ex).__name__, "message": str(ex)},
        )
        out_path = _write_artifact(repo_root, artifact)
        print(f"RELEASE_HEALTH_GATE status=fail passed=0 out={out_path}")
        return 2


if __name__ == "__main__":
    sys.exit(main())
