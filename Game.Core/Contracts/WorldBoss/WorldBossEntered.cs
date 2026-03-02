namespace Game.Core.Contracts.WorldBoss;

/// <summary>
/// Domain event: core.worldboss.entered
/// Description: Emitted when a guild enters a world boss encounter.
/// </summary>
/// <remarks>
/// ADR refs: ADR-0004, ADR-0020.
/// Overlay refs:
/// - docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Guild-Manager.md
/// - docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-V11-Gameplay-Depth.md
/// </remarks>
public sealed record WorldBossEntered(
    string EncounterId,
    string GuildId,
    int Week,
    System.DateTimeOffset EnteredAt
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = EventTypes.WorldBossEntered;
}
