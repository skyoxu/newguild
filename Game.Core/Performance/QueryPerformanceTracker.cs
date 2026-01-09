using System;
using System.Diagnostics;
using System.Text;

namespace Game.Core.Performance;

public sealed class QueryPerformanceTracker
{
    private readonly PerformanceTracker _tracker;
    private const int MaxQueryNameLength = 64;

    public QueryPerformanceTracker(PerformanceTracker tracker)
    {
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
    }

    public T MeasureQuery<T>(string name, Func<T> query)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("name must be non-empty.", nameof(name));
        if (query is null)
            throw new ArgumentNullException(nameof(query));

        var sw = Stopwatch.StartNew();
        try
        {
            return query();
        }
        finally
        {
            sw.Stop();
            _tracker.RecordMeasure($"query_{NormalizeName(name)}", ElapsedMicroseconds(sw));
        }
    }

    private static string NormalizeName(string name)
    {
        var input = (name ?? string.Empty).Trim().ToLowerInvariant();
        if (input.Length == 0)
            return "query";

        var sb = new StringBuilder(Math.Min(input.Length, MaxQueryNameLength));
        var lastWasUnderscore = false;
        foreach (var ch in input)
        {
            if (sb.Length >= MaxQueryNameLength)
                break;

            var ok = (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9');
            if (ok)
            {
                sb.Append(ch);
                lastWasUnderscore = false;
                continue;
            }

            if (!lastWasUnderscore && sb.Length > 0)
            {
                sb.Append('_');
                lastWasUnderscore = true;
            }
        }

        var s = sb.ToString().Trim('_');
        return s.Length == 0 ? "query" : s;
    }

    private static long ElapsedMicroseconds(Stopwatch sw)
    {
        if (sw is null)
            return 0;

        var ticks = sw.ElapsedTicks;
        if (ticks <= 0)
            return 0;

        var us = (double)ticks * 1_000_000.0 / Stopwatch.Frequency;
        if (us < 0)
            return 0;

        if (us > long.MaxValue)
            return long.MaxValue;

        return (long)us;
    }
}
