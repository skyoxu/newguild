using System;
using System.Threading.Tasks;
using Game.Core.Contracts.Guild;
using Game.Core.Contracts;
using Game.Core.Domain.Turn;
using Game.Core.Ports;
using Game.Core.Services;

namespace Game.Core.Engine;

public interface IEventEngine
{
    Task<GameTurnState> ExecuteResolutionPhaseAsync(GameTurnState state);
    Task<GameTurnState> ExecutePlayerPhaseAsync(GameTurnState state);
    Task<GameTurnState> ExecuteAiPhaseAsync(GameTurnState state);
}

public sealed class EventEngine : IEventEngine
{
    private readonly IEventCatalog _eventCatalog;
    private readonly IEventBus _eventBus;
    private readonly ITime _time;
    private readonly IIdGenerator _idGenerator;
    private readonly AIEcosystem _aiEcosystem;
    private readonly IAICoordinator _aiCoordinator;

    public EventEngine(
        IEventCatalog eventCatalog,
        IEventBus eventBus,
        ITime? time = null,
        IIdGenerator? idGenerator = null,
        AIEcosystem? aiEcosystem = null,
        IAICoordinator? aiCoordinator = null)
    {
        _eventCatalog = eventCatalog ?? throw new ArgumentNullException(nameof(eventCatalog));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _time = time ?? new SystemTime();
        _idGenerator = idGenerator ?? new GuidIdGenerator();
        _aiEcosystem = aiEcosystem ?? new AIEcosystem(_time, _idGenerator, seed: 1);
        _aiCoordinator = aiCoordinator ?? new AICoordinator();
    }

    public async Task<GameTurnState> ExecuteResolutionPhaseAsync(GameTurnState state)
    {
        // T2 minimal: Publish GuildCreated event
        var guildCreated = new GuildCreated(
            GuildId: "temp-guild-id",
            CreatorId: "temp-creator-id",
            GuildName: "Temp Guild",
            CreatedAt: _time.UtcNowOffset
        );

        var now = _time.UtcNowOffset;
        var domainEvent = new DomainEvent(
            Type: GuildCreated.EventType,
            Source: "EventEngine",
            Data: guildCreated,
            Timestamp: now.UtcDateTime,
            Id: _idGenerator.NewId()
        );

        await _eventBus.PublishAsync(domainEvent);
        return state;
    }

    public async Task<GameTurnState> ExecutePlayerPhaseAsync(GameTurnState state)
    {
        // T2 minimal: Publish GuildMemberJoined event
        var memberJoined = new GuildMemberJoined(
            UserId: "temp-user-id",
            GuildId: "temp-guild-id",
            JoinedAt: _time.UtcNowOffset,
            Role: "member"
        );

        var now = _time.UtcNowOffset;
        var domainEvent = new DomainEvent(
            Type: GuildMemberJoined.EventType,
            Source: "EventEngine",
            Data: memberJoined,
            Timestamp: now.UtcDateTime,
            Id: _idGenerator.NewId()
        );

        await _eventBus.PublishAsync(domainEvent);
        return state;
    }

    public async Task<GameTurnState> ExecuteAiPhaseAsync(GameTurnState state)
    {
        if (state is null)
            return state!;

        var aiCoordinatorEvents = _aiCoordinator.GenerateAiEvents(state);
        foreach (var evt in aiCoordinatorEvents)
            await _eventBus.PublishAsync(evt);

        var events = _aiEcosystem.Advance(state);
        foreach (var evt in events)
            await _eventBus.PublishAsync(evt);
        return state;
    }

    private Task PublishAsync(string type, string source, object? data)
    {
        var now = _time.UtcNowOffset;
        var evt = new DomainEvent(
            Type: type,
            Source: source,
            Data: data,
            Timestamp: now.UtcDateTime,
            Id: _idGenerator.NewId()
        );

        return _eventBus.PublishAsync(evt);
    }
}

