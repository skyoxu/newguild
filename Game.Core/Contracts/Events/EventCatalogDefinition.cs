using System.Collections.Generic;

namespace Game.Core.Contracts.Events;

/// <summary>
/// A data-driven catalog that declares available domain events and optional event chains.
/// </summary>
/// <remarks>
/// Refs: ADR-0004 (event contracts), ADR-0005.
/// Overlay: docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Events-ContentDriven.md
/// </remarks>
public sealed record EventCatalogDefinition(
    string CatalogId,
    string SchemaVersion,
    IReadOnlyList<EventDefinition> Events,
    IReadOnlyList<EventChainDefinition> Chains
);

