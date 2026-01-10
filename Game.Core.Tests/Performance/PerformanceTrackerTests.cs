using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Game.Core.Performance;
using Xunit;

namespace Game.Core.Tests.Performance;

public sealed class PerformanceTrackerTests
{
    // ACC:T20.1
    [Fact]
    public void Should_ExposePhase15MetricKeys_And_NotReferenceGodot()
    {
        var referenced = typeof(PerformanceTracker).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
        referenced.Should().NotContain("Godot");

        var metricsType = typeof(PerformanceMetrics);
        (metricsType.IsAbstract && metricsType.IsSealed)
            .Should()
            .BeTrue("PerformanceMetrics should be a static class of metric name constants.");

        AssertConstString(metricsType, "StartupTimeUs", "startup_time_us");
        AssertConstString(metricsType, "MenuFrameTimeUs", "menu_frame_time_us");
        AssertConstString(metricsType, "GameFrameTimeUs", "game_frame_time_us");
        AssertConstString(metricsType, "DbQueryTimeUs", "db_query_time_us");
        AssertConstString(metricsType, "SignalLatencyUs", "signal_latency_us");
        AssertConstString(metricsType, "MemoryPeakBytes", "memory_peak_bytes");
        AssertConstString(metricsType, "GcPauseTimeUs", "gc_pause_us");
        AssertConstString(metricsType, "CustomMetricPrefix", "custom_");
    }

    // ACC:T20.2
    [Fact]
    public void Should_RecordMeasures_And_ComputeP50P95AverageMax_ForNamedMetric()
    {
        var tracker = CreatePerformanceTracker();

        Invoke(tracker, "RecordMeasure", PerformanceMetrics.DbQueryTimeUs, 1000L);
        Invoke(tracker, "RecordMeasure", PerformanceMetrics.DbQueryTimeUs, 2000L);
        Invoke(tracker, "RecordMeasure", PerformanceMetrics.DbQueryTimeUs, 5000L);

        var avgUs = (double)InvokeRequired(tracker, "GetAverage", PerformanceMetrics.DbQueryTimeUs);
        var maxUs = (long)InvokeRequired(tracker, "GetMax", PerformanceMetrics.DbQueryTimeUs);
        var p50Us = (long)InvokeRequired(tracker, "GetPercentile", PerformanceMetrics.DbQueryTimeUs, 0.50);
        var p95Us = (long)InvokeRequired(tracker, "GetPercentile", PerformanceMetrics.DbQueryTimeUs, 0.95);

        avgUs.Should().BeApproximately(2666.6666667, 1e-6);
        maxUs.Should().Be(5000);
        p50Us.Should().Be(2000);
        p95Us.Should().Be(4700);
    }

    // ACC:T20.3
    [Fact]
    public void Should_EnsureQueryTracker_EndsMeasure_EvenWhenQueryThrows()
    {
        var tracker = CreatePerformanceTracker();
        var queryTracker = CreateQueryPerformanceTracker(tracker);

        var measureQuery = GetMeasureQueryGenericMethod(queryTracker.GetType());
        var closed = measureQuery.MakeGenericMethod(typeof(int));

        Action act = () => closed.Invoke(
            queryTracker,
            new object[]
            {
                "users",
                new Func<int>(() => throw new InvalidOperationException("boom"))
            });

        act.Should().Throw<TargetInvocationException>().WithInnerException<InvalidOperationException>();

        var samples = (int)InvokeRequired(tracker, "GetSampleCount", "query_users");
        samples.Should().Be(1);
    }

    [Fact]
    public void Should_RecordQueryMeasure_WhenQuerySucceeds()
    {
        var tracker = CreatePerformanceTracker();
        var queryTracker = CreateQueryPerformanceTracker(tracker);

        var measureQuery = GetMeasureQueryGenericMethod(queryTracker.GetType());
        var closed = measureQuery.MakeGenericMethod(typeof(string));
        var result = closed.Invoke(queryTracker, new object[] { "users", new Func<string>(() => "ok") });

        result.Should().Be("ok");
        var samples = (int)InvokeRequired(tracker, "GetSampleCount", "query_users");
        samples.Should().Be(1);
    }

