using Godot;
using Game.Godot.Adapters;

namespace Game.Godot.Scripts.UI;

public partial class MainMenu : Control
{
    private Button _btnPlay = default!;
    private Button _btnGuild = default!;
    private Button _btnSettings = default!;
    private Button _btnQuit = default!;

    public override void _Ready()
    {
        _btnPlay = GetNode<Button>("VBox/BtnPlay");
        _btnGuild = GetNode<Button>("VBox/BtnGuild");
        _btnSettings = GetNode<Button>("VBox/BtnSettings");
        _btnQuit = GetNode<Button>("VBox/BtnQuit");

        _btnPlay.Pressed += OnPlayPressed;
        _btnGuild.Pressed += OnGuildPressed;
        _btnSettings.Pressed += OnSettingsPressed;
        _btnQuit.Pressed += OnQuitPressed;
    }

    public void ShowMenu() => Visible = true;
    public void HideMenu() => Visible = false;

    private void Publish(string type, string source, string dataJson = "{}")
    {
        var bus = GetNodeOrNull<EventBusAdapter>("/root/EventBus");
        bus?.PublishSimple(type, source, dataJson);
    }

    private void OnPlayPressed()
    {
        Publish("ui.menu.start", "ui");
        HideMenu();
    }

    private void OnGuildPressed()
    {
        Publish("ui.menu.guild", "ui");
        HideMenu();
    }

    private void OnSettingsPressed()
    {
        Publish("ui.menu.settings", "ui");
    }

    private void OnQuitPressed()
    {
        Publish("ui.menu.quit", "ui");
        GetTree().Quit();
    }
}
