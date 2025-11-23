from pathlib import Path

import check_tasks_back_references


def main() -> None:
    root = Path(__file__).resolve().parents[2]
    ok = check_tasks_back_references.run_check(root)
    if not ok:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
