---
PRD-ID: PRD-Guild-Manager
Title: 08 Feature Slice T2 Core Loop
ADR-Refs:
  - ADR-0003
  - ADR-0004
  - ADR-0005
Test-Refs:
  - Game.Core.Tests/
  - Tests.Godot/
---

# 08 Feature Slice T2 Core Loop

## Runtime Boundary

Core loop logic stays in `Game.Core/` and engine adapters stay in `Scripts/Adapters/`.

## Domain Entities

- TickState
- CycleSnapshot
- SimulationCheckpoint

## Event Contracts

- `Game.Core/Contracts/EventTypes.cs`
- `Game.Core/Contracts/`

## Runtime State Machine

Define deterministic progression: Init -> Running -> Paused -> Completed.

## Failure Paths

Document invalid state transition, stale snapshot load, and corrupted input fallback.

## Acceptance Anchors

- `ACC:T53.1`
- `ACC:T60.1`

## Task Mapping

- T53, T60, T78, T94
