using Game.Core.Domain.Turn;

namespace Game.Core.Ports;

public interface IEventEngine
{
    Task<GameTurnState> ExecuteResolutionPhaseAsync(GameTurnState state);
    Task<GameTurnState> ExecutePlayerPhaseAsync(GameTurnState state);
    Task<GameTurnState> ExecuteAiPhaseAsync(GameTurnState state);
}

