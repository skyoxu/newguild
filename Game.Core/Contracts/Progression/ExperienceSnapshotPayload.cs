using System.Text.Json.Serialization;

namespace Game.Core.Contracts.Progression;

/// <summary>
/// Canonical snapshot payload used for experience save/load normalization.
/// </summary>
/// <remarks>
/// This DTO is the SSoT shape for normalized snapshot persistence payloads.
/// </remarks>
/// <param name="GuildId">Guild or user identifier bound to this snapshot.</param>
/// <param name="TotalExperience">Accumulated experience points.</param>
/// <param name="Delta">Latest delta applied to total experience.</param>
/// <param name="Level">Current level after applying total experience.</param>
/// <param name="SourceEventType">Domain event type that produced this snapshot.</param>
/// <param name="ChangedAt">Timestamp of the source change in UTC.</param>
public sealed record ExperienceSnapshotPayload(
    [property: JsonPropertyName("guildId")] string GuildId,
    [property: JsonPropertyName("totalExperience")] int TotalExperience,
    [property: JsonPropertyName("delta")] int Delta,
    [property: JsonPropertyName("level")] int Level,
    [property: JsonPropertyName("sourceEventType")] string SourceEventType,
    [property: JsonPropertyName("changedAt")] System.DateTimeOffset ChangedAt);
