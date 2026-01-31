using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Core.Contracts;
using Game.Core.Contracts.Media;
using Game.Core.Ports;

namespace Game.Core.Services;

/// <summary>
/// Minimal reputation system for Task 19 (Media / Reputation).
/// Tracks per-guild reputation value and aggregates deltas by source id.
/// </summary>
public sealed class ReputationSystem
{
    public const int MinReputation = 0;
    public const int MaxReputation = 100;

    private readonly IEventBus _eventBus;
    private readonly ITime _time;
    private readonly IIdGenerator _idGenerator;
    private readonly Dictionary<string, int> _valuesByGuildId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, int>> _sourceTotalsByGuildId = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public ReputationSystem(IEventBus eventBus, ITime? time = null, IIdGenerator? idGenerator = null)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _time = time ?? new SystemTime();
        _idGenerator = idGenerator ?? new GuidIdGenerator();
    }

    public int GetReputation(string guildId)
    {
        if (string.IsNullOrWhiteSpace(guildId))
            throw new ArgumentException("GuildId is required.", nameof(guildId));

        guildId = guildId.Trim();
        lock (_gate)
        {
            return _valuesByGuildId.TryGetValue(guildId, out var value) ? value : MinReputation;
        }
    }

    public IReadOnlyDictionary<string, int> GetSourceTotals(string guildId)
    {
        if (string.IsNullOrWhiteSpace(guildId))
            throw new ArgumentException("GuildId is required.", nameof(guildId));

        guildId = guildId.Trim();
        lock (_gate)
        {
            if (!_sourceTotalsByGuildId.TryGetValue(guildId, out var totals))
                return new Dictionary<string, int>(StringComparer.Ordinal);

            return new Dictionary<string, int>(totals, StringComparer.Ordinal);
        }
    }

    public async Task<int> ApplyDeltaAsync(string guildId, int delta, string reason, string sourceId)
    {
        if (string.IsNullOrWhiteSpace(guildId))
            throw new ArgumentException("GuildId is required.", nameof(guildId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));
        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("SourceId is required.", nameof(sourceId));

        guildId = guildId.Trim();
        reason = reason.Trim();
        sourceId = sourceId.Trim();

        int oldValue;
        int newValue;
        lock (_gate)
        {
            oldValue = _valuesByGuildId.TryGetValue(guildId, out var value) ? value : MinReputation;
            newValue = Clamp(oldValue + delta);
            _valuesByGuildId[guildId] = newValue;

            if (!_sourceTotalsByGuildId.TryGetValue(guildId, out var totals))
            {
                totals = new Dictionary<string, int>(StringComparer.Ordinal);
                _sourceTotalsByGuildId[guildId] = totals;
            }

            totals.TryGetValue(sourceId, out var existing);
            totals[sourceId] = existing + delta;
        }

        if (newValue != oldValue)
        {
            var now = _time.UtcNowOffset;
            var contract = new ReputationChanged(
                GuildId: guildId,
                OldValue: oldValue,
                NewValue: newValue,
                Reason: reason,
                ChangedAt: now);

            var evt = new DomainEvent(
                Type: ReputationChanged.EventType,
                Source: nameof(ReputationSystem),
                Data: contract,
                Timestamp: now.UtcDateTime,
                Id: _idGenerator.NewId());

            await _eventBus.PublishAsync(evt).ConfigureAwait(false);
        }

        return newValue;
    }

    private static int Clamp(int value)
    {
        if (value < MinReputation)
            return MinReputation;
        if (value > MaxReputation)
            return MaxReputation;
        return value;
    }
}

