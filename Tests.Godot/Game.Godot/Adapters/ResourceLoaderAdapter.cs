using Godot;
using Game.Core.Ports;
using Game.Core.Domain;

namespace Game.Godot.Adapters;

public partial class ResourceLoaderAdapter : Node, IResourceLoader
{
    public string? LoadText(SafeResourcePath path)
    {
        try
        {
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
