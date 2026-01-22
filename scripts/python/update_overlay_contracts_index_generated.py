import datetime
import json
import pathlib
import re


GENERATED_START = "<!-- GENERATED_CONTRACTS_START -->"
GENERATED_END = "<!-- GENERATED_CONTRACTS_END -->"


def _extract_event_type(text: str) -> str | None:
    match = re.search(r"\bpublic\s+const\s+string\s+EventType\s*=\s*\"([^\"]+)\"\s*;", text)
    return match.group(1) if match else None


def _extract_description(text: str) -> str | None:
    for line in text.splitlines():
        line = line.strip()
        if line.startswith("/// Description:"):
            return line.removeprefix("/// Description:").strip()
    # Fallback: the first non-empty summary line that isn't the event type.
    in_summary = False
    for line in text.splitlines():
        line = line.strip()
        if line.startswith("/// <summary>"):
            in_summary = True
            continue
        if in_summary and line.startswith("/// </summary>"):
            break
        if in_summary and line.startswith("///"):
            content = line.removeprefix("///").strip()
            if not content:
                continue
            if content.startswith("Domain event:"):
                continue
            return content
    return None


def _extract_record_field_names(text: str) -> list[str]:
    # Best-effort parser for the primary constructor parameter list.
    # This repository's contract events use simple BCL types, so we keep it simple.
    record_match = re.search(r"\brecord\s+\w+\s*\((.*?)\)\s*\{", text, flags=re.S)
    if not record_match:
        return []
    params_block = record_match.group(1)
    parts = [p.strip() for p in params_block.split(",") if p.strip()]
    names: list[str] = []
    for part in parts:
        tokens = part.split()
        if not tokens:
            continue
        name = tokens[-1].strip()
        name = name.rstrip(")")
        names.append(name)
    return names


def _render_generated_section(
    root: pathlib.Path,
    contract_files: list[pathlib.Path],
    dto_files: list[pathlib.Path],
) -> str:
    events: list[dict] = []
    for path in contract_files:
        text = path.read_text(encoding="utf-8")
        event_type = _extract_event_type(text)
        if not event_type:
            continue
        rel = str(path.relative_to(root)).replace("\\", "/")
        category = path.parent.name
        events.append(
            {
                "event_type": event_type,
                "category": category,
                "description": _extract_description(text) or "",
                "fields": _extract_record_field_names(text),
                "path": rel,
            }
        )

    events.sort(key=lambda e: (e["category"], e["event_type"]))

    lines: list[str] = []
    lines.append(GENERATED_START)
    lines.append("")
    lines.append("## 领域事件明细（自动生成，避免漂移）")
    lines.append("")
    lines.append(
        "> 说明：本段由脚本从 `Game.Core/Contracts/**` 中提取 `EventType` 常量与 record 参数生成。"
    )
    lines.append("")

    current_category = None
    for e in events:
        if e["category"] != current_category:
            current_category = e["category"]
            lines.append(f"### {current_category}")
            lines.append("")
        trigger = e["description"] or "See contract XML docs."
        field_list = ", ".join(e["fields"]) if e["fields"] else "(none)"
        lines.append(f"- **{e['event_type']}**")
        lines.append(f"  - Trigger: {trigger}")
        lines.append(f"  - Fields: {field_list}")
        lines.append(f"  - Contract: `{e['path']}`")
        lines.append("")

    lines.append("## DTO / Schema（自动生成，避免漂移）")
    lines.append("")
    lines.append("> 说明：本段列出 Phase2 新增的内容/事件 Schema（不包含 EventType 常量）。")
    lines.append("")

    for path in sorted(dto_files):
        rel = str(path.relative_to(root)).replace("\\", "/")
        lines.append(f"- `{rel}`")
    lines.append("")
    lines.append(GENERATED_END)
    lines.append("")
    return "\n".join(lines)


def _replace_block(text: str, block: str) -> str:
    if GENERATED_START in text and GENERATED_END in text:
        before, rest = text.split(GENERATED_START, 1)
        _, after = rest.split(GENERATED_END, 1)
        return before.rstrip() + "\n\n" + block + "\n" + after.lstrip()
    return text.rstrip() + "\n\n" + block + "\n"


def main() -> int:
    root = pathlib.Path(__file__).resolve().parents[2]
    overlay_path = (
        root
        / "docs"
        / "architecture"
        / "overlays"
        / "PRD-Guild-Manager"
        / "08"
        / "08-Contracts-Index.md"
    )
    contracts_root = root / "Game.Core" / "Contracts"
    if not overlay_path.exists():
        raise FileNotFoundError(f"Missing overlay contracts index: {overlay_path}")
    if not contracts_root.exists():
        raise FileNotFoundError(f"Missing contracts root: {contracts_root}")

    contract_files = list(contracts_root.rglob("*.cs"))

    dto_files: list[pathlib.Path] = []
    for folder in ("Content", "Events"):
        for path in (contracts_root / folder).rglob("*.cs"):
            text = path.read_text(encoding="utf-8")
            if _extract_event_type(text) is None:
                dto_files.append(path)

    new_block = _render_generated_section(root, contract_files, dto_files)
    original = overlay_path.read_text(encoding="utf-8")
    updated = _replace_block(original, new_block)

    overlay_path.write_text(updated, encoding="utf-8")

    out_dir = root / "logs" / "ci" / datetime.date.today().isoformat() / "contracts-docs"
    out_dir.mkdir(parents=True, exist_ok=True)
    report_path = out_dir / "update_contracts_index.json"
    report = {
        "ts": datetime.datetime.utcnow().isoformat() + "Z",
        "overlay_path": str(overlay_path.relative_to(root)).replace("\\", "/"),
        "events_count": sum(1 for p in contract_files if _extract_event_type(p.read_text(encoding="utf-8"))),
        "dto_count": len(dto_files),
        "notes": [
            "This script updates overlay docs via UTF-8 writes (no PowerShell pipelines).",
            "Event triggers are sourced from XML docs when available.",
        ],
    }
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Wrote {report_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

