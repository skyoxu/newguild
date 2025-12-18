namespace Game.Core.Contracts
{
    public record DomainEvent(
        string Type,
        string Source,
        object? Data,
        DateTime Timestamp,
        string Id,
        string SpecVersion = "1.0",
        string DataContentType = "application/json"
    );
}

namespace Game.Core.Contracts.Security
{
    /// <summary>
    /// Domain event: security.file_access.denied
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
        public const string EventType = "security.file_access.denied";
    }

    /// <summary>
    /// Domain event: security.process.denied
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
        public const string EventType = "security.process.denied";
    }

    /// <summary>
    /// Domain event: security.url_access.denied
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
        public const string EventType = "security.url_access.denied";
    }
}
