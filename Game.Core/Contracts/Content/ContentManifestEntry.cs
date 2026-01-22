namespace Game.Core.Contracts.Content;

/// <summary>
/// A single entry in a <see cref="ContentManifest"/>.
/// </summary>
/// <remarks>
/// Refs: ADR-0005.
/// Overlay: docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Content-Loading.md
/// </remarks>
public sealed record ContentManifestEntry(
    string Kind,
    string Id,
    string ResourcePath
);

