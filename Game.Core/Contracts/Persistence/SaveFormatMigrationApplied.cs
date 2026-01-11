namespace Game.Core.Contracts.Persistence;

/// <summary>
/// Domain event: core.save.format.migration.applied
/// Description: Emitted when a save-file format migration is applied successfully.
/// </summary>
/// <remarks>
/// Follows ADR-0004 event contracts for the persistence domain.
/// </remarks>
public sealed record SaveFormatMigrationApplied(
    string SaveId,
    string FromVersion,
    string ToVersion,
    System.DateTimeOffset AppliedAt
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.save.format.migration.applied";
}
