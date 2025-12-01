namespace Game.Core.Contracts.Engine;

/// <summary>
/// Domain event: core.game.ended
/// Published when the game session ends
/// </summary>
public sealed record GameEnded(
    int Score
)
{
    public const string EventType = "core.game.ended";
}
