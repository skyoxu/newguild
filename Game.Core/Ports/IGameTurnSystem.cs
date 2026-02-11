using Game.Core.Domain.Turn;

namespace Game.Core.Ports;

public interface IGameTurnSystem
{
    GameTurnState StartNewWeek(SaveIdValue saveId);
    Task<GameTurnState> Advance(GameTurnState state);
}

