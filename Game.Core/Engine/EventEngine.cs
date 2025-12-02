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

    public EventEngine(IEventCatalog eventCatalog, IEventBus eventBus)
    {
        _eventCatalog = eventCatalog ?? throw new ArgumentNullException(nameof(eventCatalog));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    public async Task<GameTurnState> ExecuteResolutionPhaseAsync(GameTurnState state)
    {
        // T2 minimal: Publish GuildCreated event
        var guildCreated = new GuildCreated(
            GuildId: "temp-guild-id",
            CreatorId: "temp-creator-id",
            GuildName: "Temp Guild",
            CreatedAt: DateTimeOffset.UtcNow
        );

        var domainEvent = new DomainEvent(
            Type: GuildCreated.EventType,
            Source: "EventEngine",
            Data: guildCreated,
            Timestamp: DateTime.UtcNow,
            Id: Guid.NewGuid().ToString("N")
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
            JoinedAt: DateTimeOffset.UtcNow,
            Role: "member"
        );

        var domainEvent = new DomainEvent(
            Type: GuildMemberJoined.EventType,
            Source: "EventEngine",
            Data: memberJoined,
            Timestamp: DateTime.UtcNow,
            Id: Guid.NewGuid().ToString("N")
        );

        await _eventBus.PublishAsync(domainEvent);
        return state;
    }

    public async Task<GameTurnState> ExecuteAiPhaseAsync(GameTurnState state)
    {
        // T2 minimal: Publish GuildMemberLeft event
        var memberLeft = new GuildMemberLeft(
            UserId: "temp-user-id",
            GuildId: "temp-guild-id",
            LeftAt: DateTimeOffset.UtcNow,
            Reason: "voluntary"
        );

        var domainEvent = new DomainEvent(
            Type: GuildMemberLeft.EventType,
            Source: "EventEngine",
            Data: memberLeft,
            Timestamp: DateTime.UtcNow,
            Id: Guid.NewGuid().ToString("N")
        );

        await _eventBus.PublishAsync(domainEvent);
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

