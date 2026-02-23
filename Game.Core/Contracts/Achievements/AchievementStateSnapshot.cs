using System;
using System.Collections.Generic;

namespace Game.Core.Contracts.Achievements;

/// <summary>
/// Persistent snapshot for achievement tracker state.
/// </summary>
/// <param name="SchemaVersion">Snapshot schema version for forward migration.</param>
/// <param name="UnlockedCount">Total unlocked achievements count.</param>
/// <param name="UnlockedTriggerEventTypes">Distinct trigger event types already unlocked.</param>
public sealed record AchievementStateSnapshot(
    int SchemaVersion,
    int UnlockedCount,
    IReadOnlyList<string> UnlockedTriggerEventTypes
)
{
    /// <summary>
    /// Current schema version for persisted achievement snapshot payload.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Returns an empty snapshot with zero count and no unlocked triggers.
    /// </summary>
    public static AchievementStateSnapshot Empty { get; } =
        new(CurrentSchemaVersion, 0, Array.Empty<string>());
}
