import json
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, Iterable, List, Optional, Tuple


@dataclass(frozen=True)
class ViewMapping:
    view_file: str
    view_id: str
    taskmaster_id: str
    view_task: Dict[str, Any]


def _read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def _write_json(path: Path, data: Any) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _join_csv(values: Iterable[str]) -> str:
    return ", ".join(values)


def _as_str_list(value: Any) -> List[str]:
    if value is None:
        return []
    if isinstance(value, list):
        return [str(x).strip() for x in value if str(x).strip()]
    if isinstance(value, str):
        return [v.strip() for v in value.split(";") if v.strip()]
    return [str(value).strip()]


def _dedup_sorted(values: Iterable[str]) -> List[str]:
    return sorted({v for v in values if v})


def _find_details_value(details: str, prefix: str) -> Optional[str]:
    for line in (details or "").splitlines():
        if line.startswith(prefix):
            return line[len(prefix) :].strip()
    return None


def _extract_semicolon_list(details: str, prefix: str) -> List[str]:
    raw = _find_details_value(details, prefix)
    if not raw:
        return []
    return [p.strip() for p in raw.split(";") if p.strip()]


def _repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


def _log_dir(root: Path) -> Path:
    d = datetime.now(timezone.utc).astimezone().date().isoformat()
    out_dir = root / "logs" / "ci" / d / "task-mapping"
    out_dir.mkdir(parents=True, exist_ok=True)
    return out_dir


def _build_view_index(tasks: List[Dict[str, Any]], view_file: str) -> Tuple[Dict[str, ViewMapping], List[str]]:
    by_taskmaster: Dict[str, ViewMapping] = {}
    dupes: List[str] = []
    for t in tasks:
        taskmaster_id = t.get("taskmaster_id")
        exported = bool(t.get("taskmaster_exported"))
        if taskmaster_id is None or not exported:
            continue
        master_id = str(taskmaster_id)
        view_id = str(t.get("id") or "")
        if master_id in by_taskmaster:
            dupes.append(master_id)
            continue
        by_taskmaster[master_id] = ViewMapping(
            view_file=view_file,
            view_id=view_id,
            taskmaster_id=master_id,
            view_task=t,
        )
    return by_taskmaster, dupes


def _compare_sets(label: str, a: List[str], b: List[str]) -> Dict[str, Any]:
    a_set = set(a)
    b_set = set(b)
    return {
        "label": label,
        "equal": a_set == b_set,
        "only_in_master": _dedup_sorted(a_set - b_set),
        "only_in_view": _dedup_sorted(b_set - a_set),
    }


