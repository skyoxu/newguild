"""
Fix NTFS ACL drift that can block Git writes on Windows.

- Captures ACL snapshots to logs/ci/<date>/permissions/
- Audits the ACL chain for unresolved SID DENY rules
- Removes unresolved SID ACEs from repo root and .git (or a specified SID)
- Optional: protect repo root from parent inheritance drift
- References: ADR-0002, ADR-0005
"""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
import platform
import re
import subprocess
from pathlib import Path

DEFAULT_SID_SELECTOR = "auto"


def repo_root() -> Path:
    cur = Path.cwd().resolve()
    while True:
        if (cur / "project.godot").is_file():
            return cur
        if cur.parent == cur:
            raise RuntimeError("Failed to locate repo root (missing project.godot).")
        cur = cur.parent


def ci_date() -> str:
    raw = os.environ.get("CI_DATE_UTC") or os.environ.get("CI_DATE")
    if raw:
        try:
            dt.datetime.strptime(raw, "%Y-%m-%d")
            return raw
        except ValueError:
            pass
    return dt.datetime.now(dt.UTC).strftime("%Y-%m-%d")


def run(cmd: list[str], *, cwd: Path) -> tuple[int, str]:
    p = subprocess.run(
        cmd,
        cwd=str(cwd),
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    out = (p.stdout or "") + (p.stderr or "")
    return p.returncode, out


def dump_icacls(path: Path, dest: Path, *, root: Path) -> None:
    rc, out = run(["icacls", str(path)], cwd=root)
    dest.write_text(out, encoding="utf-8", newline="\n")
    if rc != 0:
        raise RuntimeError(f"icacls failed for {path} (rc={rc})")


def ensure_logs_dir(root: Path) -> Path:
    out_dir = root / "logs" / "ci" / ci_date() / "permissions"
    out_dir.mkdir(parents=True, exist_ok=True)
    return out_dir


def run_powershell(ps: str, *, root: Path) -> tuple[int, str]:
    ps_wrapped = (
        "[Console]::OutputEncoding=[System.Text.Encoding]::UTF8;"
        + "$ErrorActionPreference='Stop';"
        + "try { Import-Module Microsoft.PowerShell.Security -ErrorAction Stop } catch { }"
        + ps
    )

    def _try(exe: str) -> tuple[int, str]:
        try:
            return run([exe, "-NoProfile", "-NonInteractive", "-Command", ps_wrapped], cwd=root)
        except FileNotFoundError:
            return 127, f"{exe} not found"

    rc, out = _try("powershell")
    if rc == 0:
        return rc, out

    # GitHub Actions runners sometimes fail to autoload/resolve Get-Acl in Windows PowerShell;
    # retry using PowerShell 7 if available.
    lowered = out.lower()
    if "get-acl" in lowered and ("could not be loaded" in lowered or "couldnotautoloadmatchingmodule" in lowered):
        rc2, out2 = _try("pwsh")
        if rc2 == 0:
            return rc2, out2
        return rc2, out2 + "\n" + out

    return rc, out


def get_acl_json(path: Path, *, root: Path) -> dict:
    ps = rf"""
$p = (Resolve-Path -LiteralPath '{str(path)}').Path
$acl = Get-Acl -LiteralPath $p
$access = @()
foreach ($r in @($acl.Access)) {{
  $id = $r.IdentityReference.Value
  $isSid = ($id -match '^S-1-')
  $isResolved = $true
  if ($isSid) {{
    try {{
      [void]$r.IdentityReference.Translate([System.Security.Principal.NTAccount])
    }} catch {{
      $isResolved = $false
    }}
  }}
  $access += [pscustomobject]@{{
    Identity = $id
    IsSid = $isSid
    IsResolved = $isResolved
    AccessControlType = $r.AccessControlType.ToString()
    Rights = $r.FileSystemRights.ToString()
    InheritanceFlags = $r.InheritanceFlags.ToString()
    PropagationFlags = $r.PropagationFlags.ToString()
    IsInherited = [bool]$r.IsInherited
  }}
}}
[pscustomobject]@{{
  Path = $p
  Owner = $acl.Owner
  AreAccessRulesProtected = [bool]$acl.AreAccessRulesProtected
  Access = $access
}} | ConvertTo-Json -Depth 6
"""
    rc, out = run_powershell(ps, root=root)
    if rc != 0:
        raise RuntimeError(f"Get-Acl failed (rc={rc}) for {path}: {out.strip()}")
    try:
        return json.loads(out)
    except json.JSONDecodeError as e:
        raise RuntimeError(f"Failed to parse Get-Acl JSON for {path}: {e}\n{out[:500]}") from e


def path_chain_to_drive_root(p: Path) -> list[Path]:
    p = p.resolve()
    chain: list[Path] = [p]
    cur = p
    while True:
        parent = cur.parent
        if parent == cur:
            break
        chain.append(parent)
        cur = parent
    return chain


def summarize_unresolved_denies(acl: dict) -> list[dict]:
    hits: list[dict] = []
    for a in acl.get("Access", []):
        if (
            a.get("AccessControlType") == "Deny"
            and a.get("IsSid") is True
            and a.get("IsResolved") is False
        ):
            hits.append(a)
    return hits


def collect_unresolved_deny_sids(path: Path, *, root: Path) -> list[str]:
    acl = get_acl_json(path, root=root)
    denies = summarize_unresolved_denies(acl)
    sids = sorted({d.get("Identity") for d in denies if d.get("Identity")})
    return [s for s in sids if isinstance(s, str) and s.strip()]


_SID_RE = re.compile(r"^S-1-\d+(?:-\d+)+$")


def is_windows_sid(identity: str) -> bool:
    # Restrict auto mode to true SID strings only.
    # If callers need to remove a non-SID identity, they must pass --sid explicitly.
    return bool(_SID_RE.match(identity.strip()))


def remove_sid_aces(path: Path, sid: str, *, root: Path) -> int:
    # Remove both allow and deny ACEs for this SID, even if the SID cannot be translated.
    #
    # icacls /remove does NOT work for unresolved SIDs (returns rc=1332).
    # Use PowerShell + Set-Acl to remove rules by IdentityReference.Value.
    ps = rf"""
$sid = '{sid}'
$p = (Resolve-Path -LiteralPath '{str(path)}').Path
$acl = Get-Acl -LiteralPath $p
$removed = 0
foreach ($r in @($acl.Access)) {{
  if ($r.IdentityReference.Value -eq $sid) {{
    $acl.RemoveAccessRuleSpecific($r) | Out-Null
    $removed += 1
  }}
}}
Set-Acl -LiteralPath $p -AclObject $acl
Write-Output ('REMOVED=' + $removed)
"""
    rc, out = run_powershell(ps, root=root)
    if rc != 0:
        raise RuntimeError(f"Set-Acl SID removal failed (rc={rc}) for {path}: {out.strip()}")
    # Parse "REMOVED=<n>"
    removed = 0
    for line in out.splitlines():
        if line.startswith("REMOVED="):
            try:
                removed = int(line.split("=", 1)[1].strip())
            except ValueError:
                removed = 0
    return removed


def set_git_inheritance(git_dir: Path, mode: str, *, root: Path) -> None:
    # mode: "enable" => /inheritance:e, "disable" => /inheritance:r
    flag = "/inheritance:e" if mode == "enable" else "/inheritance:r"
    rc, out = run(["icacls", str(git_dir), flag, "/t", "/c"], cwd=root)
    if rc != 0:
        raise RuntimeError(f"icacls {flag} failed (rc={rc}) for {git_dir}: {out.strip()}")


def protect_repo_root_from_inheritance(root: Path) -> None:
    ps = rf"""
$p = (Resolve-Path -LiteralPath '{str(root)}').Path
$acl = Get-Acl -LiteralPath $p
# Protect=True, PreserveInheritance=True (copies inherited rules as explicit)
$acl.SetAccessRuleProtection($true, $true)
Set-Acl -LiteralPath $p -AclObject $acl
Write-Output 'OK'
"""
    rc, out = run_powershell(ps, root=root)
    if rc != 0 or "OK" not in out:
        raise RuntimeError(f"Failed to protect repo root inheritance: {out.strip()}")


def git_lock_smoke(root: Path) -> None:
    # Minimal proof that Git can write lock files under .git (no network).
    rc, sha = run(["git", "rev-parse", "HEAD"], cwd=root)
    if rc != 0:
        raise RuntimeError(f"git rev-parse HEAD failed: {sha.strip()}")
    sha = sha.strip()
    name = "_acl_smoke_" + hashlib.sha1(os.urandom(16)).hexdigest()[:8]
    rc, out = run(["git", "update-ref", f"refs/heads/{name}", sha], cwd=root)
    if rc != 0:
        raise RuntimeError(f"git update-ref failed: {out.strip()}")
    run(["git", "update-ref", "-d", f"refs/heads/{name}"], cwd=root)


def write_report(out_dir: Path, payload: dict) -> None:
    (out_dir / "fix-git-acl-drift.json").write_text(
        json.dumps(payload, ensure_ascii=False, indent=2),
        encoding="utf-8",
        newline="\n",
    )
    lines = [
        f"generated: {payload.get('generated')}",
        f"status: {payload.get('status')}",
        f"sid: {payload.get('sid')}",
        f"repo_root: {payload.get('repo_root')}",
        f"git_dir: {payload.get('git_dir')}",
        f"inheritance_mode: {payload.get('inheritance_mode')}",
        f"remove_from_repo_root: {payload.get('remove_from_repo_root')}",
        "",
    ]
    for step in payload.get("steps", []):
        lines.append(f"- {step}")
    (out_dir / "fix-git-acl-drift.txt").write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument(
        "--sid",
        default=DEFAULT_SID_SELECTOR,
        help="SID to remove from ACLs. Use 'auto' to remove all unresolved SID DENY identities discovered on the target paths.",
    )
    ap.add_argument("--git-dir", default=".git", help="Git directory relative to repo root.")
    ap.add_argument("--dry-run", action="store_true", help="Only report; do not modify ACLs.")
    ap.add_argument(
        "--audit",
        action="store_true",
        help="Audit ACL chain (repo root -> parents) to locate unresolved SID/DENY source; writes acl-audit.json.",
    )
    ap.add_argument(
        "--remove-from-repo-root",
        action="store_true",
        default=True,
        help="Also remove SID ACEs from repo root (non-recursive) to prevent re-inheritance (default: true).",
    )
    ap.add_argument(
        "--no-remove-from-repo-root",
        dest="remove_from_repo_root",
        action="store_false",
        help="Do not touch repo root ACL; only fix .git.",
    )
    ap.add_argument(
        "--inheritance",
        choices=["enable", "disable", "keep"],
        default="keep",
        help="Set inheritance mode for .git after cleanup (default: keep).",
    )
    ap.add_argument(
        "--protect-repo-root",
        action="store_true",
        help="Hardening: protect repo root ACL from parent inheritance drift (copies current inherited rules).",
    )
    ap.add_argument(
        "--smoke-git-lock",
        action="store_true",
        help="Run a local git lock/write smoke test (git update-ref) after changes.",
    )
    ap.add_argument(
        "--recursive",
        action="store_true",
        help="Apply SID ACE removal recursively under .git (NOT recommended unless you know why).",
    )
    args = ap.parse_args()

    if platform.system().lower() != "windows":
        raise SystemExit("This script is Windows-only.")

    root = repo_root()
    out_dir = ensure_logs_dir(root)
    git_dir = (root / args.git_dir).resolve()
    try:
        _ = git_dir.relative_to(root)
    except ValueError:
        raise SystemExit(f"--git-dir must resolve inside repo root (root={root}, git_dir={git_dir}).")
    sid = str(args.sid).strip() or DEFAULT_SID_SELECTOR

    root_unresolved = collect_unresolved_deny_sids(root, root=root)
    git_unresolved = collect_unresolved_deny_sids(git_dir, root=root)
    needs_fix = bool(root_unresolved or git_unresolved)

    steps: list[str] = []
    payload = {
        "generated": dt.datetime.now(dt.UTC).isoformat(timespec="seconds"),
        "status": "started",
        "sid": sid,
        "repo_root": str(root),
        "git_dir": str(git_dir),
        "remove_from_repo_root": bool(args.remove_from_repo_root),
        "inheritance_mode": args.inheritance,
        "findings": {
            "repo_root_unresolved_deny_sids": root_unresolved,
            "git_unresolved_deny_sids": git_unresolved,
        },
        "steps": steps,
    }
    write_report(out_dir, payload)

    dump_icacls(root, out_dir / "before-repo-root.icacls.txt", root=root)
    dump_icacls(git_dir, out_dir / "before-dotgit.icacls.txt", root=root)

    if args.audit:
        chain = path_chain_to_drive_root(root)
        audit = {
            "generated": dt.datetime.now(dt.UTC).isoformat(timespec="seconds"),
            "repo_root": str(root),
            "git_dir": str(git_dir),
            "chain": [],
            "suspectSidSources": {},
        }
        unresolved_by_path: list[tuple[Path, list[dict]]] = []
        for p in chain:
            acl = get_acl_json(p, root=root)
            denies = summarize_unresolved_denies(acl)
            unresolved_by_path.append((p, denies))
            audit["chain"].append(
                {
                    "path": str(p),
                    "owner": acl.get("Owner"),
                    "areAccessRulesProtected": acl.get("AreAccessRulesProtected"),
                    "unresolvedDenies": denies,
                }
            )

        suspect_sids = sorted(
            {d.get("Identity") for _, denies in unresolved_by_path for d in denies if d.get("Identity")}
        )
        sources: dict[str, str] = {}
        for sid_found in suspect_sids:
            src: str | None = None
            for p, denies in unresolved_by_path:
                for d in denies:
                    if d.get("Identity") == sid_found and d.get("IsInherited") is False:
                        src = str(p)
                        break
                if src:
                    break
            sources[sid_found] = src or "unknown (only inherited or not present)"
        audit["suspectSidSources"] = sources
        (out_dir / "acl-audit.json").write_text(
            json.dumps(audit, ensure_ascii=False, indent=2),
            encoding="utf-8",
            newline="\n",
        )
        steps.append(f"Wrote ACL audit: {out_dir / 'acl-audit.json'}")

    all_unresolved = sorted({*root_unresolved, *git_unresolved})
    unresolved_sid_identities = [i for i in all_unresolved if is_windows_sid(i)]
    unresolved_non_sid_identities = [i for i in all_unresolved if not is_windows_sid(i)]
    payload["findings"]["unresolved_deny_non_sid_identities"] = unresolved_non_sid_identities

    planned_target_sids: list[str]
    if sid.lower() == "auto":
        planned_target_sids = unresolved_sid_identities
    else:
        planned_target_sids = [sid]
    payload["plannedTargetSids"] = planned_target_sids

    if args.dry_run:
        payload["status"] = "dry-run"
        payload["needs_fix"] = needs_fix
        if args.smoke_git_lock:
            steps.append("Note: --smoke-git-lock skipped in dry-run mode (no repository writes).")
        if needs_fix:
            steps.append(
                f"Detected unresolved SID DENY rules (repo_root={len(root_unresolved)}, git={len(git_unresolved)})."
            )
        else:
            steps.append("No unresolved SID DENY rules detected.")
    else:
        target_sids: list[str]
        if sid.lower() == "auto":
            target_sids = unresolved_sid_identities
            steps.append(f"Auto mode: removing unresolved deny SID identities (count={len(target_sids)}).")
        else:
            target_sids = [sid]
        payload["targetSids"] = target_sids

        if args.remove_from_repo_root:
            removed_root_total = 0
            for s in target_sids:
                removed_root_total += remove_sid_aces(root, s, root=root)
            steps.append(f"Removed SID ACEs from repo root (removed_total={removed_root_total}).")

        removed_git_total = 0
        for s in target_sids:
            removed_git_total += remove_sid_aces(git_dir, s, root=root)
        steps.append(f"Removed SID ACEs from .git (removed_total={removed_git_total}).")

        if args.recursive:
            # Optional deep cleanup: apply the same removal to all descendants.
            # Only use if inheritance was previously disabled and ACEs were copied down.
            removed_desc = 0
            for p in [git_dir, *git_dir.rglob("*")]:
                try:
                    for s in target_sids:
                        removed_desc += remove_sid_aces(p, s, root=root)
                except Exception:
                    # Best-effort to keep the script usable; details are in ACL snapshots.
                    continue
            steps.append(f"Recursive cleanup under .git completed (removed_total={removed_desc}).")

        if args.inheritance in ("enable", "disable"):
            set_git_inheritance(git_dir, args.inheritance, root=root)
            steps.append(f"Set .git inheritance: {args.inheritance}.")
        else:
            steps.append("Kept .git inheritance unchanged.")

        if args.protect_repo_root:
            protect_repo_root_from_inheritance(root)
            steps.append("Protected repo root ACL from parent inheritance drift.")

        if args.smoke_git_lock:
            git_lock_smoke(root)
            steps.append("Git lock/write smoke test passed (git update-ref).")
            payload["smoke"] = {"git_update_ref": True}

        root_unresolved_after = collect_unresolved_deny_sids(root, root=root)
        git_unresolved_after = collect_unresolved_deny_sids(git_dir, root=root)
        payload["findings"] = {
            "repo_root_unresolved_deny_sids": root_unresolved_after,
            "git_unresolved_deny_sids": git_unresolved_after,
        }
        if root_unresolved_after or git_unresolved_after:
            payload["status"] = "failed"
            steps.append(
                f"Unresolved SID DENY rules still present after cleanup (repo_root={len(root_unresolved_after)}, git={len(git_unresolved_after)})."
            )
            write_report(out_dir, payload)
            print(f"[REPORT] {out_dir / 'fix-git-acl-drift.json'}")
            print(f"[REPORT] {out_dir / 'fix-git-acl-drift.txt'}")
            return 3

    if args.dry_run:
        dump_icacls(root, out_dir / "dryrun-repo-root.icacls.txt", root=root)
        dump_icacls(git_dir, out_dir / "dryrun-dotgit.icacls.txt", root=root)
    else:
        dump_icacls(root, out_dir / "after-repo-root.icacls.txt", root=root)
        dump_icacls(git_dir, out_dir / "after-dotgit.icacls.txt", root=root)

    # If we are in dry-run and findings exist, fail the process so CI can gate/auto-remediate.
    if args.dry_run and needs_fix:
        write_report(out_dir, payload)
        print(f"[REPORT] {out_dir / 'fix-git-acl-drift.json'}")
        print(f"[REPORT] {out_dir / 'fix-git-acl-drift.txt'}")
        return 2

    if payload.get("status") in (None, "started"):
        payload["status"] = "ok"

    write_report(out_dir, payload)
    print(f"[REPORT] {out_dir / 'fix-git-acl-drift.json'}")
    print(f"[REPORT] {out_dir / 'fix-git-acl-drift.txt'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
