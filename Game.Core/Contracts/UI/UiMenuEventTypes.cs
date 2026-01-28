namespace Game.Core.Contracts.UI;

/// <summary>
/// UI menu event types for main navigation.
/// </summary>
/// <remarks>
/// ADR-0004: event type naming rules.
/// </remarks>
public static class UiMenuEventTypes
{
    public const string Start = "ui.menu.start";
    public const string Guild = "ui.menu.guild";
    public const string Settings = "ui.menu.settings";
    public const string Activity = "ui.menu.activity";
    public const string Quit = "ui.menu.quit";
}
