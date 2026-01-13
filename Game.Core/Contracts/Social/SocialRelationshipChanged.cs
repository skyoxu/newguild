namespace Game.Core.Contracts.Social;

/// <summary>
/// Domain event: core.social.relationship.changed
/// Description: Emitted when a relationship value changes.
/// </summary>
/// <remarks>
/// Follows ADR-0004 (event contracts). See overlay 08 feature slice:
/// docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Social.md
/// </remarks>
public sealed record SocialRelationshipChanged(
    string GuildId,
    string SubjectId,
    string OtherId,
    int OldValue,
    int NewValue,
    System.DateTimeOffset ChangedAt
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.social.relationship.changed";
}

