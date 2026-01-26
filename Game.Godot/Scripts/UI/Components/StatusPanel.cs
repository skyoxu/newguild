using Godot;

namespace Game.Godot.Scripts.UI.Components;

/// <summary>
/// Reusable status panel component.
/// Intended for adapter/UI layer usage per ADR-0018.
/// </summary>
public partial class StatusPanel : PanelContainer
{
    [Export]
    public string TitleText { get; set; } = "Status";

    [Export]
    public string MessageText { get; set; } = "...";

    private Label _titleLabel = default!;
    private Label _messageLabel = default!;

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("Root/Title");
        _messageLabel = GetNode<Label>("Root/Message");
        Apply();
    }

    public void SetStatus(string title, string message)
    {
        TitleText = title;
        MessageText = message;
        ApplyIfReady();
    }

    public void Refresh()
    {
        ApplyIfReady();
    }

    private void Apply()
    {
        _titleLabel.Text = TitleText;
        _messageLabel.Text = MessageText;
    }

    private void ApplyIfReady()
    {
        if (!IsNodeReady())
            return;

        Apply();
    }
}
