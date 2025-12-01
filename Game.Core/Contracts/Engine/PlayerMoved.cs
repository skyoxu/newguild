namespace Game.Core.Contracts.Engine;

/// <summary>
/// Domain event: core.player.moved
/// Published when the player changes position
/// </summary>
public sealed record PlayerMoved(
    double X,
    double Y
)
{
    public const string EventType = "core.player.moved";
}
