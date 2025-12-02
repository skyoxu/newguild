namespace Game.Core.Contracts.Guild;

/// <summary>
/// Domain event: core.guild.disbanded
/// Description: Emitted when a guild is disbanded.
/// </summary>
/// <remarks>
/// Follows ADR-0004 event contracts for the guild domain.
/// </remarks>
public sealed record GuildDisbanded(
    string GuildId,
    string DisbandedByUserId,
    System.DateTimeOffset DisbandedAt,
    string Reason
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.guild.disbanded";
}

