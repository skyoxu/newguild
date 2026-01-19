using System;

namespace Game.Godot.Adapters;

internal static class SecurityAuditFormat
{
    public const int MaxReasonChars = 500;
    public const int MaxTargetChars = 1000;

    public static DateTime SanitizeEventTimestamp(DateTime eventTimestamp, DateTime writtenAt)
    {
        var now = writtenAt.Kind == DateTimeKind.Utc ? writtenAt : writtenAt.ToUniversalTime();
        var ts = eventTimestamp.Kind == DateTimeKind.Utc ? eventTimestamp : eventTimestamp.ToUniversalTime();
        if (ts < now.AddDays(-1) || ts > now.AddDays(1))
            return now;
        return ts;
    }

    public static string ToClaimString(string raw, string fallback, int maxChars)
    {
        var trimmed = (raw ?? "").Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            trimmed = fallback;

        var value = trimmed.StartsWith("claim:", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"claim:{trimmed}";

        if (value.Length <= maxChars)
            return value;

        return value[..Math.Min(maxChars, value.Length)];
    }

    public sealed record AuditEntryMaterial(
        string ts,
        string action,
        string reason,
        string target,
        string caller,
        string event_id,
        string event_timestamp,
        string event_source,
        string audit_writer,
        string written_at,
        string prev_hash,
        string reason_trust,
        string target_trust,
        string caller_trust,
        string data_sha256,
        int data_bytes,
        string data_reason,
        string data_target,
        string data_caller,
        bool parse_error,
        string? parse_error_reason
    );

    public sealed record AuditEntryFinal(
        string ts,
        string action,
        string reason,
        string target,
        string caller,
        string event_id,
        string event_timestamp,
        string event_source,
        string audit_writer,
        string written_at,
        string prev_hash,
        string reason_trust,
        string target_trust,
        string caller_trust,
        string data_sha256,
        int data_bytes,
        string data_reason,
        string data_target,
        string data_caller,
        bool parse_error,
        string? parse_error_reason,
        string entry_sha256
    );
}

