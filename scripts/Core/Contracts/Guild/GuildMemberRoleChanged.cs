namespace Game.Contracts.Guild;

/// <summary>
/// Domain event: core.guild.member.role_changed
/// Description: Emitted when a guild member role is changed.
/// </summary>
/// <remarks>
/// Follows ADR-0004 event contracts for the guild domain.
/// </remarks>
public sealed record GuildMemberRoleChanged(
    string UserId,
    string GuildId,
    string OldRole,
    string NewRole,
    System.DateTimeOffset ChangedAt,
    string ChangedByUserId
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.guild.member.role_changed";
}

