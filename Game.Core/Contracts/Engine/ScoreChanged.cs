namespace Game.Core.Contracts.Engine;

/// <summary>
/// Domain event: core.score.changed
/// Published when player score changes
/// </summary>
/// <remarks>
/// Follows ADR-0004 event contracts for engine-level events.
/// </remarks>
public sealed record ScoreChanged(
    int Score,
    int Added
)
{
    public const string EventType = "core.score.changed";
}
