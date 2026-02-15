using Godot;
using PhaseChangedContract = Game.Contracts.GameLoop.GameTurnPhaseChanged;
using WeekAdvancedContract = Game.Contracts.GameLoop.GameWeekAdvanced;

namespace Tests.Godot.Support;

public partial class GameTurnEventTypesBridge : RefCounted
{
    public string GetPhaseChangedEventType() => PhaseChangedContract.EventType;

    public string GetWeekAdvancedEventType() => WeekAdvancedContract.EventType;
}

