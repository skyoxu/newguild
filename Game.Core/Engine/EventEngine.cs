using System;
using System.Threading.Tasks;
using Game.Core.Contracts;
using Game.Core.Domain.Turn;
using Game.Core.Ports;
using Game.Core.Services;

namespace Game.Core.Engine;

public interface IEventEngine
{
    GameTurnState ExecuteResolutionPhase(GameTurnState state);
    GameTurnState ExecutePlayerPhase(GameTurnState state);
    GameTurnState ExecuteAiPhase(GameTurnState state);
}

public sealed class EventEngine : IEventEngine
{
    private readonly IEventCatalog _eventCatalog;
    private readonly IEventBus _eventBus;

    public EventEngine(IEventCatalog eventCatalog, IEventBus eventBus)
    {
        _eventCatalog = eventCatalog;
        _eventBus = eventBus;
    }

    public GameTurnState ExecuteResolutionPhase(GameTurnState state)
    {
        return state;
    }

    public GameTurnState ExecutePlayerPhase(GameTurnState state)
    {
        return state;
    }

    public GameTurnState ExecuteAiPhase(GameTurnState state)
    {
        return state;
    }

    private Task PublishAsync(string type, string source, object? data)
    {
        var evt = new DomainEvent(
            Type: type,
            Source: source,
            Data: data,
            Timestamp: DateTime.UtcNow,
            Id: Guid.NewGuid().ToString("N")
        );

        return _eventBus.PublishAsync(evt);
    }
}

