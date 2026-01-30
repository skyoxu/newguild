using System;
using System.Collections.Generic;
using System.Threading;
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

    private readonly IDisposable _subscription;
    private readonly object _gate = new();
    private readonly HashSet<string> _unlockedTriggers = new(StringComparer.Ordinal);
    private int _unlockedCount;

    public int UnlockedCount => _unlockedCount;

    public event EventHandler<AchievementCountChanged>? UnlockedCountChanged;

    public AchievementTracker(IEventBus eventBus)
    {
        if (eventBus == null)
            throw new ArgumentNullException(nameof(eventBus));
        _subscription = eventBus.Subscribe(OnEventAsync);
    }

    private Task OnEventAsync(DomainEvent evt)
    {
        if (!TriggerEventTypes.Contains(evt.Type))
            return Task.CompletedTask;

        int newCount;
        lock (_gate)
        {
            if (!_unlockedTriggers.Add(evt.Type))
                return Task.CompletedTask;
            newCount = Interlocked.Increment(ref _unlockedCount);
        }

        UnlockedCountChanged?.Invoke(this, new AchievementCountChanged(newCount, evt.Type));
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _subscription.Dispose();
    }
}
