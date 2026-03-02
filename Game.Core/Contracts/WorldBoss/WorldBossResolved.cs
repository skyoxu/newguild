namespace Game.Core.Contracts.WorldBoss;

/// <summary>
/// Domain event: core.worldboss.resolved
/// Description: Emitted when a world boss encounter is resolved.
/// </summary>
/// <remarks>
/// ADR refs: ADR-0004, ADR-0020.
/// Overlay refs:
/// - docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Guild-Manager.md
/// - docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-V11-Gameplay-Depth.md
/// </remarks>
public sealed record WorldBossResolved(
    string EncounterId,
    string GuildId,
    int Week,
    string Result,
    int RewardPoints,
    System.DateTimeOffset ResolvedAt
)
{
    /// <summary>
    /// Encounter resolved with victory.
    /// </summary>
    public const string ResultVictory = "victory";

    /// <summary>
    /// Encounter resolved with defeat.
    /// </summary>
    public const string ResultDefeat = "defeat";

    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = EventTypes.WorldBossResolved;
}
