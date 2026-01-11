namespace Game.Core.Contracts.Persistence;

/// <summary>
/// Domain event: core.storage.migration.applied
/// Description: Emitted when a storage schema migration is applied successfully.
/// </summary>
/// <remarks>
/// Follows ADR-0004 event contracts for the persistence domain.
/// </remarks>
public sealed record StorageMigrationApplied(
    int FromVersion,
    int ToVersion,
    System.DateTimeOffset AppliedAt,
    string Description
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.storage.migration.applied";
}
