namespace Game.Core.Contracts.Pvp;

/// <summary>
/// Domain event: core.pvp.match.started
/// Description: Emitted when a PVP match starts.
/// </summary>
/// <remarks>
/// ADR refs: ADR-0004, ADR-0020.
/// Overlay refs:
/// - docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Social.md
/// - docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-V11-Gameplay-Depth.md
/// </remarks>
public sealed record PvpMatchStarted(
    string MatchId,
    string GuildId,
    string OpponentGuildId,
    int Week,
    System.DateTimeOffset StartedAt
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = EventTypes.PvpMatchStarted;
}
