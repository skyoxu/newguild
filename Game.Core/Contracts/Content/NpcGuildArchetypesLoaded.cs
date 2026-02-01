using System;

namespace Game.Core.Contracts.Content;

/// <summary>
/// Domain event: core.content.npc_guild_archetypes.loaded
/// Emitted when NPC guild archetypes are successfully loaded from content JSON.
/// </summary>
/// <remarks>
/// Refs: ADR-0004 (event contracts), ADR-0005.
/// Overlay: docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Phase2-Worldgen.md
/// Overlay: docs/architecture/overlays/PRD-Guild-Manager/08/08-DataSchema.md
/// </remarks>
public sealed record NpcGuildArchetypesLoaded(
    string ContentVersion,
    int ArchetypeCount,
    DateTimeOffset LoadedAt
)
{
    /// <summary>
    /// CloudEvents 1.0 type field for this event.
    /// </summary>
    public const string EventType = "core.content.npc_guild_archetypes.loaded";
}

