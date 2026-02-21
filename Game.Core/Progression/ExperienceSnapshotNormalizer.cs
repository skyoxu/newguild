using System;
using System.Globalization;
using System.Text.Json;
using Game.Core.Contracts.Progression;

namespace Game.Core.Progression;

public static class ExperienceSnapshotNormalizer
{
    private const int MaxPayloadLength = 8192;
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        MaxDepth = 16
    };

    public static bool TryNormalize(string? rawPayload, out string normalizedPayload)
    {
        normalizedPayload = string.Empty;
        if (string.IsNullOrWhiteSpace(rawPayload) || rawPayload.Length > MaxPayloadLength)
            return false;

        try
        {
            using var document = JsonDocument.Parse(rawPayload, JsonOptions);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            string? guildId = null;
            if (root.TryGetProperty("guildId", out var guildIdElement) && guildIdElement.ValueKind == JsonValueKind.String)
            {
                guildId = guildIdElement.GetString();
            }

            if (string.IsNullOrWhiteSpace(guildId) || guildId.Length > 128)
                return false;

            int totalExperience;
            if (root.TryGetProperty("totalExperience", out var totalExperienceElement) && totalExperienceElement.TryGetInt32(out var normalizedTotalExperience))
            {
                totalExperience = normalizedTotalExperience;
            }
            else if (root.TryGetProperty("total", out var totalElement) && totalElement.TryGetInt32(out var fallbackTotal))
            {
                totalExperience = fallbackTotal;
            }
            else
            {
                return false;
            }

            if (totalExperience < 0)
                return false;

            var delta = 0;
            if (root.TryGetProperty("delta", out var deltaElement) && deltaElement.TryGetInt32(out var parsedDelta))
                delta = parsedDelta;

            int level;
            if (root.TryGetProperty("level", out var levelElement) && levelElement.TryGetInt32(out var normalizedLevel))
            {
                level = normalizedLevel;
            }
            else if (root.TryGetProperty("newLevel", out var newLevelElement) && newLevelElement.TryGetInt32(out var fallbackLevel))
            {
                level = fallbackLevel;
            }
            else
            {
                return false;
            }

            if (level < 1)
                return false;

            var sourceEventType = ExperienceChanged.EventType;
            if (root.TryGetProperty("sourceEventType", out var sourceTypeElement) && sourceTypeElement.ValueKind == JsonValueKind.String)
            {
                var candidateType = sourceTypeElement.GetString();
                if (!string.IsNullOrWhiteSpace(candidateType) && candidateType.StartsWith("core.", StringComparison.Ordinal))
                    sourceEventType = candidateType;
            }

            if (!root.TryGetProperty("changedAt", out var changedAtElement) || changedAtElement.ValueKind != JsonValueKind.String)
                return false;

            var changedAtRaw = changedAtElement.GetString();
            if (string.IsNullOrWhiteSpace(changedAtRaw)
                || !DateTimeOffset.TryParse(
                    changedAtRaw,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var changedAt))
            {
                return false;
            }

            var payload = new ExperienceSnapshotPayload(
                GuildId: guildId,
                TotalExperience: totalExperience,
                Delta: delta,
                Level: level,
                SourceEventType: sourceEventType,
                ChangedAt: changedAt);

            normalizedPayload = JsonSerializer.Serialize(payload);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
