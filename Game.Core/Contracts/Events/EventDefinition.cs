using System;

namespace Game.Core.Contracts.Events;

/// <summary>
/// A data-driven declaration for a domain event type.
/// </summary>
/// <remarks>
/// Refs: ADR-0004, ADR-0005.
/// Overlay: docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Events-ContentDriven.md
/// </remarks>
public sealed record EventDefinition
{
    public EventDefinition(string eventType, string title, string? description, bool enabledByDefault)
    {
        EventTypeRules.Validate(eventType, nameof(eventType));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        EventType = eventType;
        Title = title;
        Description = description;
        EnabledByDefault = enabledByDefault;
    }

    public string EventType { get; }
    public string Title { get; }
    public string? Description { get; }
    public bool EnabledByDefault { get; }
}

