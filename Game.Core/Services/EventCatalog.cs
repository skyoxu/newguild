using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
    private const int MaxJsonChars = 256 * 1024;
    private const int MaxJsonDepth = 64;
    private const string AllowedDomainPrefix = "core.";

    private readonly Dictionary<string, bool> _enabledByType;

    public EventCatalog()
        : this(new Dictionary<string, bool>(StringComparer.Ordinal))
    {
    }

    /// <summary>
    /// Creates an <see cref="EventCatalog"/> from a JSON string.
    /// </summary>
    /// <remarks>
    /// Refs: ADR-0004 (event contracts), ADR-0005 (quality gates).
    /// This method exists to support content-driven catalogs and deterministic tests without Godot dependencies.
    /// </remarks>
    public static EventCatalog FromJson(string json)
    {
        if (json is null)
            throw new ArgumentNullException(nameof(json));

        if (json.Length > MaxJsonChars)
            throw new ArgumentException($"EventCatalog JSON exceeds max size ({MaxJsonChars} chars).", nameof(json));

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = MaxJsonDepth });
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"EventCatalog JSON is invalid: {ex.Message}", nameof(json), ex);
        }

        using (doc)
        {
            var root = doc.RootElement;

            var catalogId = GetStringProperty(root, "catalogId") ?? GetStringProperty(root, "id") ?? "test";
            var schemaVersion =
                GetStringProperty(root, "schemaVersion") ??
                GetStringProperty(root, "version") ??
                "1";

            var eventsElement = GetPropertyCaseInsensitive(root, "events");
            var defs = new List<EventDefinition>();

            if (eventsElement.HasValue && eventsElement.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in eventsElement.Value.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                        continue;

                    var eventType =
                        GetStringProperty(item, "eventType", allowNumber: false) ??
                        GetStringProperty(item, "type", allowNumber: false);

                    if (!IsEventTypeAllowed(eventType))
                        continue;

                    var title = GetStringProperty(item, "title") ?? eventType!;
                    var description = GetStringProperty(item, "description");
                    var enabledByDefault = GetBoolProperty(item, "enabledByDefault") ?? GetBoolProperty(item, "enabled") ?? true;

                    defs.Add(new EventDefinition(eventType!, title, description, enabledByDefault));
                }
            }

            var definition = new EventCatalogDefinition(
                CatalogId: catalogId,
                SchemaVersion: schemaVersion,
                Events: defs,
                Chains: Array.Empty<EventChainDefinition>());

            return new EventCatalog(definition);
        }
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

    /// <summary>
    /// Returns enabled event types in stable order.
    /// </summary>
    public IReadOnlyList<string> GetEnabledEventTypes()
    {
        return _enabledByType
            .Where(kv => kv.Value)
            .Select(kv => kv.Key)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
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

    private static string? GetStringProperty(JsonElement element, string name, bool allowNumber = true)
    {
        var prop = GetPropertyCaseInsensitive(element, name);
        if (!prop.HasValue)
            return null;

        if (prop.Value.ValueKind == JsonValueKind.String)
            return prop.Value.GetString();

        if (allowNumber && prop.Value.ValueKind == JsonValueKind.Number)
            return prop.Value.GetRawText();

        return null;
    }

    private static bool? GetBoolProperty(JsonElement element, string name)
    {
        var prop = GetPropertyCaseInsensitive(element, name);
        if (!prop.HasValue)
            return null;

        if (prop.Value.ValueKind == JsonValueKind.True)
            return true;

        if (prop.Value.ValueKind == JsonValueKind.False)
            return false;

        return null;
    }

    private static JsonElement? GetPropertyCaseInsensitive(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                return prop.Value;
        }

        return null;
    }

    private static bool IsEventTypeAllowed(string? eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            return false;

        if (!eventType.StartsWith(AllowedDomainPrefix, StringComparison.Ordinal))
            return false;

        try
        {
            EventTypeRules.Validate(eventType, nameof(eventType));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
