using System;
using Godot;
using Game.Core.Ports;
using Game.Core.Domain;

namespace Game.Godot.Adapters;

/// <summary>
/// Adapter implementing IResourceLoader with Godot's FileAccess API.
/// Type-safe path validation enforced at compile time via SafeResourcePath.
/// Per ADR-0019: Only res:// (read-only) and user:// (read-write) paths allowed.
/// </summary>
public partial class ResourceLoaderAdapter : Node, IResourceLoader
{
    public string? LoadText(SafeResourcePath path)
    {
        try
        {
            // SafeResourcePath guarantees path safety at type level
            using var file = FileAccess.Open(path.Value, FileAccess.ModeFlags.Read);
            if (file == null) return null;
            return file.GetAsText();
        }
        catch
        {
            return null;
        }
    }

    // GDScript-friendly wrapper (tests and UI often start from raw strings).
    public string LoadTextFromString(string rawPath)
    {
        var safe = SafeResourcePath.FromString(rawPath);
        if (safe == null) return string.Empty;
        return LoadText(safe) ?? string.Empty;
    }

    public byte[]? LoadBytes(SafeResourcePath path)
    {
        try
        {
            // SafeResourcePath guarantees path safety at type level
            using var file = FileAccess.Open(path.Value, FileAccess.ModeFlags.Read);
            if (file == null) return null;
            return file.GetBuffer((long)file.GetLength());
        }
        catch
        {
            return null;
        }
    }

    public byte[] LoadBytesFromString(string rawPath)
    {
        var safe = SafeResourcePath.FromString(rawPath);
        if (safe == null) return Array.Empty<byte>();
        return LoadBytes(safe) ?? Array.Empty<byte>();
    }
}
