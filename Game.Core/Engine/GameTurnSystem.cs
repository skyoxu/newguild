using Game.Core.Contracts;
using Game.Core.Contracts.GameLoop;
using Game.Core.Domain.Turn;
using Game.Core.Ports;
using Game.Core.Services;

namespace Game.Core.Engine;

public interface IGameTurnSystem
{
    GameTurnState StartNewWeek(string saveId);
    Task<GameTurnState> Advance(GameTurnState state);
}

public sealed class GameTurnSystem : IGameTurnSystem
{
    private readonly IEventEngine _eventEngine;
    private readonly IAICoordinator _aiCoordinator;
    private readonly IEventBus _eventBus;
    private readonly ITime _time;
    private bool _firstTurnStarted;

    public GameTurnSystem(
        IEventEngine eventEngine,
        IAICoordinator aiCoordinator,
        IEventBus eventBus,
        ITime time)
    {
        _eventEngine = eventEngine;
        _aiCoordinator = aiCoordinator;
        _eventBus = eventBus;
        _time = time;
        _firstTurnStarted = false;
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

    public async Task<GameTurnState> Advance(GameTurnState state)
    {
        // Publish GameTurnStarted event only on first turn
        if (!_firstTurnStarted)
        {
            _firstTurnStarted = true;
            var startedEvent = WrapEvent(new GameTurnStarted(
                SaveId: state.SaveId,
                Week: state.Week,
                Phase: state.Phase.ToString(),
                StartedAt: DateTimeOffset.UtcNow
            ), GameTurnStarted.EventType);
            await _eventBus.PublishAsync(startedEvent);
        }

        var nextState = state.Phase switch
        {
            GameTurnPhase.Resolution => await _eventEngine.ExecuteResolutionPhaseAsync(state) with
            {
                Phase = GameTurnPhase.Player
            },
            GameTurnPhase.Player => await _eventEngine.ExecutePlayerPhaseAsync(state) with
            {
                Phase = GameTurnPhase.AiSimulation
            },
            GameTurnPhase.AiSimulation => await _eventEngine.ExecuteAiPhaseAsync(state) with
            {
                Phase = GameTurnPhase.Resolution,
                Week = state.Week + 1
            },
            _ => state
        };

        // Publish phase changed event if phase transitioned
        if (nextState.Phase != state.Phase && nextState.Week == state.Week)
        {
            var phaseChangedEvent = WrapEvent(new GameTurnPhaseChanged(
                SaveId: state.SaveId,
                Week: state.Week,
                PreviousPhase: state.Phase.ToString(),
                CurrentPhase: nextState.Phase.ToString(),
                ChangedAt: DateTimeOffset.UtcNow
            ), GameTurnPhaseChanged.EventType);
            await _eventBus.PublishAsync(phaseChangedEvent);
        }

        // Publish week advanced event if week incremented
        if (nextState.Week > state.Week)
        {
            var weekAdvancedEvent = WrapEvent(new GameWeekAdvanced(
                SaveId: state.SaveId,
                PreviousWeek: state.Week,
                CurrentWeek: nextState.Week,
                AdvancedAt: DateTimeOffset.UtcNow
            ), GameWeekAdvanced.EventType);
            await _eventBus.PublishAsync(weekAdvancedEvent);
        }

        return nextState;
    }

    private static DomainEvent WrapEvent(object data, string eventType)
    {
        return new DomainEvent(
            Type: eventType,
            Source: "GameTurnSystem",
            Data: data,
            Timestamp: DateTime.UtcNow,
            Id: Guid.NewGuid().ToString()
        );
    }
}

