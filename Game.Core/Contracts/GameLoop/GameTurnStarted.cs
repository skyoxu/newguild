using Game.Core.Domain.Turn;

namespace Game.Core.Contracts.GameLoop;

/// <summary>
/// Domain event: core.game_turn.started
/// Represents the start of a game turn for a given save and week.
/// </summary>
/// <remarks>
/// See ADR-0004 (event contracts) and ADR-0015 (performance budgets) for naming and usage guidelines.
/// </remarks>
public sealed record GameTurnStarted(
    SaveIdValue SaveId,
    int Week,
    string Phase,
    System.DateTimeOffset StartedAt
)
{
    public const string EventType = "core.game_turn.started";
}
