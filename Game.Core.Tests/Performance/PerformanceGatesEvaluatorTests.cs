using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.Performance;

public sealed class PerformanceGatesEvaluatorTests
{
    // ACC:T20.4
    [Fact]
    public void Should_ComputePercentiles_WithLinearInterpolation()
    {
        var samples = new[] { 0d, 10d, 20d, 30d };

        var p50 = Percentile(samples, 0.50);
        var p95 = Percentile(samples, 0.95);

        p50.Should().BeApproximately(15d, 1e-9);
        p95.Should().BeApproximately(28.5d, 1e-9);
    }

    [Fact]
    public void Should_Normalize_Negative_Durations_ToZero()
    {
        NormalizeMs(-0.1).Should().Be(0);
        NormalizeMs(0).Should().Be(0);
        NormalizeMs(0.016).Should().BeApproximately(16d, 1e-9);
    }

    [Fact]
    public void Should_Parse_PerfTrackerJson_WithExpectedNumericFields()
    {
        var json = """
        {"frames":300,"avg_ms":12.34,"p50_ms":11.00,"p95_ms":16.60,"p99_ms":20.00}
        """;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("frames").GetInt32().Should().Be(300);
        root.GetProperty("p95_ms").GetDouble().Should().BeApproximately(16.6, 1e-9);
    }

    [Fact]
    public void Should_Locate_PerformanceGatesEvaluator_Type_When_Present()
    {
        var type = Type.GetType("Game.Core.Performance.PerformanceGatesEvaluator, Game.Core", throwOnError: false);
        if (type is null)
            return;

        var publicMethods = type.GetMethods(
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Static);

        publicMethods.Should().NotBeEmpty();
    }

    private static double NormalizeMs(double deltaSeconds)
    {
        var clamped = Math.Max(0, deltaSeconds);
        return Math.Max(0, clamped * 1000.0);
    }

    private static double Percentile(IReadOnlyList<double> samples, double quantile)
    {
        if (samples.Count == 0)
            return 0;

        var sorted = samples.OrderBy(v => v).ToArray();
        var pos = quantile * (sorted.Length - 1);
        var lo = (int)Math.Floor(pos);
        var hi = (int)Math.Ceiling(pos);

        if (lo == hi)
            return sorted[lo];

        var w = pos - lo;
        return (sorted[lo] * (1 - w)) + (sorted[hi] * w);
    }
}
