namespace Game.Core.Contracts.Security;

/// <summary>
/// Domain event: security.raid_encounter_demo.decision
/// Description: Emitted when the Raid Encounter demo gate evaluates allow/deny/error.
/// </summary>
/// <remarks>
/// Follows ADR-0004 event contracts for the security domain.
/// </remarks>
/// <param name="Target">Target being gated/audited (e.g., raid-encounter-demo).</param>
/// <param name="Decision">Decision token (allow|deny|error).</param>
/// <param name="Reason">Human-readable reason for the decision.</param>
/// <param name="OccurredAt">Timestamp of the decision (UTC recommended).</param>
/// <param name="Caller">Caller identifier for traceability.</param>
public sealed record SecurityDemoGateDecision(
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
    public const string EventType = "security.raid_encounter_demo.decision";

    public const string DecisionAllow = "allow";
    public const string DecisionDeny = "deny";
    public const string DecisionError = "error";
}
