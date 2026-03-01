namespace Game.Core.Contracts.Engine;

/// <summary>
/// Domain event: core.game.ended
/// Published when the game session ends
/// </summary>
/// <remarks>
/// Follows ADR-0004 event contracts for engine-level events.
/// </remarks>
public sealed record GameEnded(
    int Score
)
{
    public const string EventType = "core.game.ended";
}
