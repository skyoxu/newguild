namespace Game.Contracts.Guild;

/// <summary>
/// Domain event: core.guild.created
/// Description: Emitted when a new guild is created.
/// </summary>
/// <remarks>
/// Follows ADR-0004 event contracts for the guild domain.
/// </remarks>
public sealed record GuildCreated(
    string GuildId,
    string CreatorId,
    string GuildName,
    System.DateTimeOffset CreatedAt
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.guild.created";
}
