---
PRD-ID: PRD-Guild-Manager
Title: 08 Testing T2
ADR-Refs:
  - ADR-0003
  - ADR-0004
  - ADR-0005
Test-Refs:
  - Game.Core.Tests/
  - Tests.Godot/
---

# 08 Testing T2

## Test Layers

- Unit: `Game.Core.Tests/`
- Integration: `Tests.Godot/`

## Requirement-to-Test Mapping

Each ACC row maps to a concrete unit or headless integration test artifact.

## Test Execution Matrix (Windows)

- `py -3 scripts/sc/build.py tdd --task-id 54 --stage green`
- `dotnet test Game.Core.Tests/Game.Core.Tests.csproj`

## Evidence Policy

Store deterministic artifacts under `logs/ci/<YYYY-MM-DD>/` and link from reviews.
