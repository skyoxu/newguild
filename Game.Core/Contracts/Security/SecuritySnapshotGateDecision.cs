using System.Text.Json.Serialization;

namespace Game.Core.Contracts.Security;

/// <summary>
/// Domain event: core.security.snapshot.decision
/// Description: Emitted when snapshot restore/persist paths hit security-relevant validation branches.
/// </summary>
/// <remarks>
/// Follows ADR-0004 event contract naming and SSoT rules for security-domain decisions.
/// </remarks>
/// <param name="Ts">Audit timestamp in UTC.</param>
/// <param name="Action">Audit action token (invalid|cleared|clear_failed|restore_failed).</param>
/// <param name="Reason">Normalized reason token for the decision.</param>
/// <param name="Target">Target snapshot key being evaluated.</param>
/// <param name="Caller">Caller identifier for traceability.</param>
public sealed record SecuritySnapshotGateDecision(
    [property: JsonPropertyName("ts")] System.DateTimeOffset Ts,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("caller")] string Caller)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.security.snapshot.decision";

    public const string ActionInvalid = "invalid";
    public const string ActionCleared = "cleared";
    public const string ActionClearFailed = "clear_failed";
    public const string ActionRestoreFailed = "restore_failed";

    public const string ReasonNormalizeFailed = "normalize_failed";
    public const string ReasonUntrustedSource = "untrusted_source";
    public const string ReasonGuildMismatch = "guild_mismatch";
    public const string ReasonGuildContextMissing = "guild_context_missing";
}
