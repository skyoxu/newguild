using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core.Performance;

public static class Percentiles
{
    public static double LinearInterpolated(IReadOnlyList<double> samples, double quantile)
    {
        if (samples.Count == 0)
            return 0.0;

        var normalizedQuantile = quantile;
        if (double.IsNaN(normalizedQuantile) || double.IsInfinity(normalizedQuantile))
            normalizedQuantile = 0.0;

        if (normalizedQuantile < 0.0) normalizedQuantile = 0.0;
        if (normalizedQuantile > 1.0) normalizedQuantile = 1.0;

        var sorted = samples.OrderBy(v => v).ToArray();
        var pos = normalizedQuantile * (sorted.Length - 1);
        var lo = (int)Math.Floor(pos);
        var hi = (int)Math.Ceiling(pos);

        if (lo == hi)
            return sorted[lo];

        var weight = pos - lo;
        return (sorted[lo] * (1.0 - weight)) + (sorted[hi] * weight);
    }
}

