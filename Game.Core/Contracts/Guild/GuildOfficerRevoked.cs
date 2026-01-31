namespace Game.Core.Contracts.Guild;

/// <summary>
/// Domain event: core.guild.officer.revoked
/// Description: Emitted when an officer assignment is revoked from a slot.
/// </summary>
/// <remarks>
/// Follows ADR-0004 event contracts for the guild domain.
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

