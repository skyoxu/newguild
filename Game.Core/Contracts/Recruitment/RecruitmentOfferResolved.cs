namespace Game.Core.Contracts.Recruitment;

/// <summary>
/// Domain event: core.recruitment.offer.resolved
/// Description: Emitted when a recruitment offer is accepted/rejected/expired.
/// </summary>
/// <remarks>
/// Follows ADR-0004 (event contracts). See overlay 08 feature slice:
/// docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-T3-Recruitment.md
/// </remarks>
public sealed record RecruitmentOfferResolved(
    string OfferId,
    string GuildId,
    string CandidateId,
    string Decision,
    string Reason,
    System.DateTimeOffset ResolvedAt
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.recruitment.offer.resolved";
}

