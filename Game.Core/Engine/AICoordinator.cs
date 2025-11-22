using Game.Core.Domain.Turn;

namespace Game.Core.Engine;

public interface IAICoordinator
{
    GameTurnState StepAiCycle(GameTurnState state);
}

public sealed class AICoordinator : IAICoordinator
{
    public GameTurnState StepAiCycle(GameTurnState state)
    {
        return state;
    }
}

