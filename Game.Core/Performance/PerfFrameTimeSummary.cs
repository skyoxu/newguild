using System;
using System.Text.Json;

namespace Game.Core.Performance;

public sealed record PerfFrameTimeSummary(
    int Frames,
    double AvgMs,
    double P50Ms,
    double P95Ms,
    double P99Ms
)
{
    public static PerfFrameTimeSummary FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new PerfFrameTimeSummary(0, 0.0, 0.0, 0.0, 0.0);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new PerfFrameTimeSummary(
            Frames: TryGetInt32(root, "frames"),
            AvgMs: TryGetDouble(root, "avg_ms"),
            P50Ms: TryGetDouble(root, "p50_ms"),
            P95Ms: TryGetDouble(root, "p95_ms"),
            P99Ms: TryGetDouble(root, "p99_ms")
        );
    }

    private static int TryGetInt32(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) ? value.GetInt32() : 0;
    }

    private static double TryGetDouble(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) ? value.GetDouble() : 0.0;
    }
}

