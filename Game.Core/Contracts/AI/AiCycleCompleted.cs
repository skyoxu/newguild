namespace Game.Core.Contracts.AI;

/// <summary>
/// Domain event: core.ai.cycle.completed
/// Description: Emitted when an AI simulation cycle finishes.
/// </summary>
/// <remarks>
/// Follows ADR-0004 (event contracts). See overlay 08 feature slice:
/// docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Core-AI-Coordinator.md
/// </remarks>
public sealed record AiCycleCompleted(
    string SaveId,
    int Week,
    int IntentsIssued,
    System.DateTimeOffset CompletedAt
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.ai.cycle.completed";
}

