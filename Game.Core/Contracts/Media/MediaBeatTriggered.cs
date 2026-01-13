namespace Game.Core.Contracts.Media;

/// <summary>
/// Domain event: core.media.beat.triggered
/// Description: Emitted when a media beat is triggered by upstream gameplay.
/// </summary>
/// <remarks>
/// Follows ADR-0004 (event contracts). See overlay 08 feature slice:
/// docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Media-Reputation.md
/// </remarks>
public sealed record MediaBeatTriggered(
    string BeatId,
    string GuildId,
    string SourceEventType,
    string Headline,
    System.DateTimeOffset TriggeredAt
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.media.beat.triggered";
}

