using Game.Core.Contracts;
using Game.Core.Domain.Turn;

namespace Game.Core.Ports;

public interface IAICoordinator
{
    IReadOnlyList<DomainEvent> GenerateAiEvents(GameTurnState state);
}

