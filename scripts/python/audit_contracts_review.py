import datetime
import json
import pathlib
import re


def _detect_event_type(text: str) -> str | None:
    patterns = [
        r"\bpublic\s+const\s+string\s+EventType\s*=\s*\"([^\"]+)\"\s*;",
        r"\bconst\s+string\s+EventType\s*=\s*\"([^\"]+)\"\s*;",
        r"\bEventType\s*=\s*\"([^\"]+)\"",
    ]
    for pattern in patterns:
        match = re.search(pattern, text)
        if match:
            return match.group(1)
    return None


def _has_xml_doc(text: str) -> bool:
    return "/// <summary>" in text


def _is_domain_event_contract(text: str) -> bool:
    # Heuristic: event contracts in this repo include "Domain event:" in XML docs.
    return "Domain event:" in text or "CloudEvents 1.0 type field" in text


def main() -> int:
    root = pathlib.Path(__file__).resolve().parents[2]
    contracts_dir = root / "Game.Core" / "Contracts"
    if not contracts_dir.exists():
        raise FileNotFoundError(f"Missing contracts dir: {contracts_dir}")

    allowed_prefixes = ("core.", "ui.menu.", "screen.", "security.")

    files: list[dict] = []
    for path in sorted(contracts_dir.rglob("*.cs")):
        text = path.read_text(encoding="utf-8")
        rel = str(path.relative_to(root)).replace("\\", "/")
        event_type = _detect_event_type(text)
        is_domain_event_contract = _is_domain_event_contract(text)

        uses_godot = "using Godot;" in text or "\nusing Godot" in text or "Godot." in text
        uses_object_or_dynamic = bool(re.search(r"\bobject\b|\bdynamic\b", text))

        files.append(
            {
                "path": rel,
                "is_domain_event_contract": is_domain_event_contract,
                "event_type": event_type,
                "event_type_prefix_ok": None
                if event_type is None
                else event_type.startswith(allowed_prefixes),
                "has_eventtype_constant": event_type is not None,
                "has_xml_doc": _has_xml_doc(text),
                "uses_godot": uses_godot,
                "uses_object_or_dynamic": uses_object_or_dynamic,
            }
        )

    summary = {
        "total_files": len(files),
        "missing_xml_doc": sum(1 for f in files if not f["has_xml_doc"]),
        "missing_eventtype_constant": sum(
            1
            for f in files
            if f["is_domain_event_contract"] and not f["has_eventtype_constant"]
        ),
        "eventtype_prefix_violation": sum(
            1
            for f in files
            if f["has_eventtype_constant"] and f["event_type_prefix_ok"] is False
        ),
        "uses_godot": sum(1 for f in files if f["uses_godot"]),
        "uses_object_or_dynamic": sum(1 for f in files if f["uses_object_or_dynamic"]),
    }

    report = {
        "ts": datetime.datetime.utcnow().isoformat() + "Z",
        "contracts_dir": str(contracts_dir.relative_to(root)).replace("\\", "/"),
        "allowed_eventtype_prefixes": list(allowed_prefixes),
        "summary": summary,
        "files": files,
        "notes": [
            "This is a best-effort static audit; it does not parse the C# syntax tree.",
            "DomainEvent is expected to carry Data as object? (envelope), which may trigger the object check.",
        ],
    }

    out_dir = root / "logs" / "ci" / datetime.date.today().isoformat() / "contracts-review"
    out_dir.mkdir(parents=True, exist_ok=True)
    out_path = out_dir / "report.json"
    out_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

    print(f"Wrote {out_path}")
    print(f"Summary: {json.dumps(summary, ensure_ascii=False)}")

    def _print_top(label: str, items: list[dict], max_items: int = 12) -> None:
        print(f"{label}: {len(items)}")
        for f in items[:max_items]:
            if label == "Prefix violations":
                print(f"  {f['path']} event_type={f['event_type']}")
            else:
                print(f"  {f['path']}")

    _print_top("Missing XML doc", [f for f in files if not f["has_xml_doc"]])
    _print_top(
        "Missing EventType",
        [f for f in files if f["is_domain_event_contract"] and not f["has_eventtype_constant"]],
    )
    _print_top(
        "Prefix violations",
        [
            f
            for f in files
            if f["has_eventtype_constant"] and f["event_type_prefix_ok"] is False
        ],
    )

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
