namespace Game.Core.Ports;

/// <summary>
/// Provides event gating and discovery for the event engine.
/// </summary>
/// <remarks>
/// Refs: ADR-0004 (event contracts). This port must remain Godot-free and testable.
/// </remarks>
public interface IEventCatalog
{
    /// <summary>
    /// Returns true if the given CloudEvents-like <c>type</c> is enabled for execution.
    /// </summary>
    bool IsEventEnabled(string eventType);
}
