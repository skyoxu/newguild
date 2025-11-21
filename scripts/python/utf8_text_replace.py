import sys
from pathlib import Path


def is_text_candidate(path: Path) -> bool:
    """Return True if the file is likely a UTF-8 text file we want to touch.

    We keep this conservative to avoid corrupting binary assets.
    """

    if path.is_dir():
        return False

    # Skip typical non-text and build artifacts explicitly.
    binary_suffixes = {
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".mp3",
        ".wav",
        ".ogg",
        ".pck",
        ".exe",
        ".dll",
        ".bin",
        ".import",
    }

    if path.suffix.lower() in binary_suffixes:
        return False

    # Positive list for typical text files in this repo.
    text_suffixes = {
        ".md",
        ".txt",
        ".sql",
        ".cs",
        ".gd",
        ".py",
        ".ps1",
        ".yml",
        ".yaml",
        ".json",
        ".xml",
        ".ini",
        ".cfg",
    }

    if path.suffix.lower() in text_suffixes:
        return True

    # Fallback: treat extension-less small files as text candidates.
    try:
        if path.suffix == "" and path.stat().st_size < 512 * 1024:
            return True
    except OSError:
        return False

    return False


def should_skip(path: Path) -> bool:
    """Skip backup and VCS/tooling directories."""

    parts = {p.lower() for p in path.parts}
    if "backup" in parts:
        return True
    if "logs" in parts:
        return True
    if ".git" in parts:
        return True
    return False


def replace_in_file(path: Path, old: str, new: str) -> bool:
    """Replace occurrences of `old` with `new` in a UTF-8 text file.

    Returns True if the file was modified.
    """

    try:
        content = path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        # Respect repository rule: UTF-8 only. If a file is not UTF-8,
        # we leave it untouched and report to stderr for manual follow-up.
        print(f"[skip-non-utf8] {path}", file=sys.stderr)
        return False
    except OSError as exc:
        print(f"[error-read] {path}: {exc}", file=sys.stderr)
        return False

    if old not in content:
        return False

    updated = content.replace(old, new)
    if updated == content:
        return False

    try:
        path.write_text(updated, encoding="utf-8")
    except OSError as exc:
        print(f"[error-write] {path}: {exc}", file=sys.stderr)
        return False

    print(f"[updated] {path}")
    return True


def main(argv: list[str]) -> int:
    """Generic in-repo UTF-8 text replacement helper.

    Usage (PowerShell):
        py -3 scripts/python/utf8_text_replace.py . --old=foo --new=bar

    The "foo" and "bar" values are examples only; callers must
    always provide explicit old/new values.
    """

    root = Path(".").resolve()
    old: str | None = None
    new: str | None = None

    for arg in argv[1:]:
        if arg.startswith("--old="):
            old = arg.split("=", 1)[1]
        elif arg.startswith("--new="):
            new = arg.split("=", 1)[1]
        else:
            # Treat the first non-flag argument as an explicit root.
            if arg and not arg.startswith("-"):
                root = Path(arg).resolve()

    if old is None or new is None:
        print("Usage: utf8_text_replace.py <root> --old=<old> --new=<new>", file=sys.stderr)
        return 1

    if not root.exists():
        print(f"Root path does not exist: {root}", file=sys.stderr)
        return 1

    print(f"Scanning from root: {root}")
    print(f"Replacing '{old}' -> '{new}' (excluding backup/ and .git/)...")

    changed = 0
    for path in root.rglob("*"):
        if should_skip(path):
            continue
        if not is_text_candidate(path):
            continue
        if replace_in_file(path, old, new):
            changed += 1

    print(f"Done. Files modified: {changed}")
    return 0


if __name__ == "__main__":  # pragma: no cover - maintenance utility
    raise SystemExit(main(sys.argv))

