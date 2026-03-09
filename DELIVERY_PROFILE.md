# DELIVERY_PROFILE (Fast Only)

## Scope

This repository is fixed to **fast mode** and does not expose multi-profile switching.

- Fixed profile name: `fast-ship`
- Default security posture: `host-safe`
- No `playable-ea` / `standard` toggle surface is introduced in this repository.

## Effective Defaults (Fast)

- Build
  - `warn_as_error = true`
- Test
  - coverage gate enabled
  - `coverage_lines_min = 70`
  - `coverage_branches_min = 60`
- Acceptance
  - `strict_adr_status = false`
  - `require_task_test_refs = true`
  - `require_executed_refs = false`
  - `require_headless_e2e = false`
  - `subtasks_coverage = warn`
  - `perf_p95_ms = 33`
- Gate bundle
  - `task_links_max_warnings = 200`
- LLM semantic gate
  - `max_needs_fix = 10`
  - `max_unknown = 10`

## Notes

- This file is the local SSoT for delivery strictness in this repository.
- If future changes require stricter release hardening, do it explicitly per command/CI step rather than adding profile switching.
