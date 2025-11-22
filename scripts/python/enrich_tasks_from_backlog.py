import json
from pathlib import Path


def enrich_tasks(src_path: Path, dest_path: Path) -> None:
    """Enrich tasks.json with additional fields inspired by tasks_back.json.

    The goal is to keep existing content intact while adding schema fields
    used by Taskmaster in this Godot+C# template (story_id, layer, adr_refs,
    chapter_refs, overlay_refs, depends_on, owner, test_refs, acceptance,
    test_strategy).
    """

    raw = src_path.read_text(encoding="utf-8")
    data = json.loads(raw)

    # The existing file is structured as { "master": { "tasks": [ … ] } }
    tasks = data.get("master", {}).get("tasks", [])

    enriched: list[dict] = []

    for task in tasks:
        t = dict(task)

        # Generic mapping to PRD: this backlog originates from the Guild Manager PRD.
        if "story_id" not in t:
            t["story_id"] = "PRD-GUILD-MANAGER"

        # Layer is not obvious from title; mark as to-be-determined so humans
        # or higher-level tooling can refine it later.
        if "layer" not in t:
            t["layer"] = "tbd"

        # Attach baseline ADR for Godot+C# tech stack if missing.
        if "adr_refs" not in t:
            t["adr_refs"] = ["ADR-0018"]

        if "chapter_refs" not in t:
            t["chapter_refs"] = []

        if "overlay_refs" not in t:
            t["overlay_refs"] = []

        # Map existing numeric dependencies into a generic depends_on field.
        if "depends_on" not in t:
            deps = t.get("dependencies")
            if isinstance(deps, list):
                t["depends_on"] = deps
            else:
                t["depends_on"] = []

        # Ensure labels is always a list.
        if "labels" not in t:
            t["labels"] = []

        # Default owner to architecture for now.
        if "owner" not in t:
            t["owner"] = "architecture"

        # Normalise test strategy/test refs naming to snake_case variants
        # used in tasks_back.json, while preserving original fields.
        if "test_strategy" not in t:
            ts = t.get("testStrategy")
            if isinstance(ts, list):
                t["test_strategy"] = ts
            elif isinstance(ts, str):
                t["test_strategy"] = [ts]
            else:
                t["test_strategy"] = []

        if "test_refs" not in t:
            tr = t.get("testRefs")
            if isinstance(tr, list):
                t["test_refs"] = tr
            else:
                t["test_refs"] = []

        if "acceptance" not in t:
            t["acceptance"] = []

        enriched.append(t)

    # For the enriched variant we output a plain list of tasks, matching the
    # simpler structure used by tasks_back.json.
    dest_path.write_text(
        json.dumps(enriched, indent=2, ensure_ascii=False),
        encoding="utf-8",
    )


def main() -> None:
    root = Path(__file__).resolve().parents[2]
    src = root / ".taskmaster" / "tasks" / "tasks.json"
    dest = root / ".taskmaster" / "tasks" / "tasks_newguild.json"

    if not src.exists():
        raise SystemExit(f"Source tasks.json not found: {src}")

    enrich_tasks(src, dest)
    print(f"Enriched tasks written to: {dest}")


if __name__ == "__main__":
    main()

