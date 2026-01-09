using System;
using System.Collections.Generic;

namespace Game.Core.Performance;

public sealed class PerformanceTracker
{
    private const int DefaultWindowSamples = 300;
    private const int DefaultMaxMetricKeys = 256;
    private const int DefaultMaxMetricNameLength = 128;

    private readonly int _windowSamples;
    private readonly int _maxMetricKeys;
    private readonly int _maxMetricNameLength;
    private readonly Dictionary<string, Queue<long>> _samplesByMetric = new(StringComparer.Ordinal);

    public PerformanceTracker()
        : this(DefaultWindowSamples)
    {
    }

    public PerformanceTracker(int windowSamples, int maxMetricKeys = DefaultMaxMetricKeys, int maxMetricNameLength = DefaultMaxMetricNameLength)
    {
        if (windowSamples <= 0)
            throw new ArgumentOutOfRangeException(nameof(windowSamples), "windowSamples must be > 0");
        if (maxMetricKeys <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxMetricKeys), "maxMetricKeys must be > 0");
        if (maxMetricNameLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxMetricNameLength), "maxMetricNameLength must be > 0");

        _windowSamples = windowSamples;
        _maxMetricKeys = maxMetricKeys;
        _maxMetricNameLength = maxMetricNameLength;
    }

    public void RecordMeasure(string metricName, long value)
    {
        if (string.IsNullOrWhiteSpace(metricName))
            throw new ArgumentException("metricName must be non-empty.", nameof(metricName));

        if (value < 0)
            value = 0;

        var key = metricName.Trim();
        if (key.Length > _maxMetricNameLength)
            return;

        if (!_samplesByMetric.TryGetValue(key, out var samples))
        {
            if (_samplesByMetric.Count >= _maxMetricKeys)
                return;

            samples = new Queue<long>(Math.Min(_windowSamples, 128));
            _samplesByMetric.Add(key, samples);
        }

        samples.Enqueue(value);
        while (samples.Count > _windowSamples)
            samples.Dequeue();
    }

    public int GetSampleCount(string metricName)
    {
        if (string.IsNullOrWhiteSpace(metricName))
            return 0;

        return _samplesByMetric.TryGetValue(metricName.Trim(), out var samples) ? samples.Count : 0;
    }

    public double GetAverage(string metricName)
    {
        if (!TryGetSamples(metricName, out var samples))
            return 0;

        long sum = 0;
        foreach (var v in samples)
            sum += v;

        return (double)sum / samples.Count;
    }

    public long GetMax(string metricName)
    {
        if (!TryGetSamples(metricName, out var samples))
            return 0;

        var max = 0L;
        foreach (var v in samples)
        {
            if (v > max)
                max = v;
        }

        return max;
    }

    public long GetPercentile(string metricName, double quantile)
    {
        if (!TryGetSamples(metricName, out var samples))
            return 0;

        var arr = samples.ToArray();
        var asDouble = new double[arr.Length];
        for (var i = 0; i < arr.Length; i++)
            asDouble[i] = arr[i];

        var p = Percentiles.LinearInterpolated(asDouble, quantile);
        if (p <= 0)
            return 0;
        if (p >= long.MaxValue)
            return long.MaxValue;
        return (long)Math.Round(p, MidpointRounding.AwayFromZero);
    }

    private bool TryGetSamples(string metricName, out Queue<long> samples)
    {
        samples = null!;
        if (string.IsNullOrWhiteSpace(metricName))
            return false;

        return _samplesByMetric.TryGetValue(metricName.Trim(), out samples);
    }
}
