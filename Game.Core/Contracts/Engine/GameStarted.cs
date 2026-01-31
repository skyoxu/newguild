namespace Game.Core.Contracts.Engine;

/// <summary>
/// Domain event: core.game.started
/// Published when a new game session begins
/// </summary>
/// <remarks>
/// ADR refs: ADR-0004 (event naming/CloudEvents type), ADR-0005 (quality gates).
/// Task 40 adds an explicit seed surface to enable deterministic replay and UI validation.
/// Seed is required and should be non-empty; producers must provide the canonical world seed or a stable seed fingerprint.
/// </remarks>
public sealed record GameStarted(
    string StateId,
    string Seed
)
{
    public const string EventType = "core.game.started";
}
