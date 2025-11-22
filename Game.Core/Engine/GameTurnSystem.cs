using Game.Core.Domain.Turn;

namespace Game.Core.Engine;

public interface IGameTurnSystem
{
    GameTurnState StartNewWeek(string saveId);
    GameTurnState Advance(GameTurnState state);
}

public sealed class GameTurnSystem : IGameTurnSystem
{
    private readonly IEventEngine _eventEngine;
    private readonly IAICoordinator _aiCoordinator;

    public GameTurnSystem(IEventEngine eventEngine, IAICoordinator aiCoordinator)
    {
        _eventEngine = eventEngine;
        _aiCoordinator = aiCoordinator;
    }

    public GameTurnState StartNewWeek(string saveId)
    {
        return new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.Resolution,
            SaveId: saveId,
            CurrentTime: System.DateTimeOffset.UtcNow
        );
    }

    public GameTurnState Advance(GameTurnState state)
    {
        return state.Phase switch
        {
            GameTurnPhase.Resolution => _eventEngine.ExecuteResolutionPhase(state) with
            {
                Phase = GameTurnPhase.Player
            },
            GameTurnPhase.Player => _eventEngine.ExecutePlayerPhase(state) with
            {
                Phase = GameTurnPhase.AiSimulation
            },
            GameTurnPhase.AiSimulation => _eventEngine.ExecuteAiPhase(state) with
            {
                Phase = GameTurnPhase.Resolution,
                Week = state.Week + 1
            },
            _ => state
        };
    }
}

