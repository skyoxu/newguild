namespace Game.Core.Contracts.Guild;

/// <summary>
/// Domain event: core.guild.officer.assigned
/// Description: Emitted when a guild member is assigned to an officer slot.
/// </summary>
/// <remarks>
/// Follows ADR-0004 event contracts for the guild domain.
/// </remarks>
public sealed record GuildOfficerAssigned(
    string GuildId,
    string UserId,
    string Slot,
    System.DateTimeOffset AssignedAt,
    string AssignedByUserId
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.guild.officer.assigned";
}
