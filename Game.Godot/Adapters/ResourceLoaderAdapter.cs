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
            using var f = FileAccess.Open(path.Value, FileAccess.ModeFlags.Read);
            if (f == null) return null;
            return f.GetAsText();
        }
        catch
        {
            return null;
        }
    }

    public byte[]? LoadBytes(SafeResourcePath path)
    {
        try
        {
            // SafeResourcePath guarantees path safety at type level
            using var f = FileAccess.Open(path.Value, FileAccess.ModeFlags.Read);
            if (f == null) return null;
            return f.GetBuffer((long)f.GetLength());
        }
        catch
        {
            return null;
        }
    }
}
