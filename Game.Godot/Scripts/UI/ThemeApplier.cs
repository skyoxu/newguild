using Game.Core.Services;
using Godot;

namespace Game.Godot.Scripts.UI;

public partial class ThemeApplier : Node
{
    private readonly SecurityFileAdapter _securityFileAdapter;

    [Export]
    public string FontPath { get; set; } = "res://Game.Godot/Fonts/NotoSans-Regular.ttf";

    public ThemeApplier(SecurityFileAdapter securityFileAdapter)
    {
        _securityFileAdapter = securityFileAdapter ?? throw new System.ArgumentNullException(nameof(securityFileAdapter));
    }

    public override void _Ready()
    {
        TryApplyFont(FontPath);
    }

    private void TryApplyFont(string path)
    {
        // Validate font path using SecurityFileAdapter
        var validatedPath = _securityFileAdapter.ValidateReadPath(path);
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

