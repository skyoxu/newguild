using System;
using FluentAssertions;
using Game.Core.Performance;
using Xunit;

namespace Game.Core.Tests.Performance;

public sealed class PerformanceGatesEvaluatorTests
{
    // ACC:T20.4
    [Fact]
    public void Should_ParsePerfTrackerJson_And_EvaluateP95Gate_Deterministically()
    {
        const string json = "{\"frames\":2,\"avg_ms\":5.0,\"p50_ms\":0.0,\"p95_ms\":9.5,\"p99_ms\":9.9}";

        var metrics = PerfFrameTimeSummary.FromJson(json);

        metrics.Frames.Should().Be(2);
        metrics.P95Ms.Should().BeApproximately(9.5, 1e-9);

        var decision = PerformanceGateEvaluator.EvaluateP95(metrics.P95Ms, thresholdMs: 16.6);
        decision.IsOverBudget.Should().BeFalse();
        decision.ThresholdMs.Should().BeApproximately(16.6, 1e-9);
    }

    [Fact]
    public void Should_ComputePercentile_UsingLinearInterpolation()
    {
        var samples = new double[] { 0.0, 10.0 };

        var p95 = Percentiles.LinearInterpolated(samples, 0.95);

        p95.Should().BeApproximately(9.5, 1e-9);
    }

    [Fact]
    public void Should_ReturnZeroPercentile_ForEmptySamples()
    {
        Percentiles.LinearInterpolated(Array.Empty<double>(), 0.95).Should().Be(0.0);
    }

    [Fact]
    public void Should_ClampQuantile_And_HandleNaN_InPercentiles()
    {
        var samples = new double[] { 10.0 };

        Percentiles.LinearInterpolated(samples, -1.0).Should().Be(10.0);
        Percentiles.LinearInterpolated(samples, 2.0).Should().Be(10.0);
        Percentiles.LinearInterpolated(samples, double.NaN).Should().Be(10.0);
        Percentiles.LinearInterpolated(samples, double.PositiveInfinity).Should().Be(10.0);
    }

    [Fact]
    public void Should_DefaultMissingPerfFields_ToZero()
    {
        var summary = PerfFrameTimeSummary.FromJson("{\"frames\":7}");

        summary.Frames.Should().Be(7);
        summary.AvgMs.Should().Be(0.0);
        summary.P50Ms.Should().Be(0.0);
        summary.P95Ms.Should().Be(0.0);
        summary.P99Ms.Should().Be(0.0);
    }

    [Fact]
    public void Should_DefaultSummary_WhenJsonIsBlank()
    {
        PerfFrameTimeSummary.FromJson("").Frames.Should().Be(0);
        PerfFrameTimeSummary.FromJson("   ").P95Ms.Should().Be(0.0);
    }

    [Fact]
    public void Should_ReadThresholdFromEnvironment_UsingDefaultWhenMissing()
    {
        const string key = "PERF_P95_THRESHOLD_MS__UNITTEST";

        var old = Environment.GetEnvironmentVariable(key);
        try
        {
            Environment.SetEnvironmentVariable(key, null);

            var value = PerformanceGateEvaluator.ReadDoubleFromEnvironment(key, defaultValue: 16.6);

            value.Should().BeApproximately(16.6, 1e-9);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, old);
        }
    }

    [Fact]
    public void Should_ReadThresholdFromEnvironment_UsingDefaultWhenInvalid()
    {
        const string key = "PERF_P95_THRESHOLD_MS__UNITTEST_INVALID";

        var old = Environment.GetEnvironmentVariable(key);
        try
        {
            Environment.SetEnvironmentVariable(key, "not-a-number");

            var value = PerformanceGateEvaluator.ReadDoubleFromEnvironment(key, defaultValue: 16.6);

            value.Should().BeApproximately(16.6, 1e-9);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, old);
        }
    }

    [Fact]
    public void Should_ReadThresholdFromEnvironment_WhenValid()
    {
        const string key = "PERF_P95_THRESHOLD_MS__UNITTEST_VALID";

        var old = Environment.GetEnvironmentVariable(key);
        try
        {
            Environment.SetEnvironmentVariable(key, "12.3");

            var value = PerformanceGateEvaluator.ReadDoubleFromEnvironment(key, defaultValue: 16.6);

            value.Should().BeApproximately(12.3, 1e-9);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, old);
        }
    }
}
