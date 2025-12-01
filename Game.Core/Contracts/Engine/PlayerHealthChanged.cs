namespace Game.Core.Contracts.Engine;

/// <summary>
/// Domain event: core.player.health.changed
/// Published when player health changes due to damage or healing
/// </summary>
public sealed record PlayerHealthChanged(
    int Health,
    int Delta
)
{
    public const string EventType = "core.player.health.changed";
}
