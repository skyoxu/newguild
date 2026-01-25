using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core.Contracts.Events;

/// <summary>
/// A deterministic chain of events that can be executed as a unit by the event engine.
/// </summary>
/// <remarks>
/// Refs: ADR-0004, ADR-0005.
/// Overlay: docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Events-ContentDriven.md
/// </remarks>
public sealed record EventChainDefinition
{
    public EventChainDefinition(string chainId, IReadOnlyList<string> eventTypes)
    {
        if (string.IsNullOrWhiteSpace(chainId))
            throw new ArgumentException("Chain id is required.", nameof(chainId));
        if (eventTypes is null)
            throw new ArgumentNullException(nameof(eventTypes));
        if (eventTypes.Count == 0)
            throw new ArgumentException("Event types must not be empty.", nameof(eventTypes));

        var copy = eventTypes.ToArray();

        var duplicates = copy
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .GroupBy(e => e, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicates.Count > 0)
            throw new ArgumentException("Event types must be unique.", nameof(eventTypes));

        foreach (var eventType in copy)
        {
            EventTypeRules.Validate(eventType, nameof(eventTypes));
        }

        ChainId = chainId;
        EventTypes = Array.AsReadOnly(copy);
    }

    public string ChainId { get; }
    public IReadOnlyList<string> EventTypes { get; }
}

