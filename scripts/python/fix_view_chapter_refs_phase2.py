import datetime
import json
import pathlib


def _load_list(path: pathlib.Path) -> list[dict]:
    obj = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(obj, list):
        raise TypeError(f"Expected list at top-level: {path}")
    return obj


def _write_list(path: pathlib.Path, items: list[dict]) -> None:
    path.write_text(json.dumps(items, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main() -> int:
    root = pathlib.Path(__file__).resolve().parents[2]
    path = root / ".taskmaster" / "tasks" / "tasks_gameplay.json"

    items = _load_list(path)

    # Adjust chapter_refs to align with ADR->CH implied map (per check_tasks_all_refs.py).
    # Only touch the reported tasks to avoid scope creep.
    updates = {
        "GM-0301": ["CH02", "CH07", "CH10"],
        "GM-0302": ["CH01", "CH04", "CH06", "CH07"],
        "GM-0304": ["CH01", "CH06", "CH07", "CH10"],
        "GM-0306": ["CH01", "CH02", "CH06", "CH07"],
    }

    changed = []
    for item in items:
        task_id = item.get("id")
        if task_id not in updates:
            continue
        before = item.get("chapter_refs") or []
        after = updates[task_id]
        if before != after:
            item["chapter_refs"] = after
            changed.append({"id": task_id, "before": before, "after": after})

    _write_list(path, items)

    out_dir = root / "logs" / "ci" / datetime.date.today().isoformat() / "task-mapping"
    out_dir.mkdir(parents=True, exist_ok=True)
    report_path = out_dir / "fix_view_chapter_refs_phase2.json"
    report_path.write_text(
        json.dumps(
            {
                "ts": datetime.datetime.utcnow().isoformat() + "Z",
                "file": str(path.relative_to(root)).replace("\\", "/"),
                "changed": changed,
            },
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )
    print(f"Wrote {report_path}")
    print(f"Changed items: {len(changed)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

