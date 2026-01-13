namespace Game.Core.Contracts.AI;

/// <summary>
/// Domain event: core.ai.intent.issued
/// Description: Emitted when the AI issues an intent for downstream consumers.
/// </summary>
/// <remarks>
/// Follows ADR-0004 (event contracts). See overlay 08 feature slice:
/// docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Core-AI-Coordinator.md
/// </remarks>
public sealed record AiIntentIssued(
    string SaveId,
    int Week,
    string IntentId,
    string IntentType,
    string ActorId,
    string TargetId,
    System.DateTimeOffset IssuedAt
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.ai.intent.issued";
}

