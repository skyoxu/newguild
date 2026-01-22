using System;

namespace Game.Core.Contracts.Events;

/// <summary>
/// Domain event: core.event_catalog.loaded
/// Emitted when the event catalog definitions (events/chains) are loaded and validated.
/// </summary>
/// <remarks>
/// Refs: ADR-0004 (event contracts), ADR-0005.
/// Overlay: docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Events-ContentDriven.md
/// </remarks>
public sealed record EventCatalogLoaded(
    string CatalogId,
    string SchemaVersion,
    int EventDefinitionCount,
    int EventChainCount,
    DateTimeOffset LoadedAt
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.event_catalog.loaded";
}

