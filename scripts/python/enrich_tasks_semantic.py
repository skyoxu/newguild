import json
from pathlib import Path


ADR_FOR_CH = {
    "ADR-0002": ["CH02"],
    "ADR-0019": ["CH02"],
    "ADR-0003": ["CH03"],
    "ADR-0004": ["CH04"],
    "ADR-0006": ["CH05"],
    "ADR-0007": ["CH05", "CH06"],
    "ADR-0005": ["CH07"],
    "ADR-0011": ["CH07", "CH10"],
    "ADR-0008": ["CH10"],
    "ADR-0015": ["CH09"],
    "ADR-0018": ["CH01", "CH06", "CH07"],
    "ADR-0024": ["CH06", "CH07"],
    "ADR-0023": ["CH05"],
}


def infer_layer(title: str, description: str) -> str:
    text = f"{title} {description}".lower()

    if any(k in text for k in ["ci ", "pipeline", "workflow", "github actions", "quality gate", "smoke", "gdunit", "xunit", "coverage", "build script"]):
        return "ci"
    if any(k in text for k in ["godot", "scene", "hud", "ui", "control", "screen"]):
        # Default Godot-facing tasks到 adapter/scene，两者再由人工细分
        return "adapter"
    if any(k in text for k in ["core", "event engine", "ai coordinator", "domain", "model", "repository", "game loop", "simulation", "economy", "system"]):
        return "core"
    if any(k in text for k in ["doc", "documentation", "prd", "adr", "overlay", "acceptance checklist"]):
        return "docs"
    return "tbd"


def infer_adrs(title: str, description: str, existing: list[str]) -> list[str]:
    text = f"{title} {description}".lower()
    adrs = set(existing or [])

    # 确保 Godot+C# 技术栈 ADR 始终存在
    adrs.add("ADR-0018")

    if any(k in text for k in ["event", "cloudevent", "signal", "event bus"]):
        adrs.add("ADR-0004")
    if any(k in text for k in ["security", "secure", "权限", "sandbox", "filesystem", "file", "os.execute"]):
        adrs.add("ADR-0019")
        adrs.add("ADR-0005")
    if any(k in text for k in ["sqlite", "database", "db ", "persistence", "data store"]):
        adrs.add("ADR-0006")
    if any(k in text for k in ["adapter", "port", "ports-adapters", "injection"]):
        adrs.add("ADR-0007")
    if any(k in text for k in ["observability", "sentry", "logging", "release health"]):
        adrs.add("ADR-0003")
    if any(k in text for k in ["quality gate", "ci ", "coverage", "测试", "测试策略"]):
        adrs.add("ADR-0005")
        adrs.add("ADR-0020")
    if any(k in text for k in ["performance", "p95", "perf"]):
        adrs.add("ADR-0015")
    if any(k in text for k in ["windows", "export", "build", "release", "tag", "workflow"]):
        adrs.add("ADR-0011")
        adrs.add("ADR-0008")

    return sorted(adrs)


def infer_chapters(adr_refs: list[str]) -> list[str]:
    chapters: set[str] = set()
    for adr in adr_refs:
        chapters.update(ADR_FOR_CH.get(adr, []))
    return sorted(chapters)


def infer_overlays(title: str, description: str) -> list[str]:
    text = f"{title} {description}".lower()
    overlays: list[str] = []

    if "cloudevent" in text or "cloudevents" in text:
        overlays.append("docs/architecture/overlays/PRD-Guild-Manager/08/08-Contracts-CloudEvent.md")
        overlays.append("docs/architecture/overlays/PRD-Guild-Manager/08/08-Contracts-CloudEvents-Core.md")
    if "guild" in text and "event" in text:
        overlays.append("docs/architecture/overlays/PRD-Guild-Manager/08/08-Contracts-Guild-Manager-Events.md")
    if "preload" in text or "whitelist" in text:
        overlays.append("docs/architecture/overlays/PRD-Guild-Manager/08/08-Contracts-Preload-Whitelist.md")
    if "quality" in text and "metric" in text:
        overlays.append("docs/architecture/overlays/PRD-Guild-Manager/08/08-Contracts-Quality-Metrics.md")
    if "security" in text or "安全" in text:
        overlays.append("docs/architecture/overlays/PRD-Guild-Manager/08/08-Contracts-Security.md")
    if "guild manager" in text or "公会管理" in text:
        overlays.append("docs/architecture/overlays/PRD-Guild-Manager/08/08-功能纵切-公会管理器.md")
        overlays.append("docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md")

    # 去重保持顺序
    seen: set[str] = set()
    uniq: list[str] = []
    for p in overlays:
        if p not in seen:
            seen.add(p)
            uniq.append(p)
    return uniq


def main() -> None:
    root = Path(__file__).resolve().parents[2]
    tasks_path = root / ".taskmaster" / "tasks" / "tasks_newguild.json"

    if not tasks_path.exists():
        raise SystemExit(f"tasks_newguild.json not found: {tasks_path}")

    data = json.loads(tasks_path.read_text(encoding="utf-8"))

    for task in data:
        title = str(task.get("title", ""))
        description = str(task.get("description", ""))

        # layer
        task["layer"] = infer_layer(title, description)

        # adr_refs
        existing_adrs = task.get("adr_refs") or []
        task["adr_refs"] = infer_adrs(title, description, existing_adrs)

        # chapter_refs derived from ADRs
        task["chapter_refs"] = infer_chapters(task["adr_refs"])

        # overlay_refs
        overlays = task.get("overlay_refs") or []
        inferred_overlays = infer_overlays(title, description)
        # merge
        merged_overlays: list[str] = []
        seen_paths: set[str] = set()
        for p in overlays + inferred_overlays:
            if p not in seen_paths:
                seen_paths.add(p)
                merged_overlays.append(p)
        task["overlay_refs"] = merged_overlays

    tasks_path.write_text(
        json.dumps(data, indent=2, ensure_ascii=False),
        encoding="utf-8",
    )

    print(f"Updated semantic fields for {len(data)} tasks in {tasks_path}")


if __name__ == "__main__":
    main()

