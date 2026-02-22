using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Game.Core.Contracts;
using Game.Core.Contracts.Achievements;
using Game.Core.Contracts.Guild;
using Game.Core.Contracts.Media;
using Game.Core.Contracts.Raid;
using Game.Core.Contracts.Recruitment;
using Game.Core.Services;

namespace Game.Core.Domain.Achievements;

public sealed class AchievementTracker : IDisposable
{
    private static readonly HashSet<string> TriggerEventTypes = new(StringComparer.Ordinal)
    {
        GuildCreated.EventType,
        MediaBeatTriggered.EventType,
        RaidResolved.EventType,
        RecruitmentOfferResolved.EventType,
        ReputationChanged.EventType,
    };

    private static readonly ConditionalWeakTable<IEventBus, TrackerState> SharedStates = new();

    private readonly IDisposable _subscription;
    private readonly TrackerState _state;
    private int _unlockedCount;

    public int UnlockedCount => _unlockedCount;

    public event EventHandler<AchievementCountChanged>? UnlockedCountChanged;

    public AchievementTracker(IEventBus eventBus)
    {
        if (eventBus == null)
            throw new ArgumentNullException(nameof(eventBus));

        _state = SharedStates.GetOrCreateValue(eventBus);
        lock (_state.Gate)
        {
            _unlockedCount = _state.UnlockedCount;
        }

        _subscription = eventBus.Subscribe(OnEventAsync);
    }

    private Task OnEventAsync(DomainEvent evt)
    {
        if (!TriggerEventTypes.Contains(evt.Type))
            return Task.CompletedTask;

        int newCount;
        lock (_state.Gate)
        {
            if (!_state.UnlockedTriggers.Add(evt.Type))
                return Task.CompletedTask;

            newCount = ++_state.UnlockedCount;
            _unlockedCount = newCount;
        }

        UnlockedCountChanged?.Invoke(this, new AchievementCountChanged(newCount, evt.Type));
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _subscription.Dispose();
    }

    private sealed class TrackerState
    {
        public object Gate { get; } = new();
        public HashSet<string> UnlockedTriggers { get; } = new(StringComparer.Ordinal);
        public int UnlockedCount { get; set; }
    }
}
