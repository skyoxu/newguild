---
PRD-ID: PRD-Guild-Manager
Title: 08 Contracts T2
ADR-Refs:
  - ADR-0003
  - ADR-0004
  - ADR-0005
Test-Refs:
  - Game.Core.Tests/
  - Tests.Godot/
---

# 08 Contracts T2

## Contract Inventory

- `Game.Core/Contracts/EventTypes.cs`
- `Game.Core/Contracts/`

## Field Constraints

Use explicit scalar/BCL types and deterministic IDs; avoid dynamic/object payload fields.

## Versioning and Migration

Contract changes require schema version bump and backward migration notes.

## Breaking Change Policy

Breaking changes require ADR update and acceptance criteria updates in overlay checklist.

## Local Validation

- `py -3 scripts/python/validate_contracts.py`
- `dotnet test Game.Core.Tests/Game.Core.Tests.csproj`
