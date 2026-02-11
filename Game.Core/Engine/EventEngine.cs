using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Core.Contracts.Guild;
using Game.Core.Contracts;
using Game.Core.Domain.Turn;
using Game.Core.Ports;
using Game.Core.Services;

namespace Game.Core.Engine;

public sealed class EventEngine : IEventEngine
{
    private readonly IEventCatalog _eventCatalog;
    private readonly IEventBus _eventBus;
    private readonly ITime _time;
    private readonly IIdGenerator _idGenerator;
    private readonly AIEcosystem _aiEcosystem;
    private readonly IAICoordinator _aiCoordinator;
    private readonly IntimacySystem _intimacySystem;

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
        _intimacySystem = new IntimacySystem(_eventBus, _time, _idGenerator);
    }

    /// <summary>
    /// Generates a deterministic sequence of <see cref="DomainEvent"/> instances from a content-driven catalog.
    /// </summary>
    /// <remarks>
    /// Refs: ADR-0004 (event contracts), ADR-0005 (quality gates).
    /// This method is intentionally static and pure (no Godot dependencies) to support repeatable unit tests.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> is null.</exception>
    public static IEnumerable<DomainEvent> GenerateEvents(EventCatalog? catalog, int seed, DateTimeOffset now, int count)
    {
        if (catalog is null)
            throw new ArgumentNullException(nameof(catalog));

        if (count <= 0)
            yield break;

        var enabledTypes = catalog.GetEnabledEventTypes();
        if (enabledTypes.Count == 0)
            yield break;

        var rng = new Random(seed);
        var source = "EventEngine.GenerateEvents";
        var nowUnixMs = now.ToUnixTimeMilliseconds();

        for (var i = 0; i < count; i++)
        {
            var eventType = enabledTypes[rng.Next(enabledTypes.Count)];
            yield return new DomainEvent(
                Type: eventType,
                Source: source,
                Data: null,
                Timestamp: now.UtcDateTime.AddSeconds(i),
                Id: $"{seed}:{nowUnixMs}:{i}");
        }
    }

    public async Task<GameTurnState> ExecuteResolutionPhaseAsync(GameTurnState state)
    {
        var guildCreated = new GuildCreated(
            GuildId: "temp-guild-id",
            CreatorId: "temp-creator-id",
            GuildName: "Temp Guild",
            CreatedAt: _time.UtcNowOffset
        );

        await PublishAsync(GuildCreated.EventType, nameof(EventEngine), guildCreated);
        return state;
    }

    public async Task<GameTurnState> ExecutePlayerPhaseAsync(GameTurnState state)
    {
        var memberJoined = new GuildMemberJoined(
            UserId: "temp-user-id",
            GuildId: "temp-guild-id",
            JoinedAt: _time.UtcNowOffset,
            Role: "member"
        );

        await PublishAsync(GuildMemberJoined.EventType, nameof(EventEngine), memberJoined);

        // T18 minimal: Each player phase triggers a deterministic "social interaction" effect
        // between the creator and the joined member, updating relationship value and emitting
        // core.social.relationship.changed (ADR-0004).
        await _intimacySystem.ApplyInteractionAsync(
            guildId: memberJoined.GuildId,
            subjectId: memberJoined.UserId,
            otherId: "temp-creator-id",
            delta: 1);

        return state;
    }

    public async Task<GameTurnState> ExecuteAiPhaseAsync(GameTurnState state)
    {
        if (state is null)
            return state!;

        var aiCoordinatorEvents = _aiCoordinator.GenerateAiEvents(state);
        foreach (var evt in aiCoordinatorEvents)
        {
            if (_eventCatalog.IsEventEnabled(evt.Type))
                await _eventBus.PublishAsync(evt);
        }

        var events = _aiEcosystem.Advance(state);
        foreach (var evt in events)
        {
            if (_eventCatalog.IsEventEnabled(evt.Type))
                await _eventBus.PublishAsync(evt);
        }
        return state;
    }

    private Task PublishAsync(string type, string source, object? data)
    {
        if (!_eventCatalog.IsEventEnabled(type))
            return Task.CompletedTask;

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

