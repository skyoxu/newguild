using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Core.Contracts;
using Game.Core.Contracts.Achievements;
using Game.Core.Contracts.Guild;
using Game.Core.Contracts.Media;
using Game.Core.Contracts.Raid;
using Game.Core.Contracts.Recruitment;
using Game.Core.Domain.Turn;
using Game.Core.Ports;
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

    private static readonly ConcurrentDictionary<string, TrackerState> SharedStates = new(StringComparer.Ordinal);

    private readonly IDisposable _subscription;
    private readonly IAchievementStateStore _stateStore;
    private readonly TrackerState _state;
    private readonly string _saveId;
    private bool _disposed;

    public int UnlockedCount
    {
        get
        {
            lock (_state.Gate)
            {
                return _state.UnlockedCount;
            }
        }
    }

    public event EventHandler<AchievementCountChanged>? UnlockedCountChanged;

    public AchievementTracker(IEventBus eventBus, IAchievementStateStore stateStore, string saveId)
    {
        if (eventBus == null)
            throw new ArgumentNullException(nameof(eventBus));
        if (stateStore == null)
            throw new ArgumentNullException(nameof(stateStore));

        _stateStore = stateStore;
        _saveId = NormalizeSaveId(saveId);

        _state = SharedStates.GetOrAdd(_saveId, static _ => new TrackerState());

        lock (_state.Gate)
        {
            _state.ReferenceCount++;
            if (!_state.IsHydrated)
            {
                var snapshot = _stateStore.LoadAsync(_saveId).GetAwaiter().GetResult();
                ApplySnapshotUnsafe(_state, snapshot);
                _state.IsHydrated = true;
            }
        }

        _subscription = eventBus.Subscribe(OnEventAsync);
    }

    private Task OnEventAsync(DomainEvent evt)
    {
        if (!TriggerEventTypes.Contains(evt.Type))
            return Task.CompletedTask;

        bool isNewTrigger;
        int newCount;
        AchievementStateSnapshot? snapshot = null;
        lock (_state.Gate)
        {
            isNewTrigger = _state.UnlockedTriggers.Add(evt.Type);
            if (isNewTrigger)
            {
                _state.UnlockedCount = _state.UnlockedTriggers.Count;
                snapshot = CreateSnapshotUnsafe(_state);
            }

            newCount = _state.UnlockedCount;
        }

        if (!isNewTrigger)
            return Task.CompletedTask;

        return PersistAndNotifyAsync(snapshot!, evt.Type, newCount);
    }

    private async Task PersistAndNotifyAsync(AchievementStateSnapshot snapshot, string triggerEventType, int newCount)
    {
        try
        {
            await _stateStore.SaveAsync(_saveId, snapshot);
        }
        catch
        {
        }

        UnlockedCountChanged?.Invoke(this, new AchievementCountChanged(newCount, triggerEventType));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _subscription.Dispose();

        var shouldRemove = false;
        lock (_state.Gate)
        {
            if (_state.ReferenceCount > 0)
                _state.ReferenceCount--;

            if (_state.ReferenceCount == 0)
            {
                _state.IsHydrated = false;
                shouldRemove = true;
            }
        }

        if (shouldRemove)
            SharedStates.TryRemove(_saveId, out _);

        _disposed = true;
    }

    private static string NormalizeSaveId(string saveId)
    {
        if (!SaveIdValue.TryCreate(saveId, out var normalized) || normalized == null)
            throw new ArgumentException("saveId must match [a-zA-Z0-9_-]{1,64}.", nameof(saveId));

        return normalized.Value;
    }

    private static void ApplySnapshotUnsafe(TrackerState state, AchievementStateSnapshot? snapshot)
    {
        state.UnlockedTriggers.Clear();

        var source = snapshot?.UnlockedTriggerEventTypes ?? Array.Empty<string>();
        foreach (var eventType in source)
        {
            if (!string.IsNullOrWhiteSpace(eventType))
                state.UnlockedTriggers.Add(eventType.Trim());
        }

        state.UnlockedCount = state.UnlockedTriggers.Count;
    }

    private static AchievementStateSnapshot CreateSnapshotUnsafe(TrackerState state)
    {
        var triggerTypes = state.UnlockedTriggers
            .OrderBy(static item => item, StringComparer.Ordinal)
            .ToArray();

        return new AchievementStateSnapshot(AchievementStateSnapshot.CurrentSchemaVersion, triggerTypes.Length, triggerTypes);
    }

    private sealed class TrackerState
    {
        public object Gate { get; } = new();
        public HashSet<string> UnlockedTriggers { get; } = new(StringComparer.Ordinal);
        public int UnlockedCount { get; set; }
        public bool IsHydrated { get; set; }
        public int ReferenceCount { get; set; }
    }
}
