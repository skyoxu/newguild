using System;
using System.Collections.Generic;

namespace Game.Core.Progression;

public sealed class ExperienceSystem
{
    public const int BaseLevelXp = 100;

    private ExperienceSnapshot _snapshot = EmptySnapshot;

    public static ExperienceSnapshot EmptySnapshot { get; } = new(0, 1, BaseLevelXp);

    public ExperienceSnapshot ApplyRewards(IReadOnlyList<RewardGrant> grants)
    {
        if (grants is null)
            throw new ArgumentNullException(nameof(grants));

        var delta = CalculateExperienceDelta(grants);
        if (delta == 0)
            return _snapshot;

        var total = _snapshot.TotalXp + delta;
        _snapshot = FromTotalExperience(total);
        return _snapshot;
    }

    public ExperienceSnapshot Snapshot() => _snapshot;

    public void Restore(ExperienceSnapshot snapshot)
    {
        if (snapshot is null)
            throw new ArgumentNullException(nameof(snapshot));

        _snapshot = snapshot;
    }

    public static ExperienceSnapshot FromTotalExperience(int totalExperience)
    {
        if (totalExperience < 0)
            totalExperience = 0;

        var level = Math.Max(1, (totalExperience / BaseLevelXp) + 1);
        var nextLevelXp = level * BaseLevelXp;
        return new ExperienceSnapshot(totalExperience, level, nextLevelXp);
    }

    private static int CalculateExperienceDelta(IReadOnlyList<RewardGrant> grants)
    {
        var total = 0;
        foreach (var grant in grants)
        {
            if (grant is null || grant.Rewards is null)
                continue;

            if (!grant.Rewards.TryGetValue(RewardTypes.Experience, out var points))
                continue;

            if (points <= 0)
                continue;

            total += points;
        }

        return total;
    }
}

public sealed record ExperienceSnapshot(int TotalXp, int Level, int NextLevelXp);
