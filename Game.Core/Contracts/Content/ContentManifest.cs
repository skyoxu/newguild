using System;
using System.Collections.Generic;

namespace Game.Core.Contracts.Content;

/// <summary>
/// Content manifest schema used to load data-driven gameplay assets (JSON).
/// </summary>
/// <remarks>
/// Refs: ADR-0005 (quality gates), ADR-0011 (Windows-only), ADR-0019 (content/ops).
/// Overlay: docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Content-Loading.md
/// </remarks>
public sealed record ContentManifest(
    string ManifestId,
    string SchemaVersion,
    IReadOnlyList<ContentManifestEntry> Entries,
    DateTimeOffset GeneratedAt
);

