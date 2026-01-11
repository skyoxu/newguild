using System;
using System.Text.Json;
using Game.Core.Ports;

namespace Game.Core.Observability;

/// <summary>
/// Minimal structured logger (JSON) that does not depend on Godot or any external SDK.
/// </summary>
public sealed class StructuredLogger : ILogger
{
    private readonly Action<string> _writeLine;
    private readonly Func<DateTimeOffset> _now;
    private readonly string _source;
    private readonly bool _includeSensitiveDetails;

    public StructuredLogger(Action<string> writeLine, string source = "core", Func<DateTimeOffset>? now = null, bool includeSensitiveDetails = false)
    {
        _writeLine = writeLine ?? throw new ArgumentNullException(nameof(writeLine));
        _source = string.IsNullOrWhiteSpace(source) ? "core" : source;
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _includeSensitiveDetails = includeSensitiveDetails;
    }

    public void Info(string message) => Write("info", message, null);
    public void Warn(string message) => Write("warn", message, null);
    public void Error(string message) => Write("error", message, null);
    public void Error(string message, Exception ex) => Write("error", message, ex);

    private void Write(string level, string message, Exception? ex)
    {
        var safeMessage = _includeSensitiveDetails ? message : PiiDataScrubber.Scrub(message);
        var exMessage = ex?.Message;
        if (!_includeSensitiveDetails && exMessage != null)
            exMessage = PiiDataScrubber.Scrub(exMessage);

        var payload = new
        {
            ts = _now().ToString("O"),
            level,
            message = safeMessage,
            source = _source,
            exception = ex == null ? null : new { type = ex.GetType().Name, message = exMessage }
        };
        _writeLine(JsonSerializer.Serialize(payload));
    }
}
