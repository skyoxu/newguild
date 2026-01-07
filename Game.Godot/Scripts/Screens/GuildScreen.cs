using Godot;

namespace Game.Godot.Scripts.Screens;

public partial class GuildScreen : Control
{
    public override void _Ready()
    {
        var back = GetNodeOrNull<Button>("Top/Back");
        if (back != null)
            back.Pressed += OnBack;
    }

    public void Enter()
    {
        GD.Print("[GuildScreen] Enter");
    }

    public void Exit()
    {
        GD.Print("[GuildScreen] Exit");
    }

    private void OnBack()
    {
        var nav = GetNodeOrNull<Node>("/root/Main/ScreenNavigator");
        if (nav != null && nav.HasMethod("Clear"))
            nav.Call("Clear");

        var menu = GetNodeOrNull<Node>("/root/Main/MainMenu");
        if (menu != null && menu.HasMethod("ShowMenu"))
            menu.Call("ShowMenu");
    }
}
