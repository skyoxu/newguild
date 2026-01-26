using Godot;

namespace Game.Godot.Scripts.UI.Components;

/// <summary>
/// Reusable error panel component with optional Retry/Close actions.
/// Intended for adapter/UI layer usage per ADR-0018.
/// </summary>
public partial class ErrorPanel : PanelContainer
{
    [Signal]
    public delegate void RetryRequestedEventHandler();

    [Signal]
    public delegate void CloseRequestedEventHandler();

    [Export]
    public string TitleText { get; set; } = "Error";

    [Export]
    public string MessageText { get; set; } = "Unknown error";

    [Export]
    public bool RetryVisible { get; set; } = true;

    [Export]
    public bool CloseVisible { get; set; } = true;

    private Label _titleLabel = default!;
    private RichTextLabel _messageLabel = default!;
    private Button _retryButton = default!;
    private Button _closeButton = default!;

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("Root/Title");
        _messageLabel = GetNode<RichTextLabel>("Root/Message");
        _retryButton = GetNode<Button>("Root/Buttons/RetryButton");
        _closeButton = GetNode<Button>("Root/Buttons/CloseButton");

        _retryButton.Pressed += () => EmitSignal(SignalName.RetryRequested);
        _closeButton.Pressed += () => EmitSignal(SignalName.CloseRequested);

        Apply();
    }

    public void SetError(string title, string message)
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
        _retryButton.Visible = RetryVisible;
        _closeButton.Visible = CloseVisible;
    }

    private void ApplyIfReady()
    {
        if (!IsNodeReady())
            return;

        Apply();
    }
}
