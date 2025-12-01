namespace Game.Core.Contracts.Engine;

/// <summary>
/// Domain event: core.game.started
/// Published when a new game session begins
/// </summary>
public sealed record GameStarted(
    string StateId
)
{
    public const string EventType = "core.game.started";
}
