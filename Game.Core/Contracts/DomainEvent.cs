namespace Game.Core.Contracts
{
    /// <summary>
    /// Canonical CloudEvents-like envelope used by the domain event bus.
    /// </summary>
    /// <remarks>
    /// This contract defines shared event metadata fields consumed by core and adapters.
    /// </remarks>
    public record DomainEvent(
        string Type,
        string Source,
        object? Data,
        DateTimeOffset Timestamp,
        string Id,
        string SpecVersion = "1.0",
        string DataContentType = "application/json"
    );
}

namespace Game.Core.Contracts.Security
{
    /// <summary>
    /// Domain event: core.security.file_access.denied
    /// Description: Emitted when file path validation denies access.
    /// </summary>
    /// <remarks>
    /// Follows ADR-0004 event contracts for the security domain.
    /// </remarks>
    public sealed record SecurityFileAccessDenied(
        string Target,
        string Reason,
        System.DateTimeOffset OccurredAt,
        string Caller
    )
    {
        /// <summary>
        /// CloudEvents 1.0 type field for this event.
        /// </summary>
        public const string EventType = "core.security.file_access.denied";
    }

    /// <summary>
    /// Domain event: core.security.process.denied
    /// Description: Emitted when a process execution request is denied.
    /// </summary>
    /// <remarks>
    /// Follows ADR-0004 event contracts for the security domain.
    /// </remarks>
    public sealed record SecurityProcessDenied(
        string Target,
        string Reason,
        System.DateTimeOffset OccurredAt,
        string Caller
    )
    {
        /// <summary>
        /// CloudEvents 1.0 type field for this event.
        /// </summary>
        public const string EventType = "core.security.process.denied";
    }

    /// <summary>
    /// Domain event: core.security.process.approved
    /// Description: Emitted when a process execution request is approved (development/CI only).
    /// </summary>
    /// <remarks>
    /// Follows ADR-0004 event contracts for the security domain.
    /// </remarks>
    public sealed record SecurityProcessApproved(
        string Target,
        int ExitCode,
        System.DateTimeOffset OccurredAt,
        string Caller
    )
    {
        /// <summary>
        /// CloudEvents 1.0 type field for this event.
        /// </summary>
        public const string EventType = "core.security.process.approved";
    }

    /// <summary>
    /// Domain event: core.security.url_access.denied
    /// Description: Emitted when URL validation denies access.
    /// </summary>
    /// <remarks>
    /// Follows ADR-0004 event contracts for the security domain.
    /// </remarks>
    public sealed record SecurityUrlAccessDenied(
        string Target,
        string Reason,
        System.DateTimeOffset OccurredAt,
        string Caller
    )
    {
        /// <summary>
        /// CloudEvents 1.0 type field for this event.
        /// </summary>
        public const string EventType = "core.security.url_access.denied";
    }
}
