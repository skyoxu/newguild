using System;

namespace Game.Core.Contracts.Persistence;

/// <summary>
/// Domain event: core.autosave.disabled
/// </summary>
/// <remarks>
/// Per ADR-0004 (event bus and contracts), DomainEvent.Type follows CloudEvents 1.0 naming.
/// Recorded in overlay docs under docs/architecture/overlays/PRD-Guild-Manager/08/08-Contracts-CloudEvents-Core.md.
/// </remarks>
public sealed record AutoSaveDisabled(
    DateTimeOffset DisabledAt)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.autosave.disabled";
}
