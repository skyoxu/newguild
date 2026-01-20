using System;

namespace Game.Core.Domain;

public static class IntimacyRules
{
    public const int MinIntimacy = 0;
    public const int MaxIntimacy = 100;

    public static int Clamp(int value)
    {
        if (value < MinIntimacy) return MinIntimacy;
        if (value > MaxIntimacy) return MaxIntimacy;
        return value;
    }

    public static bool IsValidPeerPair(string subjectId, string otherId)
    {
        if (string.IsNullOrWhiteSpace(subjectId)) return false;
        if (string.IsNullOrWhiteSpace(otherId)) return false;
        if (string.Equals(subjectId, otherId, StringComparison.Ordinal)) return false;
        return true;
    }
}

