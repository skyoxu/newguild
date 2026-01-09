using System;
using System.Globalization;

namespace Game.Core.Performance;

public static class PerformanceGateEvaluator
{
    public static PerformanceGateDecision EvaluateP95(double p95Ms, double thresholdMs)
    {
        return new PerformanceGateDecision(p95Ms, thresholdMs);
    }

    public static double ReadDoubleFromEnvironment(string key, double defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(raw))
            return defaultValue;

        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return value;

        return defaultValue;
    }
}

