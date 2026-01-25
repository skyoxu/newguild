using System;
using System.Collections.Generic;
using Game.Core.Contracts.Events;
using Game.Core.Ports;

namespace Game.Core.Services;

/// <summary>
/// Canonical in-core implementation of <see cref="IEventCatalog"/> backed by an <see cref="EventCatalogDefinition"/>.
/// </summary>
/// <remarks>
/// Refs: ADR-0004 (event contracts), ADR-0005.
/// Overlay: docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Events-ContentDriven.md
/// </remarks>
public sealed class EventCatalog : IEventCatalog
{
    private readonly Dictionary<string, bool> _enabledByType;

    public EventCatalog()
        : this(new Dictionary<string, bool>(StringComparer.Ordinal))
    {
    }

    public EventCatalog(EventCatalogDefinition definition)
        : this(ToMap(definition))
    {
    }

    private EventCatalog(Dictionary<string, bool> enabledByType)
    {
        _enabledByType = enabledByType;
    }

    public bool IsEventEnabled(string eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            return false;

        return _enabledByType.TryGetValue(eventType, out var enabled) && enabled;
    }

    private static Dictionary<string, bool> ToMap(EventCatalogDefinition definition)
    {
        if (definition is null)
            throw new ArgumentNullException(nameof(definition));

        var map = new Dictionary<string, bool>(StringComparer.Ordinal);
        if (definition.Events is null)
            return map;

        foreach (var def in definition.Events)
        {
            if (def is null)
                continue;
            if (string.IsNullOrWhiteSpace(def.EventType))
                throw new ArgumentException("Event type must not be null or whitespace.", nameof(definition));

            if (!map.TryAdd(def.EventType, def.EnabledByDefault))
                throw new ArgumentException($"Duplicate event type: '{def.EventType}'.", nameof(definition));
        }

        return map;
    }
}
