using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.Performance;

public class PerformanceTrackerTests
{
    private const string PerformanceTrackerTypeName = "Game.Core.Performance.PerformanceTracker";
    private const string PerformanceMetricsTypeName = "Game.Core.Performance.PerformanceMetrics";
    private const string QueryPerformanceTrackerTypeName = "Game.Core.Performance.QueryPerformanceTracker";

    // ACC:T20.1
    // ADR Refs: ADR-0005, ADR-0015, ADR-0018
    [Fact]
    public void Should_NotReferenceGodotApi_FromCorePerformanceTypes()
    {
        var trackerType = FindType(PerformanceTrackerTypeName);
        var metricsType = FindType(PerformanceMetricsTypeName);
        var queryTrackerType = FindType(QueryPerformanceTrackerTypeName);

        trackerType.Should().NotBeNull($"Expected type '{PerformanceTrackerTypeName}' to exist in Game.Core.");
        metricsType.Should().NotBeNull($"Expected type '{PerformanceMetricsTypeName}' to exist in Game.Core.");
        queryTrackerType.Should().NotBeNull($"Expected type '{QueryPerformanceTrackerTypeName}' to exist in Game.Core.");

        AssertNoGodotReference(trackerType!);
        AssertNoGodotReference(metricsType!);
        AssertNoGodotReference(queryTrackerType!);
    }

    // ACC:T20.2
    // ADR Refs: ADR-0005, ADR-0015
    [Fact]
    public void Should_ComputePercentiles_WithLinearInterpolation_MatchingReferenceAlgorithm()
    {
        var tracker = CreateTracker(windowSamples: 100);
        AddSamples(tracker, 1, 2, 3, 4, 5);

        var metrics = ComputeMetrics(tracker);

        GetInt(metrics, "Frames").Should().Be(5);
        GetDouble(metrics, "AvgMs").Should().BeApproximately(3.0, 1e-9);
        GetDouble(metrics, "P50Ms").Should().BeApproximately(3.0, 1e-9);
        GetDouble(metrics, "P95Ms").Should().BeApproximately(4.8, 1e-9);
        GetDouble(metrics, "P99Ms").Should().BeApproximately(4.96, 1e-9);
        GetDouble(metrics, "MaxMs").Should().BeApproximately(5.0, 1e-9);
    }

    // ACC:T20.3
    // ADR Refs: ADR-0015
    [Fact]
    public void Should_SerializeMetrics_ToSnakeCaseJson_ForPerfJsonAndGates()
    {
        var tracker = CreateTracker(windowSamples: 10);
        AddSamples(tracker, 10, 20);

        var metrics = ComputeMetrics(tracker);
        var json = JsonSerializer.Serialize(metrics);

        json.Should().Contain("\"frames\"");
        json.Should().Contain("\"avg_ms\"");
        json.Should().Contain("\"p50_ms\"");
        json.Should().Contain("\"p95_ms\"");
        json.Should().Contain("\"p99_ms\"");
        json.Should().Contain("\"max_ms\"");

        json.Should().NotContain("\"Frames\"");
        json.Should().NotContain("\"AvgMs\"");
        json.Should().NotContain("\"P95Ms\"");
    }

    // ACC:T20.4
    // ADR Refs: ADR-0015
    [Fact]
    public void Should_RejectNaNSampleInput_ToKeepPerfArtifactsValidJson()
    {
        var tracker = CreateTracker(windowSamples: 10);

        var addSample = RequireMethod(tracker.GetType(), "AddSample", typeof(double));
        Action act = () => addSample.Invoke(tracker, new object[] { double.NaN });

        act.Should().Throw<TargetInvocationException>()
            .WithInnerException<ArgumentException>();
    }

    [Fact]
    public void Should_TrimToWindowSamples_WhenMoreSamplesAdded()
    {
        var tracker = CreateTracker(windowSamples: 3);
        AddSamples(tracker, 1, 2, 3, 4, 5);

        var metrics = ComputeMetrics(tracker);

        GetInt(metrics, "Frames").Should().Be(3);
        GetDouble(metrics, "AvgMs").Should().BeApproximately(4.0, 1e-9);
        GetDouble(metrics, "P50Ms").Should().BeApproximately(4.0, 1e-9);
        GetDouble(metrics, "P95Ms").Should().BeApproximately(4.9, 1e-9);
        GetDouble(metrics, "MaxMs").Should().BeApproximately(5.0, 1e-9);
    }

    private static Type? FindType(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(fullName, throwOnError: false, ignoreCase: false);
            if (t != null) return t;
        }

        try
        {
            var core = Assembly.Load("Game.Core");
            return core.GetType(fullName, throwOnError: false, ignoreCase: false);
        }
        catch
        {
            return null;
        }
    }

    private static void AssertNoGodotReference(Type type)
    {
        var referenced = type.Assembly.GetReferencedAssemblies().Select(a => a.Name).Where(n => n != null).ToArray();
        referenced.Should().NotContain(n => string.Equals(n, "Godot", StringComparison.OrdinalIgnoreCase));
        referenced.Should().NotContain(n => n!.StartsWith("Godot.", StringComparison.OrdinalIgnoreCase));
    }

    private static object CreateTracker(int windowSamples)
    {
        var trackerType = FindType(PerformanceTrackerTypeName);
        trackerType.Should().NotBeNull($"Expected type '{PerformanceTrackerTypeName}' to exist.");

        var ctor = trackerType!.GetConstructor(new[] { typeof(int) });
        ctor.Should().NotBeNull("PerformanceTracker must have a public constructor: .ctor(int windowSamples).");

        return ctor!.Invoke(new object[] { windowSamples });
    }

    private static void AddSamples(object tracker, params double[] samplesMs)
    {
        var add = RequireMethod(tracker.GetType(), "AddSample", typeof(double));
        foreach (var v in samplesMs)
        {
            add.Invoke(tracker, new object[] { v });
        }
    }

    private static object ComputeMetrics(object tracker)
    {
        var compute = RequireMethod(tracker.GetType(), "Compute");
        var metrics = compute.Invoke(tracker, Array.Empty<object>());
        metrics.Should().NotBeNull("Compute() must return a PerformanceMetrics instance.");
        return metrics!;
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        var method = type.GetMethod(name, parameterTypes);
        method.Should().NotBeNull($"Expected {type.FullName} to have public method '{name}({string.Join(", ", parameterTypes.Select(t => t.Name))})'.");
        return method!;
    }

    private static PropertyInfo RequireProperty(Type type, string name)
    {
        var prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        prop.Should().NotBeNull($"Expected {type.FullName} to have public property '{name}'.");
        return prop!;
    }

    private static int GetInt(object obj, string propertyName)
    {
        var prop = RequireProperty(obj.GetType(), propertyName);
        var value = prop.GetValue(obj);
        value.Should().BeOfType<int>();
        return (int)value!;
    }

    private static double GetDouble(object obj, string propertyName)
    {
        var prop = RequireProperty(obj.GetType(), propertyName);
        var value = prop.GetValue(obj);
        value.Should().BeOfType<double>();
        return (double)value!;
    }
}
