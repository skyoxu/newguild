using Godot;

namespace Game.Godot.Scripts.UI.Components;

/// <summary>
/// Reusable confirm dialog-like panel component.
/// Intended for adapter/UI layer usage per ADR-0018.
/// </summary>
public partial class ConfirmDialogPanel : PanelContainer
{
    [Signal]
    public delegate void ConfirmedEventHandler();

    [Signal]
    public delegate void CancelledEventHandler();

    [Export]
    public string TitleText { get; set; } = "Confirm";

    [Export]
    public string MessageText { get; set; } = "Are you sure?";

    [Export]
    public string ConfirmText { get; set; } = "Confirm";

    [Export]
    public string CancelText { get; set; } = "Cancel";

    [Export]
    public bool CancelVisible { get; set; } = true;

    private Label _titleLabel = default!;
    private Label _messageLabel = default!;
    private Button _confirmButton = default!;
    private Button _cancelButton = default!;

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("Root/Title");
        _messageLabel = GetNode<Label>("Root/Message");
        _confirmButton = GetNode<Button>("Root/Buttons/ConfirmButton");
        _cancelButton = GetNode<Button>("Root/Buttons/CancelButton");

        _confirmButton.Pressed += () => EmitSignal(SignalName.Confirmed);
        _cancelButton.Pressed += () => EmitSignal(SignalName.Cancelled);

        Apply();
    }

    public void SetPrompt(string title, string message)
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
        _confirmButton.Text = ConfirmText;
        _cancelButton.Text = CancelText;
        _cancelButton.Visible = CancelVisible;
    }

    private void ApplyIfReady()
    {
        if (!IsNodeReady())
            return;

        Apply();
    }
}
