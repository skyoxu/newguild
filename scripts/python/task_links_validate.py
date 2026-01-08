from pathlib import Path

import check_tasks_back_references
import check_tasks_all_refs
import validate_view_ref_semantics


def main() -> None:
    root = Path(__file__).resolve().parents[2]

    # 1) 兼容旧行为：只校验 NG-0023..NG-0033 新任务
    ok_new = check_tasks_back_references.run_check(root)

    # 2) 全量检查：对所有 tasks_back.json/tasks_gameplay.json 做 ADR/CH/Overlay 校验
    ok_all = check_tasks_all_refs.run_check_all(root)

    # 3) View field semantics: keep test_refs strict, artifactRefs allows placeholders.
    ok_semantics = validate_view_ref_semantics.main() == 0

    if not (ok_new and ok_all and ok_semantics):
        raise SystemExit(1)


if __name__ == "__main__":
    main()
