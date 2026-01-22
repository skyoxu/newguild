using System;

namespace Game.Core.Contracts.Content;

/// <summary>
/// Domain event: core.content.manifest.loaded
/// Emitted when the content manifest is successfully loaded and validated.
/// </summary>
/// <remarks>
/// Refs: ADR-0004 (event contracts), ADR-0005.
/// Overlay: docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Content-Loading.md
/// </remarks>
public sealed record ContentManifestLoaded(
    string ManifestId,
    string SchemaVersion,
    int EntryCount,
    DateTimeOffset LoadedAt
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.content.manifest.loaded";
}

