namespace Game.Core.Contracts.Activity;

/// <summary>
/// Domain event: core.activity.feed.appended
/// Description: Emitted when a new activity feed entry is appended for gameplay observability.
/// </summary>
/// <remarks>
/// ADR refs: ADR-0004, ADR-0020.
/// Overlay refs:
/// - docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Core-Observability.md
/// - docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-V11-Gameplay-Depth.md
/// </remarks>
public sealed record ActivityFeedAppended(
    string FeedEntryId,
    string GuildId,
    string SourceEventType,
    string Message,
    System.DateTimeOffset AppendedAt
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = EventTypes.ActivityFeedAppended;
}
