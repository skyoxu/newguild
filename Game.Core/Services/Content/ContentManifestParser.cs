using System;
using System.Collections.Generic;
using System.Text.Json;
using Game.Core.Contracts.Content;
using Game.Core.Domain;

namespace Game.Core.Services.Content;

public sealed class ContentManifestParser
{
    private const int MaxManifestJsonChars = 256 * 1024;
    private const int MaxEntries = 1_024;
    private const int MaxIdChars = 128;
    private const int MaxTypeChars = 64;
    private const int MaxPathChars = 512;

    public static ContentManifest Parse(string manifestJson)
    {
        if (string.IsNullOrWhiteSpace(manifestJson))
            throw new FormatException("Manifest JSON is empty.");

        if (manifestJson.Length > MaxManifestJsonChars)
            throw new FormatException($"Manifest JSON exceeds max size: {MaxManifestJsonChars} chars.");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(manifestJson);
        }
        catch (JsonException ex)
        {
            throw new FormatException("Manifest JSON is invalid.", ex);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new FormatException("Manifest JSON root must be an object.");

            var manifestId = GetRequiredString(root, "manifestId", "manifest_id");
            var schemaVersion = GetRequiredString(root, "schemaVersion", "schema_version");

            if (!TryGetProperty(root, out var entriesElement, "entries"))
                throw new FormatException("Manifest JSON is missing 'entries'.");

            if (entriesElement.ValueKind != JsonValueKind.Array)
                throw new FormatException("Manifest JSON property 'entries' must be an array.");

            var entries = new List<ContentManifestEntry>();
            var ids = new HashSet<string>(StringComparer.Ordinal);

            var entryCount = 0;
            foreach (var entryElement in entriesElement.EnumerateArray())
            {
                entryCount++;
                if (entryCount > MaxEntries)
                    throw new FormatException($"Manifest JSON entries exceeds max size: {MaxEntries}.");

                if (entryElement.ValueKind != JsonValueKind.Object)
                    throw new FormatException("Manifest entry must be an object.");

                var entryId = GetRequiredString(entryElement, "id");
                var entryType = GetRequiredString(entryElement, "type");
                var entryPathRaw = GetRequiredString(entryElement, "path");

                if (entryId.Length > MaxIdChars)
                    throw new FormatException($"Manifest entry id exceeds max length: {MaxIdChars}.");
                if (entryType.Length > MaxTypeChars)
                    throw new FormatException($"Manifest entry type exceeds max length: {MaxTypeChars}.");
                if (entryPathRaw.Length > MaxPathChars)
                    throw new FormatException($"Manifest entry path exceeds max length: {MaxPathChars}.");

                if (!ids.Add(entryId))
                    throw new FormatException($"Duplicate manifest entry id: '{entryId}'.");

                var safePath = SafeResourcePath.FromString(entryPathRaw);
                if (safePath is null)
                    throw new FormatException($"Unsafe manifest entry path: '{entryPathRaw}'.");
                if (safePath.Type != PathType.ReadOnly)
                    throw new FormatException($"Unsafe manifest entry path (must be res://): '{entryPathRaw}'.");

                entries.Add(new ContentManifestEntry(
                    Kind: entryType,
                    Id: entryId,
                    ResourcePath: safePath.Value));
            }

            return new ContentManifest(
                ManifestId: manifestId,
                SchemaVersion: schemaVersion,
                Entries: entries,
                GeneratedAt: DateTimeOffset.UtcNow);
        }
    }

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

    private static string GetRequiredString(JsonElement element, params string[] names)
    {
        if (!TryGetProperty(element, out var property, names))
            throw new FormatException($"Manifest JSON is missing '{string.Join("/", names)}'.");

        if (property.ValueKind != JsonValueKind.String)
            throw new FormatException($"Manifest JSON property '{string.Join("/", names)}' must be a string.");

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
            throw new FormatException($"Manifest JSON property '{string.Join("/", names)}' cannot be empty.");

        return value.Trim();
    }
}
