#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Update ADR index for the Godot+C# template.

Writes: docs/architecture/ADR_INDEX_GODOT.md (UTF-8, no BOM)

This script is defensive-only and does not modify ADR files themselves.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
import re


REPO_ROOT = Path(__file__).resolve().parents[2]
ADR_DIR = REPO_ROOT / "docs" / "adr"
INDEX_PATH = REPO_ROOT / "docs" / "architecture" / "ADR_INDEX_GODOT.md"

# Keep this list narrow on purpose: this index is for Godot+C# migration ADRs.
TARGET_IDS = {
    "ADR-0015",
    "ADR-0018",
    "ADR-0019",
    "ADR-0020",
    "ADR-0021",
    "ADR-0022",
    "ADR-0023",
    "ADR-0024",
    "ADR-0026",
    "ADR-0027",
}

ADDENDA_PATHS = [
    ADR_DIR / "addenda" / "ADR-0005-godot-quality-gates-addendum.md",
    ADR_DIR / "addenda" / "ADR-0006-godot-data-storage-addendum.md",
]

FRONT_MATTER_RE = re.compile(r"^---\s*$")
YAML_STATUS_RE = re.compile(r"^\s*status\s*:\s*(.+?)\s*$", re.IGNORECASE)
YAML_TITLE_RE = re.compile(r"^\s*title\s*:\s*(.+?)\s*$", re.IGNORECASE)
MD_STATUS_RE = re.compile(r"^-\s*(?:\*\*Status\*\*|Status)\s*:\s*(.+?)\s*$", re.IGNORECASE)
H1_RE = re.compile(r"^#\s+(.+?)\s*$")
ADR_ID_RE = re.compile(r"(ADR-\d{4})")


@dataclass(frozen=True)
class AdrMeta:
    adr_id: str
    title: str
    status: str
    rel_path: str

    @property
    def sort_key(self) -> int:
        m = re.search(r"ADR-(\d{4})", self.adr_id)
        return int(m.group(1)) if m else 9999


def _read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="ignore")


def _strip_quotes(value: str) -> str:
    s = (value or "").strip()
    if (s.startswith('"') and s.endswith('"')) or (s.startswith("'") and s.endswith("'")):
        return s[1:-1].strip()
    return s


def _extract_front_matter(lines: list[str]) -> list[str]:
    if not lines or not FRONT_MATTER_RE.match(lines[0]):
        return []
    out: list[str] = []
    for i in range(1, min(len(lines), 200)):
        if FRONT_MATTER_RE.match(lines[i]):
            break
        out.append(lines[i])
    return out


def parse_adr(path: Path) -> AdrMeta:
    text = _read_text(path)
    lines = text.splitlines()

    m_id = ADR_ID_RE.search(path.name)
    adr_id = m_id.group(1) if m_id else ""

    status: str | None = None
    title: str | None = None

    fm = _extract_front_matter(lines)
    for line in fm:
        if title is None:
            m = YAML_TITLE_RE.match(line)
            if m:
                title = _strip_quotes(m.group(1))
        if status is None:
            m = YAML_STATUS_RE.match(line)
            if m:
                status = _strip_quotes(m.group(1))

    if status is None:
        for line in lines[:120]:
            m = MD_STATUS_RE.match(line.strip())
            if m:
                status = m.group(1).strip()
                break

    if title is None:
        for line in lines[:120]:
            m = H1_RE.match(line.strip())
            if m:
                title = m.group(1).strip()
                break

    if title and adr_id and title.startswith(adr_id + ":"):
        title = title[len(adr_id) + 1 :].strip()

    if not adr_id:
        adr_id = "(unknown)"
    if not title:
        title = "(missing title)"
    if not status:
        status = "(missing status)"

    rel_path = path.relative_to(REPO_ROOT).as_posix()
    return AdrMeta(adr_id=adr_id, title=title, status=status, rel_path=rel_path)


def build_index_markdown(*, accepted: list[AdrMeta], proposed: list[AdrMeta], addenda: list[AdrMeta]) -> str:
    tick = chr(96)
    out: list[str] = []
    out.append("# ADR 索引 — Godot 迁移（Accepted + Addenda）")
    out.append("")
    out.append("本文件用于快速定位与 Godot + C# 模板相关的 ADR（以 `docs/adr/` 为唯一口径）。")
    out.append("")
    out.append("## 已采纳（Accepted）")
    for a in accepted:
        out.append(f"- {a.adr_id}: {a.title} — {tick}{a.rel_path}{tick}")
    if proposed:
        out.append("")
        out.append("## 提案中（Proposed）")
        for a in proposed:
            out.append(f"- {a.adr_id}: {a.title} — {tick}{a.rel_path}{tick}")
    out.append("")
    out.append("## 附录（Addenda）")
    for a in addenda:
        out.append(f"- {a.adr_id} Addendum: {a.title} — {tick}{a.rel_path}{tick}")
    out.append("")
    return "\n".join(out)


def main() -> int:
    if not ADR_DIR.is_dir():
        raise FileNotFoundError(str(ADR_DIR))

    metas: list[AdrMeta] = []
    for p in sorted(ADR_DIR.glob("ADR-*.md")):
        if not p.is_file():
            continue
        meta = parse_adr(p)
        if meta.adr_id in TARGET_IDS:
            metas.append(meta)

    accepted = sorted([m for m in metas if m.status.strip().lower() == "accepted"], key=lambda m: m.sort_key)
    proposed = sorted([m for m in metas if m.status.strip().lower() != "accepted"], key=lambda m: m.sort_key)

    addenda: list[AdrMeta] = []
    for p in ADDENDA_PATHS:
        if p.is_file():
            addenda.append(parse_adr(p))
    addenda = sorted(addenda, key=lambda m: m.rel_path)

    text = build_index_markdown(accepted=accepted, proposed=proposed, addenda=addenda)
    INDEX_PATH.parent.mkdir(parents=True, exist_ok=True)
    INDEX_PATH.write_text(text, encoding="utf-8")

    print(f"UPDATED {INDEX_PATH.relative_to(REPO_ROOT).as_posix()}")
    print(f"accepted={len(accepted)} proposed={len(proposed)} addenda={len(addenda)}")
    for p in proposed:
        print(f"proposed: {p.adr_id} status={p.status}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
