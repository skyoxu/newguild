# ADR-0021: C# Domain Layer Architecture

- Status: Proposed
- Context: Migration Phase-2; CH05 data/ports and CH06 runtime view; enforce testable, engine-agnostic core
- Decision: Structure Game.Core as pure C# (no Godot dependency), using ports/interfaces (ITime, IInput, IDataStore, ILogger, etc.); inject adapters from Game.Godot; keep events/DTO/contracts under Game.Core/Contracts/<Module>/ as SSoT (per ADR-0020)
- Consequences: Faster TDD cycles; deterministic unit tests; clear boundaries for adapters; scene glue remains thin
- References: ADR-0020-contract-location-standardization, docs/migration/Phase-4-Domain-Layer.md, docs/migration/Phase-5-Adapter-Layer.md, docs/architecture/base/05-data-models-and-storage-ports-v2.md

