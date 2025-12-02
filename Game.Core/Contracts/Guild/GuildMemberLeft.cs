namespace Game.Core.Contracts.Guild;

/// <summary>
/// Domain event: core.guild.member.left
/// Description: Emitted when a user leaves or is removed from a guild.
/// </summary>
/// <remarks>
/// Follows ADR-0004 event contracts for the guild domain.
/// </remarks>
public sealed record GuildMemberLeft(
    string UserId,
    string GuildId,
    System.DateTimeOffset LeftAt,
    string Reason
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.guild.member.left";
}
