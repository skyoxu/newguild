#!/usr/bin/env python3
"""
Sanguo/GameLoop contracts checks.

This currently reuses the baseline GameLoop contract assertions from
scripts/python/check_gameloop_contracts.py.

Why:
  - Keep sc-analyze and sc-acceptance-check scripts stable while the domain naming
    converges (e.g., "GameLoop" vs "Sanguo").
  - Avoid "missing script" noise in soft checks.
"""

from __future__ import annotations

from check_gameloop_contracts import main as _main


if __name__ == "__main__":  # pragma: no cover
    raise SystemExit(_main())

