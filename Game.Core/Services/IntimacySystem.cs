using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Core.Contracts;
using Game.Core.Contracts.Social;
using Game.Core.Domain;
using Game.Core.Ports;

namespace Game.Core.Services;

/// <summary>
/// Minimal intimacy system for social relationships (Task 18).
/// </summary>
public sealed class IntimacySystem
{
    public const int MinIntimacy = IntimacyRules.MinIntimacy;
    public const int MaxIntimacy = IntimacyRules.MaxIntimacy;

    private readonly IEventBus _eventBus;
    private readonly ITime _time;
    private readonly IIdGenerator _idGenerator;
    private readonly Dictionary<GuildPairKey, int> _values = new();
    private readonly object _gate = new();

    public IntimacySystem(IEventBus eventBus, ITime? time = null, IIdGenerator? idGenerator = null)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _time = time ?? new SystemTime();
        _idGenerator = idGenerator ?? new GuidIdGenerator();
    }

    public int GetIntimacy(string guildId, string subjectId, string otherId)
    {
        EnsureValidInput(guildId, subjectId, otherId);

        var key = GuildPairKey.Create(guildId, subjectId, otherId);
        lock (_gate)
        {
            return _values.TryGetValue(key, out var value) ? value : MinIntimacy;
        }
    }

    public async Task<int> ApplyInteractionAsync(string guildId, string subjectId, string otherId, int delta)
    {
        EnsureValidInput(guildId, subjectId, otherId);

        var key = GuildPairKey.Create(guildId, subjectId, otherId);
        int oldValue;
        int newValue;
        lock (_gate)
        {
            oldValue = _values.TryGetValue(key, out var value) ? value : MinIntimacy;
            newValue = IntimacyRules.Clamp(oldValue + delta);
            _values[key] = newValue;
        }

        if (newValue != oldValue)
        {
            var contract = new SocialRelationshipChanged(
                GuildId: guildId,
                SubjectId: subjectId,
                OtherId: otherId,
                OldValue: oldValue,
                NewValue: newValue,
                ChangedAt: _time.UtcNowOffset);

            var now = _time.UtcNowOffset;
            var evt = new DomainEvent(
                Type: SocialRelationshipChanged.EventType,
                Source: nameof(IntimacySystem),
                Data: contract,
                Timestamp: now.UtcDateTime,
                Id: _idGenerator.NewId());

            await _eventBus.PublishAsync(evt);
        }

        return newValue;
    }

    private static void EnsureValidInput(string guildId, string subjectId, string otherId)
    {
        if (string.IsNullOrWhiteSpace(guildId))
            throw new ArgumentException("GuildId is required.", nameof(guildId));
        if (!IntimacyRules.IsValidPeerPair(subjectId, otherId))
            throw new ArgumentException("Invalid member pair.", nameof(subjectId));
    }

    private readonly record struct GuildPairKey(string GuildId, string A, string B)
    {
        public static GuildPairKey Create(string guildId, string subjectId, string otherId)
        {
            if (string.CompareOrdinal(subjectId, otherId) <= 0)
                return new GuildPairKey(guildId, subjectId, otherId);
            return new GuildPairKey(guildId, otherId, subjectId);
        }
    }
}
