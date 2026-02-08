from __future__ import annotations

import json
import os
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path


@dataclass(frozen=True)
class UpdateResult:
    path: str
    changed: bool
    notes: list[str]


def read_text_utf8(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def write_text_utf8(path: Path, text: str) -> None:
    path.write_text(text, encoding="utf-8", newline="\n")


def today_str() -> str:
    return os.environ.get("CI_DATE") or datetime.now().strftime("%Y-%m-%d")


def insert_after_anchor(lines: list[str], anchor_substring: str, insert_lines: list[str]) -> tuple[list[str], bool]:
    for i, line in enumerate(lines):
        if anchor_substring in line:
            # Avoid double insert if already present
            window = "\n".join(lines[i : i + 20])
            if any(x in window for x in insert_lines if x.strip()):
                return lines, False
            return lines[: i + 1] + insert_lines + lines[i + 1 :], True
    raise ValueError(f"Anchor not found: {anchor_substring!r}")


def ensure_line_replacement(lines: list[str], old: str, new: str) -> tuple[list[str], bool]:
    changed = False
    out: list[str] = []
    for line in lines:
        if line.strip() == old.strip():
            out.append(new)
            changed = True
        else:
            out.append(line)
    return out, changed


def update_contracts_index(path: Path) -> UpdateResult:
    text = read_text_utf8(path)
    lines = text.splitlines()
    notes: list[str] = []

    # Insert officer events under "### Guild" section after member.role_changed.
    insert_lines = [
        "- `core.guild.officer.assigned` \u2192 `Game.Core/Contracts/Guild/GuildOfficerAssigned.cs`",
        "- `core.guild.officer.revoked` \u2192 `Game.Core/Contracts/Guild/GuildOfficerRevoked.cs`",
    ]
    updated, inserted = insert_after_anchor(
        lines,
        anchor_substring="core.guild.member.role_changed",
        insert_lines=insert_lines,
    )
    if inserted:
        notes.append("Inserted officer events into Guild section.")
    return UpdateResult(path.as_posix(), inserted, notes), "\n".join(updated) + "\n"


def update_officers_feature_slice(path: Path) -> UpdateResult:
    text = read_text_utf8(path)
    lines = text.splitlines()
    notes: list[str] = []

    # Ensure contract paths are in backticks (so validate_contracts.py can detect them).
    lines, changed1 = ensure_line_replacement(
        lines,
        old="- 契约位置：Game.Core/Contracts/Guild/GuildOfficerAssigned.cs",
        new="- 契约位置：`Game.Core/Contracts/Guild/GuildOfficerAssigned.cs`",
    )
    if changed1:
        notes.append("Backticked assigned contract path.")

    # Add revoked event definition after assigned block if not present.
    revoked_block = [
        "- **GuildOfficerRevoked** (core.guild.officer.revoked)",
        "  - 触发时机：官员撤销成功并持久化后",
        "  - 字段：GuildId, UserId, Slot, RevokedAt, RevokedByUserId",
        "  - 契约位置：`Game.Core/Contracts/Guild/GuildOfficerRevoked.cs`",
    ]
    # Insert after the assigned contract path line.
    updated, inserted = insert_after_anchor(
        lines,
        anchor_substring="GuildOfficerAssigned.cs",
        insert_lines=revoked_block,
    )
    if inserted:
        notes.append("Added revoked event contract entry.")

    return UpdateResult(path.as_posix(), (changed1 or inserted), notes), "\n".join(updated) + "\n"


def main() -> int:
    repo_root = Path(__file__).resolve().parents[2]
    index_path = repo_root / "docs" / "architecture" / "overlays" / "PRD-Guild-Manager" / "08" / "08-Contracts-Index.md"
    officers_path = (
        repo_root
        / "docs"
        / "architecture"
        / "overlays"
        / "PRD-Guild-Manager"
        / "08"
        / "08-FeatureSlice-Phase2-Officers.md"
    )

    results: list[dict] = []
    changed_any = False

    idx_res, idx_text = update_contracts_index(index_path)
    if idx_res.changed:
        write_text_utf8(index_path, idx_text)
        changed_any = True
    results.append({"path": idx_res.path, "changed": idx_res.changed, "notes": idx_res.notes})

    off_res, off_text = update_officers_feature_slice(officers_path)
    if off_res.changed:
        write_text_utf8(officers_path, off_text)
        changed_any = True
    results.append({"path": off_res.path, "changed": off_res.changed, "notes": off_res.notes})

    # Audit output
    out_dir = repo_root / "logs" / "ci" / today_str() / "contracts-doc-update"
    out_dir.mkdir(parents=True, exist_ok=True)
    out_path = out_dir / "officer-revoked-doc-update.json"
    out_path.write_text(
        json.dumps({"ts": datetime.now(timezone.utc).isoformat(timespec="seconds"), "results": results}, ensure_ascii=False, indent=2)
        + "\n",
        encoding="utf-8",
        newline="\n",
    )

    print(f"OK: updated overlay contract docs (changed={changed_any})")
    print(f"report: {out_path.as_posix()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
