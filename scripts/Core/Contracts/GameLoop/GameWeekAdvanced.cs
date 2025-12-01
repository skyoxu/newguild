namespace Game.Contracts.GameLoop;

/// <summary>
/// Domain event: core.game_turn.week_advanced
/// Signals that the game loop advanced from one week to the next.
/// </summary>
/// <remarks>
/// See ADR-0004 (event contracts) and ADR-0015 (performance budgets) for naming and usage guidelines.
/// </remarks>
public sealed record GameWeekAdvanced(
    string SaveId,
    int PreviousWeek,
    int CurrentWeek,
    System.DateTimeOffset AdvancedAt
)
{
    public const string EventType = "core.game_turn.week_advanced";
}

