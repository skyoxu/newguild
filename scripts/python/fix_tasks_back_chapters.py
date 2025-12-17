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


NEW_TASK_IDS = {
    "NG-0023",
    "NG-0024",
    "NG-0025",
    "NG-0026",
    "NG-0027",
    "NG-0028",
    "NG-0029",
    "NG-0030",
    "NG-0031",
    "NG-0032",
    "NG-0033",
}


def main() -> None:
    root = Path(__file__).resolve().parents[2]
    tasks_path = root / ".taskmaster" / "tasks" / "tasks_back.json"

    if not tasks_path.exists():
        raise SystemExit(f"tasks_back.json not found: {tasks_path}")

    data = json.loads(tasks_path.read_text(encoding="utf-8"))

    changed = 0
    for task in data:
        task_id = task.get("id")
        if task_id not in NEW_TASK_IDS:
            continue

        expected_chapters: set[str] = set()
        for adr in task.get("adr_refs", []):
            expected_chapters.update(ADR_FOR_CH.get(adr, []))

        if not expected_chapters:
            continue

        current = set(task.get("chapter_refs", []))
        if current != expected_chapters:
            task["chapter_refs"] = sorted(expected_chapters)
            changed += 1

    if changed:
        tasks_path.write_text(
            json.dumps(data, indent=2, ensure_ascii=False),
            encoding="utf-8",
        )

    print(f"Updated chapter_refs for {changed} tasks in {tasks_path}")


if __name__ == "__main__":
    main()

