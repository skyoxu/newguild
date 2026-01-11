using Godot;
using System;
using System.IO;
using System.Text.Json;
using System.Text;
using Game.Core.Observability;
using Game.Core.Services;

namespace Game.Godot.Scripts.Obs;

/// <summary>
/// Local JSONL event sink for Sentry-like diagnostics.
/// This project template does not ship a Sentry SDK integration by default.
/// </summary>
public partial class SentryClient : Node
{
    [Export] public bool Enabled { get; set; } = false;
    [Export] public string Dsn { get; set; } = string.Empty;

    public override void _Ready()
    {
        if (string.IsNullOrWhiteSpace(Dsn))
        {
            var env = System.Environment.GetEnvironmentVariable("SENTRY_DSN");
            if (!string.IsNullOrWhiteSpace(env))
            {
                Dsn = env;
                // Template default: no network reporting; local JSONL only unless explicitly enabled.
                Enabled = Enabled && !string.IsNullOrWhiteSpace(Dsn);
            }
        }
    }

    public bool CaptureMessage(string level, string message, System.Collections.Generic.Dictionary<string, object>? context = null)
    {
        try
        {
            if (!Enabled)
                return false;

            var includeSensitiveDetails = IncludeSensitiveDetails();
            var evt = new
            {
                ts = DateTime.UtcNow.ToString("O"),
                type = "message",
                level = level,
                message = includeSensitiveDetails ? message : PiiDataScrubber.Scrub(message),
                context = SanitizeContext(context, includeSensitiveDetails),
                dsn_present = !string.IsNullOrWhiteSpace(Dsn),
                enabled = Enabled
            };
            WriteLocal(evt);
            return true;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[SentryClient] CaptureMessage failed ({ex.GetType().Name}).");
            return false;
        }
    }

    public bool CaptureException(string exceptionMessage, System.Collections.Generic.Dictionary<string, object>? context = null)
    {
        try
        {
            if (!Enabled)
                return false;

            var includeSensitiveDetails = IncludeSensitiveDetails();
            var evt = new
            {
                ts = DateTime.UtcNow.ToString("O"),
                type = "exception",
                message = includeSensitiveDetails ? exceptionMessage : PiiDataScrubber.Scrub(exceptionMessage),
                context = SanitizeContext(context, includeSensitiveDetails),
                dsn_present = !string.IsNullOrWhiteSpace(Dsn),
                enabled = Enabled
            };
            WriteLocal(evt);
            return true;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[SentryClient] CaptureException failed ({ex.GetType().Name}).");
            return false;
        }
    }

    private void WriteLocal(object evt)
    {
        var dir = ProjectSettings.GlobalizePath("user://logs/sentry");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"events-{DateTime.UtcNow:yyyyMMdd}.jsonl");
        var json = JsonSerializer.Serialize(evt);
        File.AppendAllText(path, json + System.Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static bool IncludeSensitiveDetails()
    {
#if DEBUG
        const bool isDebugBuild = true;
#else
        const bool isDebugBuild = false;
#endif
        return SensitiveDetailsPolicy.IncludeSensitiveDetails(isDebugBuild);
    }

    private static System.Collections.Generic.Dictionary<string, object> SanitizeContext(
        System.Collections.Generic.Dictionary<string, object>? context,
        bool includeSensitiveDetails)
    {
        var sanitized = new System.Collections.Generic.Dictionary<string, object>();
        if (context == null)
            return sanitized;

        foreach (var kvp in context)
        {
            var key = kvp.Key ?? string.Empty;
            var value = kvp.Value;
            if (!includeSensitiveDetails && value is string s)
                sanitized[key] = PiiDataScrubber.Scrub(s);
            else
                sanitized[key] = value ?? string.Empty;
        }

        return sanitized;
    }
}
