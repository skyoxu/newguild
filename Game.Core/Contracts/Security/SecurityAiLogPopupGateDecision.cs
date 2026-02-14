namespace Game.Core.Contracts.Security;

/// <summary>
/// Domain event: core.security.ai_log_popup.decision
/// Description: Emitted when StartScreen evaluates allow/deny/error for AI log popup gate.
/// </summary>
/// <remarks>
/// Follows ADR-0004 event contracts for security decision auditing.
/// </remarks>
/// <param name="Target">Target being gated/audited (ai-log-popup).</param>
/// <param name="Decision">Decision token (allow|deny|error).</param>
/// <param name="Reason">Human-readable reason for the decision.</param>
/// <param name="OccurredAt">Timestamp of the decision (UTC recommended).</param>
/// <param name="Caller">Caller identifier for traceability.</param>
public sealed record SecurityAiLogPopupGateDecision(
    string Target,
    string Decision,
    string Reason,
    System.DateTimeOffset OccurredAt,
    string Caller
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.security.ai_log_popup.decision";

    public const string DecisionAllow = "allow";
    public const string DecisionDeny = "deny";
    public const string DecisionError = "error";

    public const string ReasonDemosDisabled = "demos_disabled";
    public const string ReasonInvalidPayload = "invalid_payload";
    public const string ReasonPopupNotAvailable = "popup_not_available";
    public const string ReasonPopupOpened = "popup_opened";
    public const string ReasonPopupToggled = "popup_toggled";
}
