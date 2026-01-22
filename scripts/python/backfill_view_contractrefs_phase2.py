import datetime
import json
import pathlib


def _load_json(path: pathlib.Path):
    return json.loads(path.read_text(encoding="utf-8"))


def _write_json(path: pathlib.Path, data) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _collect_event_types(contracts_root: pathlib.Path) -> set[str]:
    event_types: set[str] = set()
    marker = "public const string EventType = "
    for cs in contracts_root.rglob("*.cs"):
        for line in cs.read_text(encoding="utf-8").splitlines():
            if marker not in line:
                continue
            left = line.split(marker, 1)[1].strip()
            if left.startswith("\"") and "\"" in left[1:]:
                event_types.add(left.split("\"", 2)[1])
    return event_types


def _sorted_unique(values: list[str]) -> list[str]:
    return sorted(set(values))


def _backfill_items(items: list[dict], mapping: dict[int, list[str]], known: set[str]) -> dict:
    changed = []
    skipped_unknown = []
    for item in items:
        raw = item.get("taskmaster_id")
        if not str(raw).isdigit():
            continue
        task_id = int(raw)
        if task_id not in mapping:
            continue

        requested = mapping[task_id]
        unknown = [x for x in requested if x not in known]
        if unknown:
            skipped_unknown.append({"taskmaster_id": task_id, "id": item.get("id"), "unknown": unknown})
            requested = [x for x in requested if x in known]

        new_refs = _sorted_unique(requested)
        old_refs = item.get("contractRefs") or []
        if old_refs != new_refs:
            item["contractRefs"] = new_refs
            changed.append({"taskmaster_id": task_id, "id": item.get("id"), "before": old_refs, "after": new_refs})

    return {"changed": changed, "skipped_unknown": skipped_unknown}


def main() -> int:
    root = pathlib.Path(__file__).resolve().parents[2]

    contracts_root = root / "Game.Core" / "Contracts"
    known = _collect_event_types(contracts_root)

    # Phase2 taskmaster IDs: 27-41 are in tasks_gameplay; 42-43 are in tasks_back.
    # Policy: only reference event types that already exist in Contracts (resolvable).
    phase2_contractrefs: dict[int, list[str]] = {
        27: ["core.content.manifest.loaded"],
        28: [],
        29: ["core.event_catalog.loaded"],
        30: [],
        31: [],
        32: [],
        33: [
            "core.ai.cycle.completed",
            "core.ai.cycle.started",
            "core.ai.ecosystem.step.completed",
            "core.ai.intent.issued",
            "core.content.manifest.loaded",
            "core.event_catalog.loaded",
            "core.game_turn.phase_changed",
            "core.game_turn.started",
            "core.game_turn.week_advanced",
            "core.guild.created",
            "core.guild.disbanded",
            "core.guild.member.joined",
            "core.guild.member.left",
            "core.guild.member.role_changed",
            "core.load.completed",
            "core.load.failed",
            "core.load.requested",
            "core.media.beat.triggered",
            "core.raid.resolved",
            "core.raid.scheduled",
            "core.recruitment.offer.presented",
            "core.recruitment.offer.resolved",
            "core.reputation.changed",
            "core.save.completed",
            "core.save.failed",
            "core.save.format.migration.applied",
            "core.save.requested",
            "core.social.interaction.triggered",
            "core.social.relationship.changed",
        ],
        34: ["core.raid.scheduled", "core.raid.resolved"],
        35: ["core.raid.resolved", "core.recruitment.offer.resolved", "core.media.beat.triggered"],
        36: [
            "core.guild.created",
            "core.raid.resolved",
            "core.recruitment.offer.resolved",
            "core.media.beat.triggered",
            "core.reputation.changed",
        ],
        37: ["core.raid.resolved", "core.recruitment.offer.resolved", "core.media.beat.triggered"],
        38: ["core.guild.member.role_changed", "core.guild.created"],
        39: ["core.guild.member.role_changed", "core.guild.created"],
        40: ["core.game.started"],
        41: ["core.content.manifest.loaded"],
        42: sorted([e for e in known if e.startswith("core.")]),
        43: [],
    }

    gameplay_path = root / ".taskmaster" / "tasks" / "tasks_gameplay.json"
    back_path = root / ".taskmaster" / "tasks" / "tasks_back.json"

    gameplay_obj = _load_json(gameplay_path)
    back_obj = _load_json(back_path)

    gameplay_items = gameplay_obj if isinstance(gameplay_obj, list) else gameplay_obj.get("tasks", [])
    back_items = back_obj if isinstance(back_obj, list) else back_obj.get("tasks", [])

    gameplay_report = _backfill_items(gameplay_items, phase2_contractrefs, known)
    back_report = _backfill_items(back_items, phase2_contractrefs, known)

    _write_json(gameplay_path, gameplay_items if isinstance(gameplay_obj, list) else {**gameplay_obj, "tasks": gameplay_items})
    _write_json(back_path, back_items if isinstance(back_obj, list) else {**back_obj, "tasks": back_items})

    out_dir = root / "logs" / "ci" / datetime.date.today().isoformat() / "contractrefs-backfill"
    out_dir.mkdir(parents=True, exist_ok=True)
    report_path = out_dir / "phase2_contractrefs_report.json"
    report_path.write_text(
        json.dumps(
            {
                "ts": datetime.datetime.utcnow().isoformat() + "Z",
                "known_event_types_count": len(known),
                "gameplay": gameplay_report,
                "back": back_report,
            },
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )
    print(f"Wrote {report_path}")
    print(
        f"Updated gameplay={len(gameplay_report['changed'])} back={len(back_report['changed'])} "
        f"skipped_unknown={len(gameplay_report['skipped_unknown']) + len(back_report['skipped_unknown'])}"
    )

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
