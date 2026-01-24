using System;
using Game.Core.Contracts;
using Game.Core.Contracts.Content;
using Game.Core.Domain;
using Game.Core.Services.Content;
using Godot;

namespace Game.Godot.Scripts.Bootstrap;

/// <summary>
/// Startup bootstrap: loads the content manifest from res:// and publishes a domain event.
/// </summary>
/// <remarks>
/// Refs: ADR-0004 (event contracts), ADR-0019 (safe paths).
/// Event: <c>core.content.manifest.loaded</c>.
/// </remarks>
public sealed partial class ContentManifestBootstrapper : Node
{
    private const string ManifestPath = "res://Game.Godot/Assets/Data/content/base/manifest.json";

    public override void _Ready()
    {
        // Run on main thread so EventBusAdapter emits synchronously.
        BootstrapOnce();
    }

    private void BootstrapOnce()
    {
        var bus = GetNodeOrNull<Game.Godot.Adapters.EventBusAdapter>("/root/EventBus");
        if (bus is null)
        {
            GD.PushWarning("[ContentManifestBootstrapper] EventBus not found; skipping manifest load.");
            return;
        }

        var safeManifestPath = SafeResourcePath.FromString(ManifestPath);
        if (safeManifestPath is null || safeManifestPath.Type != PathType.ReadOnly)
        {
            GD.PushWarning($"[ContentManifestBootstrapper] Manifest path is not a res:// path: '{ManifestPath}'.");
            return;
        }

        // Do not rely on /root/ResourceLoader (it may not exist before CompositionRoot finishes wiring).
        var loader = new Game.Godot.Adapters.ResourceLoaderAdapter();
        var json = loader.LoadText(safeManifestPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            GD.PushWarning($"[ContentManifestBootstrapper] Manifest is missing or empty: '{ManifestPath}'.");
            return;
        }

        try
        {
            var manifest = ContentManifestParser.Parse(json);
            var loadedAt = DateTimeOffset.UtcNow;

            var evt = new ContentManifestLoaded(
                ManifestId: manifest.ManifestId,
                SchemaVersion: manifest.SchemaVersion,
                EntryCount: manifest.Entries.Count,
                LoadedAt: loadedAt);

            _ = bus.PublishAsync(new DomainEvent(
                Type: ContentManifestLoaded.EventType,
                Source: nameof(ContentManifestBootstrapper),
                Data: evt,
                Timestamp: DateTime.UtcNow,
                Id: Guid.NewGuid().ToString("N")));

            GD.Print($"[ContentManifestBootstrapper] Loaded manifest id={manifest.ManifestId} entries={manifest.Entries.Count} path={ManifestPath}");
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[ContentManifestBootstrapper] Failed to load manifest path={ManifestPath} exType={ex.GetType().Name} msg={ex.Message}");
        }
    }
}

