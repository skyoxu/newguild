using Godot;
using System;

namespace Game.Godot.Scripts.Obs;

/// <summary>
/// Observability autoload (adapter layer): collects runtime metadata and wires optional
/// reporting sinks (e.g., Sentry) without leaking engine dependencies into Game.Core.
/// </summary>
public partial class Observability : Node
{
    [Export] public bool Enabled { get; set; } = false;
    [Export] public string Release { get; private set; } = string.Empty;
    [Export] public string Environment { get; private set; } = string.Empty;

    public override void _EnterTree()
    {
        Release = ReadFirstNonEmptyEnv("SENTRY_RELEASE", "GITHUB_SHA") ?? string.Empty;
        Environment = ReadFirstNonEmptyEnv("SENTRY_ENVIRONMENT", "SENTRY_ENV", "ENVIRONMENT") ?? string.Empty;

        if (IsOfflineMode())
            Enabled = false;
    }

    public override void _Ready()
    {
        var sentry = GetNodeOrNull<Node>("/root/SentryClient") as SentryClient;
        if (sentry != null && Enabled)
        {
            sentry.CaptureMessage("info", "Observability initialized", new System.Collections.Generic.Dictionary<string, object>
            {
                ["release"] = Release,
                ["environment"] = Environment
            });
        }
    }

    private static bool IsOfflineMode()
    {
        var rawValue = System.Environment.GetEnvironmentVariable("GD_OFFLINE_MODE") ?? string.Empty;
        return rawValue.Trim() == "1" || rawValue.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadFirstNonEmptyEnv(params string[] keys)
    {
        foreach (var key in keys)
        {
            var envValue = System.Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(envValue))
                return envValue.Trim();
        }

        return null;
    }
}

