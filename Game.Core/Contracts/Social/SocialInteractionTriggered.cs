namespace Game.Core.Contracts.Social;

/// <summary>
/// Domain event: core.social.interaction.triggered
/// Description: Emitted when a social interaction is triggered between two actors.
/// </summary>
/// <remarks>
/// Follows ADR-0004 (event contracts). See overlay 08 feature slice:
/// docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Social.md
/// </remarks>
public sealed record SocialInteractionTriggered(
    string InteractionId,
    string GuildId,
    string ActorId,
    string TargetId,
    string InteractionType,
    System.DateTimeOffset TriggeredAt
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.social.interaction.triggered";
}

