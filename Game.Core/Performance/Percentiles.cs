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

        var q = quantile;
        if (double.IsNaN(q) || double.IsInfinity(q))
            q = 0.0;

        if (q < 0.0) q = 0.0;
        if (q > 1.0) q = 1.0;

        var sorted = samples.OrderBy(v => v).ToArray();
        var pos = q * (sorted.Length - 1);
        var lo = (int)Math.Floor(pos);
        var hi = (int)Math.Ceiling(pos);

        if (lo == hi)
            return sorted[lo];

        var w = pos - lo;
        return (sorted[lo] * (1.0 - w)) + (sorted[hi] * w);
    }
}

