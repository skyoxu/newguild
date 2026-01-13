namespace Game.Core.Contracts.AI;

/// <summary>
/// Domain event: core.ai.ecosystem.step.completed
/// Description: Emitted when an AI ecosystem step completes for the current week.
/// </summary>
/// <remarks>
/// Follows ADR-0004 (event contracts). See overlay 08 feature slice:
/// docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-AI-Ecosystem.md
/// </remarks>
public sealed record AiEcosystemStepCompleted(
    string SaveId,
    int Week,
    string Summary,
    System.DateTimeOffset CompletedAt
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.ai.ecosystem.step.completed";
}

