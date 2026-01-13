namespace Game.Core.Contracts.Media;

/// <summary>
/// Domain event: core.reputation.changed
/// Description: Emitted when a guild reputation value changes.
/// </summary>
/// <remarks>
/// Follows ADR-0004 (event contracts). See overlay 08 feature slice:
/// docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Media-Reputation.md
/// </remarks>
public sealed record ReputationChanged(
    string GuildId,
    int OldValue,
    int NewValue,
    string Reason,
    System.DateTimeOffset ChangedAt
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.reputation.changed";
}

