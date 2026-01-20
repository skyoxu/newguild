using System;
using System.Threading.Tasks;
using Game.Core.Contracts;
using Game.Core.Contracts.Media;
using Game.Core.Ports;

namespace Game.Core.Services;

/// <summary>
/// Minimal media beat producer for Task 19 (Media / Reputation).
/// Emits <see cref="MediaBeatTriggered"/> as a domain event when upstream gameplay triggers a media beat.
/// </summary>
public sealed class MediaBeatSystem
{
    private readonly IEventBus _eventBus;
    private readonly ITime _time;
    private readonly IIdGenerator _idGenerator;

    public MediaBeatSystem(IEventBus eventBus, ITime? time = null, IIdGenerator? idGenerator = null)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _time = time ?? new SystemTime();
        _idGenerator = idGenerator ?? new GuidIdGenerator();
    }

    public async Task TriggerBeatAsync(string beatId, string guildId, string sourceEventType, string headline)
    {
        if (string.IsNullOrWhiteSpace(beatId))
            throw new ArgumentException("BeatId is required.", nameof(beatId));
        if (string.IsNullOrWhiteSpace(guildId))
            throw new ArgumentException("GuildId is required.", nameof(guildId));
        if (string.IsNullOrWhiteSpace(sourceEventType))
            throw new ArgumentException("SourceEventType is required.", nameof(sourceEventType));
        if (string.IsNullOrWhiteSpace(headline))
            throw new ArgumentException("Headline is required.", nameof(headline));

        var now = _time.UtcNowOffset;
        var contract = new MediaBeatTriggered(
            BeatId: beatId.Trim(),
            GuildId: guildId.Trim(),
            SourceEventType: sourceEventType.Trim(),
            Headline: headline.Trim(),
            TriggeredAt: now);

        var evt = new DomainEvent(
            Type: MediaBeatTriggered.EventType,
            Source: nameof(MediaBeatSystem),
            Data: contract,
            Timestamp: now.UtcDateTime,
            Id: _idGenerator.NewId());

        await _eventBus.PublishAsync(evt).ConfigureAwait(false);
    }
}

