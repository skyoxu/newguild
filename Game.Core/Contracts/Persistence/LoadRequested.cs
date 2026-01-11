namespace Game.Core.Contracts.Persistence;

/// <summary>
/// Domain event: core.load.requested
/// Description: Emitted when a load operation is requested.
/// </summary>
/// <remarks>
/// Follows ADR-0004 event contracts for the persistence domain.
/// </remarks>
public sealed record LoadRequested(
    string SaveId,
    System.DateTimeOffset RequestedAt
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.load.requested";
}
