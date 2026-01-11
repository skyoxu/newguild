namespace Game.Core.Contracts.Persistence;

/// <summary>
/// Domain event: core.save.completed
/// Description: Emitted when a save operation completes successfully.
/// </summary>
/// <remarks>
/// Follows ADR-0004 event contracts for the persistence domain.
/// </remarks>
public sealed record SaveCompleted(
    string SaveId,
    System.DateTimeOffset CompletedAt
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.save.completed";
}
