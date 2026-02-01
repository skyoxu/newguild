using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;

namespace Game.Core.World;

/// <summary>
/// Content-driven NPC guild archetype catalog used by world generation.
/// </summary>
/// <remarks>
/// Refs: ADR-0004 (event contracts), ADR-0005 (quality gates).
/// Overlay: docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Worldgen.md
/// Overlay: docs/architecture/overlays/PRD-Guild-Manager/08/08-DataSchema.md
/// </remarks>
public sealed class NpcGuildArchetypeCatalog : IEnumerable<NpcGuildArchetypeCatalog.NpcGuildArchetype>
{
    private const int MaxJsonChars = 256 * 1024;
    private const int MaxJsonDepth = 64;
    private const int MaxItems = 4_096;
    private const int MaxIdChars = 128;

    private readonly Dictionary<string, NpcGuildArchetype> _byId;
    public string? ContentVersion { get; }
    public int Count => _byId.Count;

    private NpcGuildArchetypeCatalog(Dictionary<string, NpcGuildArchetype> byId, string? contentVersion)
    {
        _byId = byId;
        ContentVersion = string.IsNullOrWhiteSpace(contentVersion) ? null : contentVersion.Trim();
    }

    /// <summary>
    /// Loads a catalog from a content JSON string.
    /// </summary>
    /// <remarks>
    /// This method is intentionally pure C# (no Godot dependencies) to keep unit tests deterministic and fast.
    /// Supports both <c>{ "npcGuildArchetypes": [...] }</c> (overlay schema) and <c>{ "archetypes": [...] }</c> (tests).
    /// </remarks>
    public static NpcGuildArchetypeCatalog LoadFromContentJson(string json)
    {
        if (json is null)
            throw new ArgumentNullException(nameof(json));

        if (json.Length > MaxJsonChars)
            throw new ArgumentException($"NpcGuildArchetypeCatalog JSON exceeds max size ({MaxJsonChars} chars).", nameof(json));

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = MaxJsonDepth });
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"NpcGuildArchetypeCatalog JSON is invalid: {ex.Message}", nameof(json), ex);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("NpcGuildArchetypeCatalog JSON root must be an object.", nameof(json));

            var contentVersion = GetOptionalString(root, nameof(json), "contentVersion", "content_version");

            if (!TryGetProperty(root, out var itemsElement, "npcGuildArchetypes", "archetypes"))
                throw new ArgumentException("NpcGuildArchetypeCatalog JSON is missing 'npcGuildArchetypes/archetypes'.", nameof(json));

            if (itemsElement.ValueKind != JsonValueKind.Array)
                throw new ArgumentException("NpcGuildArchetypeCatalog JSON property 'npcGuildArchetypes/archetypes' must be an array.", nameof(json));

            var byId = new Dictionary<string, NpcGuildArchetype>(StringComparer.Ordinal);
            var itemCount = 0;

            foreach (var item in itemsElement.EnumerateArray())
            {
                itemCount++;
                if (itemCount > MaxItems)
                    throw new ArgumentException($"NpcGuildArchetypeCatalog JSON exceeds max items ({MaxItems}).", nameof(json));

                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var id = GetRequiredString(item, nameof(json), "id");
                if (id.Length > MaxIdChars)
                    throw new ArgumentException($"NpcGuildArchetype id exceeds max length ({MaxIdChars}).", nameof(json));
                if (!IsValidArchetypeId(id))
                    throw new ArgumentException($"NpcGuildArchetype id contains invalid characters: '{id}'.", nameof(json));

                if (!byId.TryAdd(id, new NpcGuildArchetype(id)))
                    throw new ArgumentException($"Duplicate npc guild archetype id: '{id}'.", nameof(json));
            }

            return new NpcGuildArchetypeCatalog(byId, contentVersion);
        }
    }

    /// <summary>
    /// Tries to resolve an archetype by id.
    /// </summary>
    public bool TryGetById(string id, out NpcGuildArchetype? archetype)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            archetype = null;
            return false;
        }

        return _byId.TryGetValue(id, out archetype);
    }

    public IEnumerator<NpcGuildArchetype> GetEnumerator()
    {
        return _byId.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Minimal archetype DTO for world generation.
    /// </summary>
    public sealed record NpcGuildArchetype(string Id);

    private static bool TryGetProperty(JsonElement element, out JsonElement value, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out value))
                return true;
        }

        value = default;
        return false;
    }

    private static string GetRequiredString(JsonElement element, string jsonParamName, params string[] names)
    {
        if (!TryGetProperty(element, out var property, names))
            throw new ArgumentException($"NpcGuildArchetypeCatalog JSON is missing '{string.Join("/", names)}'.", jsonParamName);

        if (property.ValueKind != JsonValueKind.String)
            throw new ArgumentException($"NpcGuildArchetypeCatalog JSON property '{string.Join("/", names)}' must be a string.", jsonParamName);

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"NpcGuildArchetypeCatalog JSON property '{string.Join("/", names)}' cannot be empty.", jsonParamName);

        return value.Trim();
    }

    private static string? GetOptionalString(JsonElement element, string jsonParamName, params string[] names)
    {
        if (!TryGetProperty(element, out var property, names))
            return null;

        if (property.ValueKind != JsonValueKind.String)
            throw new ArgumentException($"NpcGuildArchetypeCatalog JSON property '{string.Join("/", names)}' must be a string.", jsonParamName);

        var value = property.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool IsValidArchetypeId(string id)
    {
        // Defensive-only: id must be a stable business key, not a path.
        // Allow typical identifier punctuation but reject whitespace, slashes, and control characters.
        if (string.IsNullOrWhiteSpace(id))
            return false;

        foreach (var character in id)
        {
            if (character <= 0x1F || character == 0x7F)
                return false;

            if (char.IsAsciiLetterOrDigit(character))
                continue;

            if (character is '_' or '.' or '-')
                continue;

            return false;
        }

        return true;
    }
}
