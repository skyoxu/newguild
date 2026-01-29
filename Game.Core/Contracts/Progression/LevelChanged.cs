namespace Game.Core.Contracts.Progression;

/// <summary>
/// Domain event: core.level.changed
/// Description: Emitted when a guild level changes due to experience gain.
/// </summary>
/// <remarks>
/// Follows ADR-0004 (event contracts). See overlay 08 feature slice:
/// docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Tactical-Rewards.md
/// </remarks>
public sealed record LevelChanged(
    string GuildId,
    int OldLevel,
    int NewLevel,
    int TotalExperience,
    string SourceEventType,
    System.DateTimeOffset ChangedAt
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.level.changed";
}
