using System.Collections.Generic;

namespace Game.Core.Contracts.Events;

/// <summary>
/// A deterministic chain of events that can be executed as a unit by the event engine.
/// </summary>
/// <remarks>
/// Refs: ADR-0004, ADR-0005.
/// Overlay: docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Events-ContentDriven.md
/// </remarks>
public sealed record EventChainDefinition(
    string ChainId,
    IReadOnlyList<string> EventTypes
);

