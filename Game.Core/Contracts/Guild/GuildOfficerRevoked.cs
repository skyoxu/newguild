namespace Game.Core.Contracts.Guild;

/// <summary>
/// Domain event: core.guild.officer.revoked
/// Description: Emitted when a guild member is removed from an officer slot.
/// </summary>
/// <remarks>
/// Follows ADR-0004 event contracts for the guild domain.
/// See: docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Officers.md
/// </remarks>
public sealed record GuildOfficerRevoked(
    string GuildId,
    string UserId,
    string Slot,
    System.DateTimeOffset RevokedAt,
    string RevokedByUserId
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.guild.officer.revoked";
}

