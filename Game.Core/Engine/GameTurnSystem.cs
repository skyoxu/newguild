using Game.Core.Contracts;
using Game.Contracts.GameLoop;
using Game.Core.Domain.Turn;
using Game.Core.Ports;
using Game.Core.Services;

namespace Game.Core.Engine;

public interface IGameTurnSystem
{
    GameTurnState StartNewWeek(SaveIdValue saveId);
    Task<GameTurnState> Advance(GameTurnState state);
}

public sealed class GameTurnSystem : IGameTurnSystem
{
    private readonly IEventEngine _eventEngine;
    private readonly IAICoordinator _aiCoordinator;
    private readonly IEventBus _eventBus;
    private readonly ITime _time;
    private readonly IIdGenerator _idGenerator;
    private bool _firstTurnStarted;

    public GameTurnSystem(
        IEventEngine eventEngine,
        IAICoordinator aiCoordinator,
        IEventBus eventBus,
        ITime time,
        IIdGenerator? idGenerator = null)
    {
        _eventEngine = eventEngine;
        _aiCoordinator = aiCoordinator;
        _eventBus = eventBus;
        _time = time;
        _idGenerator = idGenerator ?? new GuidIdGenerator();
        _firstTurnStarted = false;
    }

    public GameTurnState StartNewWeek(SaveIdValue saveId)
    {
        System.ArgumentNullException.ThrowIfNull(saveId);

        return new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.Resolution,
            SaveId: saveId,
            CurrentTime: _time.UtcNowOffset
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
                StartedAt: _time.UtcNowOffset
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
                ChangedAt: _time.UtcNowOffset
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
                AdvancedAt: _time.UtcNowOffset
            ), GameWeekAdvanced.EventType);
            await _eventBus.PublishAsync(weekAdvancedEvent);
        }

        return nextState;
    }

    private DomainEvent WrapEvent(object data, string eventType)
    {
        var now = _time.UtcNowOffset;
        return new DomainEvent(
            Type: eventType,
            Source: "GameTurnSystem",
            Data: data,
            Timestamp: now.UtcDateTime,
            Id: _idGenerator.NewId()
        );
    }
}

