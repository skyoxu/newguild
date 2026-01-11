namespace Game.Core.Contracts.Persistence;

/// <summary>
/// Domain event: core.load.completed
/// Description: Emitted when a load operation completes successfully.
/// </summary>
/// <remarks>
/// Follows ADR-0004 event contracts for the persistence domain.
/// </remarks>
public sealed record LoadCompleted(
    string SaveId,
    System.DateTimeOffset CompletedAt
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.load.completed";
}
