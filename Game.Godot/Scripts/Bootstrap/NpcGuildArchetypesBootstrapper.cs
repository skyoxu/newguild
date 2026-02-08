using System;
using Game.Core.Contracts;
using Game.Core.Contracts.Content;
using Game.Core.Domain;
using Game.Core.World;
using Godot;

namespace Game.Godot.Scripts.Bootstrap;

/// <summary>
/// Startup bootstrap: loads NPC guild archetypes from res:// content JSON and publishes a domain event.
/// </summary>
/// <remarks>
/// Refs: ADR-0004 (event contracts), ADR-0019 (safe paths).
/// Event: <c>core.content.npc_guild_archetypes.loaded</c>.
/// </remarks>
public sealed partial class NpcGuildArchetypesBootstrapper : Node
{
    private const string NpcGuildsPath = "res://Game.Godot/Assets/Data/content/base/npc_guilds.json";

    public NpcGuildArchetypeCatalog? Catalog { get; private set; }

    public override void _Ready()
    {
        BootstrapOnce();
    }

    private void BootstrapOnce()
    {
        if (Catalog != null)
            return;

        var bus = GetNodeOrNull<Game.Godot.Adapters.EventBusAdapter>("/root/EventBus");
        if (bus is null)
        {
            GD.PrintErr("[NpcGuildArchetypesBootstrapper] EventBus not found; skipping npc guild archetypes load.");
            return;
        }

        var safePath = SafeResourcePath.FromString(NpcGuildsPath);
        if (safePath is null || safePath.Type != PathType.ReadOnly)
        {
            GD.PrintErr($"[NpcGuildArchetypesBootstrapper] npc_guilds path is not a res:// path: '{NpcGuildsPath}'.");
            return;
        }

        try
        {
            using var file = FileAccess.Open(safePath.Value, FileAccess.ModeFlags.Read);
            var json = file?.GetAsText();
            if (string.IsNullOrWhiteSpace(json))
            {
                GD.PrintErr($"[NpcGuildArchetypesBootstrapper] npc_guilds is missing or empty: '{NpcGuildsPath}'.");
                return;
            }

            var catalog = NpcGuildArchetypeCatalog.LoadFromContentJson(json);
            Catalog = catalog;

            var evt = new NpcGuildArchetypesLoaded(
                ContentVersion: catalog.ContentVersion ?? "unknown",
                ArchetypeCount: catalog.Count,
                LoadedAt: DateTimeOffset.UtcNow);

            _ = bus.PublishAsync(new DomainEvent(
                Type: NpcGuildArchetypesLoaded.EventType,
                Source: nameof(NpcGuildArchetypesBootstrapper),
                Data: evt,
                Timestamp: DateTimeOffset.UtcNow,
                Id: Guid.NewGuid().ToString("N")));

            GD.Print($"[NpcGuildArchetypesBootstrapper] Loaded npc guild archetypes count={catalog.Count} path={NpcGuildsPath}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[NpcGuildArchetypesBootstrapper] Failed to load npc_guilds path={NpcGuildsPath} exType={ex.GetType().Name} msg={ex.Message}");
        }
    }
}

