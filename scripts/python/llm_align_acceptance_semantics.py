import argparse
import datetime
import json
import pathlib
from typing import Any


def _load_json(path: pathlib.Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def _write_json(path: pathlib.Path, obj: Any) -> None:
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _ensure_list(value: Any) -> list[str]:
    if value is None:
        return []
    if isinstance(value, list):
        return [str(x).strip() for x in value if str(x).strip()]
    if isinstance(value, str):
        v = value.strip()
        return [v] if v else []
    return [str(value).strip()]


def _normalize_line(line: str) -> str:
    return " ".join((line or "").strip().split())


def _contains_any(text: str, keywords: list[str]) -> bool:
    t = text.lower()
    return any(k.lower() in t for k in keywords)


def _render_acceptance_lines(
    task_id: int,
    title: str,
    layer: str,
    contract_refs: list[str],
    test_refs: list[str],
    artifact_refs: list[str],
) -> list[str]:
    lines: list[str] = []

    # Functional
    if title:
        lines.append(f"应完成：{title}")
    else:
        lines.append(f"应完成：任务 T{task_id} 的主要功能可用且可见。")

    # Tests
    if test_refs:
        lines.append(f"应有测试：至少包含并通过 test_refs 引用的测试（{len(test_refs)} 个）。")
    else:
        lines.append("应有测试：至少新增/更新 1 个确定性测试并在 test_refs 中引用。")

    # Contracts
    if contract_refs:
        # Minimum coverage definition: explicitly mention publish/consume.
        joined = ", ".join(contract_refs[:8]) + (" ..." if len(contract_refs) > 8 else "")
        lines.append(f"应对齐领域事件：publish/consume contractRefs 中列出的事件（例如 {joined}）。")

    # Wiring (UI/adapters)
    if layer in {"adapter", "ui", "scene"}:
        lines.append("应接线：对应屏幕/入口可达，关键按钮可点击（无透明层/MouseFilter 阻挡），状态可见。")
        if contract_refs:
            lines.append("应接线：UI 的显示/状态变化应由上述领域事件驱动（避免接线漏了但回链看似齐全）。")

    # Observability / Evidence
    if artifact_refs:
        lines.append("应留证：按 artifactRefs 产出 logs/** 工件（可复现、可追溯）。")
    else:
        lines.append("应留证：在 logs/** 下产出最小审计/门禁证据（可复现、可追溯）。")

    # Keep acceptance list concise (target 3-6).
    # If we generated too many lines, keep the most valuable ones.
    max_lines = 6
    if len(lines) > max_lines:
        # Always keep first two (functional/tests), then contracts/wiring, then evidence.
        keep: list[str] = []
        keep.extend(lines[:2])
        for candidate in lines[2:]:
            if len(keep) >= max_lines:
                break
            keep.append(candidate)
        lines = keep

    return lines


def _align_acceptance(
    existing: list[str],
    generated: list[str],
    contract_refs: list[str],
    layer: str,
) -> tuple[list[str], dict]:
    before = list(existing)
    normalized_before = {_normalize_line(x) for x in before}

    after = list(before)
    for line in generated:
        n = _normalize_line(line)
        if not n or n in normalized_before:
            continue
        after.append(line)
        normalized_before.add(n)

    # Enforce minimal semantics: if contractRefs exists, acceptance must mention publish/consume/event.
    if contract_refs:
        blob = " ".join(after)
        if not _contains_any(blob, ["publish", "emit", "consume", "subscribe", "事件", "event", "DomainEvent", "type"]):
            after.append("应对齐领域事件：明确说明 publish/consume 的事件时机与可观测结果。")

    # If adapter/UI/scene, acceptance must mention clickability/wiring.
    if layer in {"adapter", "ui", "scene"}:
        blob = " ".join(after)
        if not _contains_any(blob, ["点击", "click", "button", "按钮", "可点击", "MouseFilter", "信号", "signal"]):
            after.append("应接线：关键 UI 元素可点击且无透明阻挡；必要时有 GdUnit4 冒烟覆盖。")

    # Ensure at least 3 acceptance lines.
    if len(after) < 3:
        after.append("应可复现：本地按测试路径操作可得到一致结果。")

    return after, {"before": before, "after": after}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--task-id-start", type=int, default=27)
    parser.add_argument("--task-id-end", type=int, default=43)
    args = parser.parse_args()

    root = pathlib.Path(__file__).resolve().parents[2]
    tasks_path = root / ".taskmaster" / "tasks" / "tasks.json"
    gameplay_path = root / ".taskmaster" / "tasks" / "tasks_gameplay.json"
    back_path = root / ".taskmaster" / "tasks" / "tasks_back.json"

    tasks_obj = _load_json(tasks_path)
    gameplay_obj = _load_json(gameplay_path)
    back_obj = _load_json(back_path)

    if not isinstance(gameplay_obj, list) or not isinstance(back_obj, list):
        raise TypeError("Expected tasks_gameplay.json and tasks_back.json to be top-level lists.")

    master_tasks = tasks_obj.get("master", {}).get("tasks", [])
    if not isinstance(master_tasks, list):
        raise TypeError("Expected tasks.json master.tasks to be a list.")

    master_by_id = {int(t["id"]): t for t in master_tasks if str(t.get("id", "")).isdigit()}

    def _find_view(task_id: int) -> dict | None:
        for item in gameplay_obj:
            if str(item.get("taskmaster_id", "")).isdigit() and int(item["taskmaster_id"]) == task_id:
                return item
        for item in back_obj:
            if str(item.get("taskmaster_id", "")).isdigit() and int(item["taskmaster_id"]) == task_id:
                return item
        return None

    changed: list[dict] = []
    unchanged: list[int] = []
    missing: list[int] = []

    for task_id in range(args.task_id_start, args.task_id_end + 1):
        master = master_by_id.get(task_id)
        view = _find_view(task_id)
        if not master or not view:
            missing.append(task_id)
            continue

        title = (master.get("title") or view.get("title") or "").strip()
        layer = (view.get("layer") or "").strip()
        contract_refs = _ensure_list(view.get("contractRefs"))
        test_refs = _ensure_list(view.get("test_refs"))
        artifact_refs = _ensure_list(view.get("artifactRefs"))

        existing_acceptance = _ensure_list(view.get("acceptance"))
        generated = _render_acceptance_lines(task_id, title, layer, contract_refs, test_refs, artifact_refs)
        after, diff = _align_acceptance(existing_acceptance, generated, contract_refs, layer)

        if after != existing_acceptance:
            view["acceptance"] = after
            changed.append(
                {
                    "task_id": task_id,
                    "view_id": view.get("id"),
                    "before_len": len(diff["before"]),
                    "after_len": len(diff["after"]),
                }
            )
        else:
            unchanged.append(task_id)

    _write_json(gameplay_path, gameplay_obj)
    _write_json(back_path, back_obj)

    out_dir = root / "logs" / "ci" / datetime.date.today().isoformat() / "acceptance-align"
    out_dir.mkdir(parents=True, exist_ok=True)
    report_path = out_dir / "align_acceptance_semantics.json"
    _write_json(
        report_path,
        {
            "ts": datetime.datetime.utcnow().isoformat() + "Z",
            "range": {"start": args.task_id_start, "end": args.task_id_end},
            "changed": changed,
            "unchanged": unchanged,
            "missing": missing,
        },
    )
    print(f"Wrote {report_path}")
    print(f"changed={len(changed)} unchanged={len(unchanged)} missing={len(missing)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
