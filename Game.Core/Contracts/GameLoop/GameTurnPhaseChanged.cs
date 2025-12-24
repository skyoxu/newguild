using Game.Core.Domain.Turn;

namespace Game.Contracts.GameLoop;

/// <summary>
/// Domain event: core.game_turn.phase_changed
/// Indicates that the game turn phase changed within the same week.
/// </summary>
/// <remarks>
/// See ADR-0004 (event contracts) and ADR-0015 (performance budgets) for naming and usage guidelines.
/// </remarks>
public sealed record GameTurnPhaseChanged(
    SaveIdValue SaveId,
    int Week,
    string PreviousPhase,
    string CurrentPhase,
    System.DateTimeOffset ChangedAt
)
{
    public const string EventType = "core.game_turn.phase_changed";
}
