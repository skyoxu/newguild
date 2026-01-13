using Godot;
using System;

namespace Game.Godot.Scripts.Autoload;

/// <summary>
/// Local player session (single-player) for providing a single source of truth
/// for the current user identity across UI and adapters.
/// </summary>
public partial class PlayerSession : Node
{
    public string CurrentUserId { get; private set; } = "player1";

    public override void _Ready()
    {
        var envUserId = System.Environment.GetEnvironmentVariable("GD_PLAYER_ID");
        if (!string.IsNullOrWhiteSpace(envUserId))
            CurrentUserId = envUserId.Trim();

#if DEBUG
        GD.Print($"[PlayerSession] CurrentUserId={CurrentUserId}");
#endif
    }

    public void SetCurrentUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return;

        CurrentUserId = userId.Trim();
#if DEBUG
        GD.Print($"[PlayerSession] CurrentUserId={CurrentUserId}");
#endif
    }
}
