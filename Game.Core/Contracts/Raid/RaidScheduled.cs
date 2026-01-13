namespace Game.Core.Contracts.Raid;

/// <summary>
/// Domain event: core.raid.scheduled
/// Description: Emitted when a raid encounter is scheduled for a given week.
/// </summary>
/// <remarks>
/// Follows ADR-0004 (event contracts). See overlay 08 feature slice:
/// docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-PVE-Raid.md
/// </remarks>
public sealed record RaidScheduled(
    string RaidId,
    string GuildId,
    int Week,
    string EncounterId,
    System.DateTimeOffset ScheduledAt
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.raid.scheduled";
}

