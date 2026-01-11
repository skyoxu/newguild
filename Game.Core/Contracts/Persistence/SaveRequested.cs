namespace Game.Core.Contracts.Persistence;

/// <summary>
/// Domain event: core.save.requested
/// Description: Emitted when a save operation is requested.
/// </summary>
/// <remarks>
/// Follows ADR-0004 event contracts for the persistence domain.
/// </remarks>
public sealed record SaveRequested(
    string SaveId,
    System.DateTimeOffset RequestedAt
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.save.requested";
}
