using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core.Contracts.Achievements;

/// <summary>
/// Migration helper for persisted achievement snapshot payloads.
/// </summary>
public static class AchievementStateSnapshotMigration
{
    /// <summary>
    /// Converts a persisted payload version to the current snapshot version.
    /// </summary>
    /// <param name="schemaVersion">Persisted schema version (0 means pre-versioned legacy payload).</param>
    /// <param name="unlockedTriggerEventTypes">Persisted unlocked trigger event types.</param>
    /// <param name="snapshot">Migrated snapshot when conversion succeeds.</param>
    /// <returns><c>true</c> when migration succeeds; otherwise <c>false</c>.</returns>
    public static bool TryMigrateToCurrent(
        int schemaVersion,
        IEnumerable<string>? unlockedTriggerEventTypes,
        out AchievementStateSnapshot snapshot)
    {
        var normalizedTriggerTypes = NormalizeTriggerTypes(unlockedTriggerEventTypes);

        switch (schemaVersion)
        {
            case 0:
            case AchievementStateSnapshot.CurrentSchemaVersion:
                snapshot = new AchievementStateSnapshot(
                    AchievementStateSnapshot.CurrentSchemaVersion,
                    normalizedTriggerTypes.Count,
                    normalizedTriggerTypes);
                return true;

            default:
                snapshot = AchievementStateSnapshot.Empty;
                return false;
        }
    }

    private static IReadOnlyList<string> NormalizeTriggerTypes(IEnumerable<string>? triggerTypes)
    {
        if (triggerTypes == null)
            return Array.Empty<string>();

        return triggerTypes
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static item => item, StringComparer.Ordinal)
            .ToArray();
    }
}

