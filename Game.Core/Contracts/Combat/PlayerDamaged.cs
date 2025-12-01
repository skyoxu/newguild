namespace Game.Contracts.Combat;

/// <summary>
/// Domain event: core.player.damaged
/// Description: Emitted when a player takes damage in combat.
/// </summary>
/// <remarks>
/// Follows ADR-0004 event contracts for the combat domain.
/// </remarks>
public sealed record PlayerDamaged(
    string PlayerId,
    int Amount,
    string DamageType, // Physical | Magical | True
    bool IsCritical,
    System.DateTimeOffset Timestamp
)
{
    public const string EventType = "core.player.damaged";
}