    [Fact]
    public void Should_NormalizeQueryName_ToLowerSnakeAndLimitChars()
    {
        var tracker = CreatePerformanceTracker();
        var queryTracker = CreateQueryPerformanceTracker(tracker);

        var measureQuery = GetMeasureQueryGenericMethod(queryTracker.GetType());
        var closed = measureQuery.MakeGenericMethod(typeof(string));
        _ = closed.Invoke(queryTracker, new object[] { "  Users/By Id  ", new Func<string>(() => "ok") });

        ((int)InvokeRequired(tracker, "GetSampleCount", "query_users_by_id")).Should().Be(1);
    }

    [Fact]
    public void Should_Throw_ForNullTracker()
    {
        Action act = () => _ = new QueryPerformanceTracker(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Should_Throw_ForBlankQueryName()
    {
        var tracker = new PerformanceTracker();
        var queryTracker = new QueryPerformanceTracker(tracker);
        Action act = () => queryTracker.MeasureQuery("   ", () => 1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Should_Throw_ForNullQueryFunc()
    {
        var tracker = new PerformanceTracker();
        var queryTracker = new QueryPerformanceTracker(tracker);
        Action act = () => queryTracker.MeasureQuery<int>("users", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Should_ReturnZeroFromElapsedMicroseconds_ForNullStopwatch_And_NotStarted()
    {
        var type = typeof(QueryPerformanceTracker);
        var method = type.GetMethod("ElapsedMicroseconds", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        ((long)method!.Invoke(null, new object?[] { null })!).Should().Be(0);
        ((long)method.Invoke(null, new object?[] { new System.Diagnostics.Stopwatch() })!).Should().Be(0);
    }

    [Fact]
    public void Should_ReturnZeroStats_ForUnknownMetric()
    {
        var tracker = CreatePerformanceTracker();

        ((double)InvokeRequired(tracker, "GetAverage", "missing")).Should().Be(0);
        ((long)InvokeRequired(tracker, "GetMax", "missing")).Should().Be(0);
        ((long)InvokeRequired(tracker, "GetPercentile", "missing", 0.50)).Should().Be(0);
        ((int)InvokeRequired(tracker, "GetSampleCount", "missing")).Should().Be(0);
    }

    [Fact]
    public void Should_ClampNegativeMeasure_ToZero()
    {
        var tracker = CreatePerformanceTracker();

        Invoke(tracker, "RecordMeasure", PerformanceMetrics.DbQueryTimeUs, -5L);

        var maxUs = (long)InvokeRequired(tracker, "GetMax", PerformanceMetrics.DbQueryTimeUs);
        maxUs.Should().Be(0);
    }

    [Fact]
    public void Should_ClampQuantile_OutsideRange_And_HandleNaN()
    {
        var tracker = CreatePerformanceTracker();

        Invoke(tracker, "RecordMeasure", PerformanceMetrics.DbQueryTimeUs, 1000L);
        Invoke(tracker, "RecordMeasure", PerformanceMetrics.DbQueryTimeUs, 2000L);

        var pMin = (long)InvokeRequired(tracker, "GetPercentile", PerformanceMetrics.DbQueryTimeUs, -1.0);
        var pMax = (long)InvokeRequired(tracker, "GetPercentile", PerformanceMetrics.DbQueryTimeUs, 2.0);
        var pNaN = (long)InvokeRequired(tracker, "GetPercentile", PerformanceMetrics.DbQueryTimeUs, double.NaN);

        pMin.Should().Be(1000);
        pMax.Should().Be(2000);
        pNaN.Should().Be(1000);
    }

    [Fact]
    public void Should_ReturnZeroPercentile_WhenAllSamplesAreZero()
    {
        var tracker = new PerformanceTracker(windowSamples: 4);

        tracker.RecordMeasure(PerformanceMetrics.DbQueryTimeUs, 0);
        tracker.RecordMeasure(PerformanceMetrics.DbQueryTimeUs, 0);

        tracker.GetPercentile(PerformanceMetrics.DbQueryTimeUs, 0.95).Should().Be(0);
    }

    [Fact]
    public void Should_TrimSamples_ToWindowSize()
    {
        var tracker = new PerformanceTracker(windowSamples: 2);

        tracker.RecordMeasure(PerformanceMetrics.DbQueryTimeUs, 1000);
        tracker.RecordMeasure(PerformanceMetrics.DbQueryTimeUs, 2000);
        tracker.RecordMeasure(PerformanceMetrics.DbQueryTimeUs, 5000);

        tracker.GetSampleCount(PerformanceMetrics.DbQueryTimeUs).Should().Be(2);
        tracker.GetMax(PerformanceMetrics.DbQueryTimeUs).Should().Be(5000);
        tracker.GetPercentile(PerformanceMetrics.DbQueryTimeUs, 0.50).Should().Be(3500);
    }

    [Fact]
    public void Should_IgnoreNewMetric_WhenMetricKeyLimitReached()
    {
        var tracker = new PerformanceTracker(windowSamples: 1, maxMetricKeys: 1, maxMetricNameLength: 64);

        tracker.RecordMeasure("a", 1);
        tracker.RecordMeasure("b", 1);

        tracker.GetSampleCount("a").Should().Be(1);
        tracker.GetSampleCount("b").Should().Be(0);
    }

    [Fact]
    public void Should_IgnoreMetric_WhenNameTooLong()
    {
        var tracker = new PerformanceTracker(windowSamples: 1, maxMetricKeys: 16, maxMetricNameLength: 3);

        tracker.RecordMeasure("abcd", 1);

        tracker.GetSampleCount("abcd").Should().Be(0);
    }

    private static object CreatePerformanceTracker()
    {
        var type = typeof(PerformanceTracker);
        var ctor = type.GetConstructor(Type.EmptyTypes);

        ctor.Should().NotBeNull("PerformanceTracker should have a public parameterless constructor.");
        return ctor!.Invoke(null);
    }

    private static object CreateQueryPerformanceTracker(object tracker)
    {
        var type = typeof(QueryPerformanceTracker);
        var ctor = type.GetConstructor(new[] { typeof(PerformanceTracker) });

        ctor.Should().NotBeNull("QueryPerformanceTracker should accept a PerformanceTracker instance.");
        return ctor!.Invoke(new[] { tracker });
    }

    private static MethodInfo GetMeasureQueryGenericMethod(Type type)
    {
        var method = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(m =>
                m.Name == "MeasureQuery" &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 2 &&
                m.GetParameters()[0].ParameterType == typeof(string) &&
                IsFuncOfT(m.GetParameters()[1].ParameterType));

        method.Should().NotBeNull("QueryPerformanceTracker should expose MeasureQuery<T>(string, Func<T>).");
        return method!;
    }

    private static bool IsFuncOfT(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Func<>);
    }

    private static object? Invoke(object instance, string methodName, params object[] args)
    {
        var argTypes = args.Select(a => a.GetType()).ToArray();
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, null, argTypes, null);

        method.Should().NotBeNull(
            $"Expected {instance.GetType().Name}.{methodName}({string.Join(", ", argTypes.Select(t => t.Name))}) to exist.");

        return method!.Invoke(instance, args);
    }

    private static object InvokeRequired(object instance, string methodName, params object[] args)
    {
        var result = Invoke(instance, methodName, args);
        result.Should().NotBeNull($"Expected {instance.GetType().Name}.{methodName} to return a value.");
        return result!;
    }

    private static void AssertConstString(Type type, string name, string expectedValue)
    {
        var field = type.GetField(name, BindingFlags.Public | BindingFlags.Static);

        field.Should().NotBeNull($"Expected PerformanceMetrics to declare public const string {name}.");
        field!.IsLiteral.Should().BeTrue($"Expected {name} to be a const string.");
        field.FieldType.Should().Be(typeof(string));
        field.GetRawConstantValue().Should().Be(expectedValue);
    }
}
