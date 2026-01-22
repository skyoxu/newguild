import datetime
import json
import pathlib


def _ensure_section(text: str, section_md: str) -> str:
    marker = "<!-- PHASE2_CONTRACTS_SECTION -->"
    if marker in text:
        before, _ = text.split(marker, 1)
        return before.rstrip() + "\n\n" + marker + "\n" + section_md.strip() + "\n"
    return text.rstrip() + "\n\n" + marker + "\n" + section_md.strip() + "\n"


def main() -> int:
    root = pathlib.Path(__file__).resolve().parents[2]
    base = root / "docs" / "architecture" / "overlays" / "PRD-Guild-Manager" / "08"

    updates: dict[str, dict] = {
        "08-FeatureSlice-Phase2-Content-Loading.md": {
            "contracts": [
                "Game.Core/Contracts/Content/ContentManifest.cs",
                "Game.Core/Contracts/Content/ContentManifestEntry.cs",
                "Game.Core/Contracts/Content/ContentManifestLoaded.cs",
            ],
            "interfaces": [],
        },
        "08-FeatureSlice-Phase2-Events-ContentDriven.md": {
            "contracts": [
                "Game.Core/Contracts/Events/EventCatalogDefinition.cs",
                "Game.Core/Contracts/Events/EventDefinition.cs",
                "Game.Core/Contracts/Events/EventChainDefinition.cs",
                "Game.Core/Contracts/Events/EventCatalogLoaded.cs",
            ],
            "interfaces": [
                "Game.Core/Ports/IEventCatalog.cs",
            ],
        },
    }

    changed = []
    for filename, payload in updates.items():
        path = base / filename
        if not path.exists():
            raise FileNotFoundError(path)

        contracts_lines = "\n".join(f"- `{c}`" for c in payload["contracts"])
        interfaces_lines = "\n".join(f"- `{i}`" for i in payload["interfaces"])

        section = f"""\
## 契约定义（Phase2）

### 事件 / DTO
{contracts_lines}

### 接口契约
{interfaces_lines}
"""
        original = path.read_text(encoding="utf-8")
        updated = _ensure_section(original, section)
        if updated != original:
            path.write_text(updated, encoding="utf-8")
            changed.append(str(path.relative_to(root)).replace("\\", "/"))

    out_dir = root / "logs" / "ci" / datetime.date.today().isoformat() / "contracts-docs"
    out_dir.mkdir(parents=True, exist_ok=True)
    report_path = out_dir / "update_phase2_overlays_contract_refs.json"
    report_path.write_text(
        json.dumps(
            {
                "ts": datetime.datetime.utcnow().isoformat() + "Z",
                "changed": changed,
            },
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )
    print(f"Wrote {report_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
