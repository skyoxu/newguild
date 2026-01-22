namespace Game.Core.Contracts.Events;

/// <summary>
/// A data-driven declaration for a domain event type.
/// </summary>
/// <remarks>
/// Refs: ADR-0004, ADR-0005.
/// Overlay: docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Events-ContentDriven.md
/// </remarks>
public sealed record EventDefinition(
    string EventType,
    string Title,
    string? Description,
    bool EnabledByDefault
);

