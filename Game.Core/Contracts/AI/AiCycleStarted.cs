namespace Game.Core.Contracts.AI;

/// <summary>
/// Domain event: core.ai.cycle.started
/// Description: Emitted when an AI simulation cycle begins.
/// </summary>
/// <remarks>
/// Follows ADR-0004 (event contracts). See overlay 08 feature slice:
/// docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Core-AI-Coordinator.md
/// </remarks>
public sealed record AiCycleStarted(
    string SaveId,
    int Week,
    System.DateTimeOffset StartedAt
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.ai.cycle.started";
}

