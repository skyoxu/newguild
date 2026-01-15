using System;

namespace Game.Core.Domain;

/// <summary>
/// Pending recruitment offer stored in the Guild DB.
/// This is internal domain state, not a cross-module contract.
/// </summary>
public sealed record RecruitmentOffer(
    string OfferId,
    string GuildId,
    string CandidateId,
    GuildRole Role,
    DateTimeOffset PresentedAt
);

