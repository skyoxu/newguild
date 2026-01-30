namespace Game.Core.Contracts.Achievements;

/// <summary>
/// DTO: achievement count update for cross-module UI/adapters.
/// </summary>
/// <remarks>
/// Cross-module contract aligned with ADR-0004 contracts discipline.
/// </remarks>
/// <param name="UnlockedCount">Total unlocked achievements after this update.</param>
/// <param name="TriggerEventType">Event type that triggered this update.</param>
public sealed record AchievementCountChanged(
    int UnlockedCount,
    string TriggerEventType
);