def main() -> int:
    root = _repo_root()
    tasks_json_path = root / ".taskmaster" / "tasks" / "tasks.json"
    tasks_back_path = root / ".taskmaster" / "tasks" / "tasks_back.json"
    tasks_gameplay_path = root / ".taskmaster" / "tasks" / "tasks_gameplay.json"

    master_doc = _read_json(tasks_json_path)
    master_tasks: List[Dict[str, Any]] = master_doc.get("master", {}).get("tasks", [])

    view_back: List[Dict[str, Any]] = _read_json(tasks_back_path)
    view_gameplay: List[Dict[str, Any]] = _read_json(tasks_gameplay_path)

    back_index, back_dupes = _build_view_index(view_back, str(tasks_back_path))
    gameplay_index, gameplay_dupes = _build_view_index(view_gameplay, str(tasks_gameplay_path))

    mapped: List[Dict[str, Any]] = []
    unmapped: List[str] = []

    for t in master_tasks:
        master_id = str(t.get("id"))
        mapping = back_index.get(master_id) or gameplay_index.get(master_id)
        if not mapping:
            unmapped.append(master_id)
            continue

        view_task = mapping.view_task

        adr_master = _dedup_sorted(_as_str_list(t.get("adrRefs")))
        adr_view = _dedup_sorted(_as_str_list(view_task.get("adr_refs")))

        arch_master = _dedup_sorted(_as_str_list(t.get("archRefs")))
        arch_view = _dedup_sorted(_as_str_list(view_task.get("chapter_refs")))

        overlay_master = str(t.get("overlay") or "").strip()
        overlay_refs_view = _dedup_sorted(_as_str_list(view_task.get("overlay_refs")))

        overlay_result: Dict[str, Any] = {
            "label": "overlay",
            "master": overlay_master,
            "view_overlay_refs_count": len(overlay_refs_view),
            "master_in_view_overlay_refs": (overlay_master in overlay_refs_view) if overlay_master else False,
            "view_overlay_refs_empty": len(overlay_refs_view) == 0,
        }

        # Extra consistency check: details lines if present
        details = str(t.get("details") or "")
        details_adr = _dedup_sorted(_extract_semicolon_list(details, "ADR Refs:"))
        details_ch = _dedup_sorted(_extract_semicolon_list(details, "Chapters:"))
        details_overlays = _dedup_sorted(_extract_semicolon_list(details, "Overlays:"))

        mapped.append(
            {
                "master_id": master_id,
                "master_title": t.get("title"),
                "view_file": mapping.view_file,
                "view_id": mapping.view_id,
                "adr": _compare_sets("adrRefs vs adr_refs", adr_master, adr_view),
                "arch": _compare_sets("archRefs vs chapter_refs", arch_master, arch_view),
                "overlay": overlay_result,
                "details_consistency": {
                    "details_adr_equal_to_master_adrRefs": set(details_adr) == set(adr_master),
                    "details_ch_equal_to_master_archRefs": set(details_ch) == set(arch_master),
                    "details_overlays": details_overlays,
                },
            }
        )

    summary = {
        "ts": datetime.now(timezone.utc).isoformat(),
        "files": {
            "tasks_json": str(tasks_json_path),
            "tasks_back": str(tasks_back_path),
            "tasks_gameplay": str(tasks_gameplay_path),
        },
        "counts": {
            "master_tasks_total": len(master_tasks),
            "mapped_total": len(mapped),
            "unmapped_total": len(unmapped),
            "view_dupes": {"tasks_back": back_dupes, "tasks_gameplay": gameplay_dupes},
        },
        "drift_counts": {
            "adr_mismatch": sum(1 for x in mapped if not x["adr"]["equal"]),
            "arch_mismatch": sum(1 for x in mapped if not x["arch"]["equal"]),
            "overlay_mismatch": sum(1 for x in mapped if not x["overlay"]["master_in_view_overlay_refs"]),
            "overlay_view_empty": sum(1 for x in mapped if x["overlay"]["view_overlay_refs_empty"]),
        },
    }

    report = {"summary": summary, "mapped": mapped, "unmapped": _dedup_sorted(unmapped)}

    out_dir = _log_dir(root)
    json_path = out_dir / "master-view-ref-drift.json"
    txt_path = out_dir / "master-view-ref-drift.txt"

    _write_json(json_path, report)

    lines: List[str] = []
    lines.append("Master <-> View reference drift report")
    lines.append(f"GeneratedAtUtc: {summary['ts']}")
    lines.append("")
    lines.append(
        "Counts: "
        + _join_csv(
            [
                f"master={summary['counts']['master_tasks_total']}",
                f"mapped={summary['counts']['mapped_total']}",
                f"unmapped={summary['counts']['unmapped_total']}",
            ]
        )
    )
    lines.append(
        "Drift: "
        + _join_csv(
            [
                f"adr_mismatch={summary['drift_counts']['adr_mismatch']}",
                f"arch_mismatch={summary['drift_counts']['arch_mismatch']}",
                f"overlay_mismatch={summary['drift_counts']['overlay_mismatch']}",
                f"overlay_view_empty={summary['drift_counts']['overlay_view_empty']}",
            ]
        )
    )
    lines.append("")

    for item in mapped:
        adr_ok = item["adr"]["equal"]
        arch_ok = item["arch"]["equal"]
        overlay_ok = item["overlay"]["master_in_view_overlay_refs"]
        if adr_ok and arch_ok and overlay_ok:
            continue
        lines.append(f"- master_id={item['master_id']} view_id={item['view_id']} view_file={item['view_file']}")
        if not adr_ok:
            lines.append(f"  adr only_in_master={item['adr']['only_in_master']}")
            lines.append(f"  adr only_in_view={item['adr']['only_in_view']}")
        if not arch_ok:
            lines.append(f"  arch only_in_master={item['arch']['only_in_master']}")
            lines.append(f"  arch only_in_view={item['arch']['only_in_view']}")
        if not overlay_ok:
            lines.append(f"  overlay master={item['overlay']['master']!r}")
            lines.append(f"  overlay view_refs_count={item['overlay']['view_overlay_refs_count']}")
            lines.append(f"  overlay view_refs_empty={item['overlay']['view_overlay_refs_empty']}")

    txt_path.write_text("\n".join(lines) + "\n", encoding="utf-8")

    print(f"[REPORT] {json_path}")
    print(f"[REPORT] {txt_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

