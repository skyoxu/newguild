using Game.Core.Services;
using Game.Godot.Adapters;
using Godot;

namespace Game.Godot.Scripts.UI;

public partial class ThemeApplier : Node
{
    private SecurityFileAdapter? _securityFileAdapter;

    [Export]
    public string FontPath { get; set; } = "res://Game.Godot/Fonts/NotoSans-Regular.ttf";

    public override void _Ready()
    {
        _ = GetSecurityFileAdapter();
        TryApplyFont(FontPath);
    }

    private SecurityFileAdapter? GetSecurityFileAdapter()
    {
        if (_securityFileAdapter != null) return _securityFileAdapter;

        var bus = GetNodeOrNull<EventBusAdapter>("/root/EventBus");
        if (bus == null)
        {
            GD.PushWarning("[ThemeApplier] EventBus not found at /root/EventBus; font validation will be skipped.");
            return null;
        }

        _securityFileAdapter = new SecurityFileAdapter(bus);
        return _securityFileAdapter;
    }

    private void TryApplyFont(string path)
    {
        var sec = GetSecurityFileAdapter();
        if (sec == null)
            return;

        // Validate font path using SecurityFileAdapter
        var validatedPath = sec.ValidateReadPath(path);
        if (validatedPath == null)
        {
            GD.PrintErr($"[ThemeApplier] Font path validation failed: {path}");
            return;
        }

        if (!FileAccess.FileExists(validatedPath.Value))
            return;

        var font = ResourceLoader.Load<FontFile>(validatedPath.Value);
        if (font == null)
            return;

        ApplyFontToControls(GetTree().Root, font);
    }

    private void ApplyFontToControls(Node root, Font font)
    {
        if (root is Control c)
        {
            c.AddThemeFontOverride("font", font);
        }
        foreach (var child in root.GetChildren())
        {
            if (child is Node n)
                ApplyFontToControls(n, font);
        }
    }
}
