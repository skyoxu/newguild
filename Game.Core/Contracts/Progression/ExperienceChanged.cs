namespace Game.Core.Contracts.Progression;

/// <summary>
/// Domain event: core.experience.changed
/// Description: Emitted when guild experience changes and a new total is calculated.
/// </summary>
/// <remarks>
/// Follows ADR-0004 (event contracts). See overlay 08 feature slice:
/// docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Tactical-Rewards.md
/// </remarks>
public sealed record ExperienceChanged(
    string GuildId,
    int TotalExperience,
    int Delta,
    int Level,
    string SourceEventType,
    System.DateTimeOffset ChangedAt
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.experience.changed";

    /// <summary>
    /// Canonical event source from core domain publish path.
    /// </summary>
    public const string SourceCore = "core";

    /// <summary>
    /// Canonical event source from UI publish path.
    /// </summary>
    public const string SourceUi = "ui";

    /// <summary>
    /// Canonical event source from reward ledger publish path.
    /// </summary>
    public const string SourceRewardLedger = "RewardLedgerService";
}
