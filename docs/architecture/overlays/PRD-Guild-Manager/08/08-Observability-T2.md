---
PRD-ID: PRD-Guild-Manager
Title: 08 Observability T2
ADR-Refs:
  - ADR-0003
  - ADR-0004
  - ADR-0005
Test-Refs:
  - Game.Core.Tests/
  - Tests.Godot/
---

# 08 Observability T2

## Artifact Naming

Use stable names with date partitioning under `logs/ci/<YYYY-MM-DD>/`.

## Mandatory JSON Fields

Include task_id, status, run_id, started_at, finished_at, and evidence_path.

## Gate Failure Handling

On hard gate failure, stop pipeline and publish failure summary with root cause pointers.

## Release Health Linkage

Tie quality gate outputs to release health checks and promote only green runs.
