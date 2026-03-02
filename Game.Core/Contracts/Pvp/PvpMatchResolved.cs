namespace Game.Core.Contracts.Pvp;

/// <summary>
/// Domain event: core.pvp.match.resolved
/// Description: Emitted when a PVP match is resolved.
/// </summary>
/// <remarks>
/// ADR refs: ADR-0004, ADR-0020.
/// Overlay refs:
/// - docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Social.md
/// - docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-V11-Gameplay-Depth.md
/// </remarks>
public sealed record PvpMatchResolved(
    string MatchId,
    string GuildId,
    string OpponentGuildId,
    string Result,
    int RatingDelta,
    System.DateTimeOffset ResolvedAt
)
{
    /// <summary>
    /// Match result for winning side.
    /// </summary>
    public const string ResultWin = "win";

    /// <summary>
    /// Match result for losing side.
    /// </summary>
    public const string ResultLoss = "loss";

    /// <summary>
    /// Match result for draw.
    /// </summary>
    public const string ResultDraw = "draw";

    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = EventTypes.PvpMatchResolved;
}
