namespace Game.Core.Contracts.Persistence;

/// <summary>
/// Domain event: core.load.failed
/// Description: Emitted when a load operation fails.
/// </summary>
/// <remarks>
/// Follows ADR-0004 event contracts for the persistence domain.
/// Do not include sensitive details in <paramref name="Reason"/>.
/// </remarks>
public sealed record LoadFailed(
    string SaveId,
    System.DateTimeOffset FailedAt,
    string Reason
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.load.failed";
}
