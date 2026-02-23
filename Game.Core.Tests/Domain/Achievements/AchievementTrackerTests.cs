using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Contracts;
using Game.Core.Contracts.Achievements;
using Game.Core.Contracts.Guild;
using Game.Core.Contracts.Media;
using Game.Core.Contracts.Raid;
using Game.Core.Contracts.Recruitment;
using Game.Core.Domain.Achievements;
using Game.Core.Services;
using Xunit;

namespace Game.Core.Tests.Domain.Achievements;

public class AchievementTrackerTests
{
    private const string SaveId = "t50-achievements";

    // ACC:T36.3 ACC:T50.1
    [Fact]
    public void ShouldBeZero_WhenTrackerCreated()
    {
        var bus = new InMemoryEventBus();
        var store = new TestAchievementStateStore();
        using var tracker = new AchievementTracker(bus, store, SaveId);

        tracker.UnlockedCount.Should().Be(0);
    }

    // ACC:T36.3 ACC:T50.1 ACC:T50.5 ACC:T50.6 ACC:T50.10
    [Fact]
    public async Task ShouldIncrementOnce_WhenSameTriggerTypePublishedRepeatedly()
    {
        var bus = new InMemoryEventBus();
        var store = new TestAchievementStateStore();
        using var tracker = new AchievementTracker(bus, store, SaveId);

        var updates = new List<AchievementCountChanged>();
        tracker.UnlockedCountChanged += (_, args) => updates.Add(args);

        await bus.PublishAsync(BuildEvent(GuildCreated.EventType));
        await bus.PublishAsync(BuildEvent(GuildCreated.EventType));
        await bus.PublishAsync(BuildEvent(MediaBeatTriggered.EventType));

        tracker.UnlockedCount.Should().Be(2);
        updates.Should().HaveCount(2);
        updates[0].UnlockedCount.Should().Be(1);
        updates[0].TriggerEventType.Should().Be(GuildCreated.EventType);
        updates[1].UnlockedCount.Should().Be(2);
        updates[1].TriggerEventType.Should().Be(MediaBeatTriggered.EventType);
    }

    // ACC:T36.3 ACC:T50.3
    [Fact]
    public async Task ShouldRemainZero_WhenNonTriggerEventPublished()
    {
        var bus = new InMemoryEventBus();
        var store = new TestAchievementStateStore();
        using var tracker = new AchievementTracker(bus, store, SaveId);

        await bus.PublishAsync(BuildEvent("core.test.non.trigger.event"));

        tracker.UnlockedCount.Should().Be(0);
    }


    // ACC:T50.4 RED-FIRST
    [Fact]
    public async Task ShouldRestoreUnlockedCount_WhenTrackerRecreatedWithNewEventBus()
    {
        var store = new TestAchievementStateStore();
        var bus = new InMemoryEventBus();

        using (var tracker = new AchievementTracker(bus, store, SaveId))
        {
            await bus.PublishAsync(BuildEvent(GuildCreated.EventType));
            tracker.UnlockedCount.Should().Be(1);
        }

        var reloadedBus = new InMemoryEventBus();
        using var reloaded = new AchievementTracker(reloadedBus, store, SaveId);
        reloaded.UnlockedCount.Should().Be(1);
    }

    // ACC:T50.4
    [Fact]
    public async Task ShouldSyncUnlockedCount_WhenMultipleTrackersShareSameEventBus()
    {
        var bus = new InMemoryEventBus();
        var store = new TestAchievementStateStore();
        using var trackerA = new AchievementTracker(bus, store, SaveId);
        using var trackerB = new AchievementTracker(bus, store, SaveId);

        await bus.PublishAsync(BuildEvent(GuildCreated.EventType));

        trackerA.UnlockedCount.Should().Be(1);
        trackerB.UnlockedCount.Should().Be(1);
    }

    // ACC:T50.4
    [Fact]
    public async Task ShouldPersistTriggerTypes_WhenTrackerRecreatedAcrossSessions()
    {
        var store = new TestAchievementStateStore();

        var firstBus = new InMemoryEventBus();
        using (var firstSession = new AchievementTracker(firstBus, store, SaveId))
        {
            await firstBus.PublishAsync(BuildEvent(GuildCreated.EventType));
            await firstBus.PublishAsync(BuildEvent(MediaBeatTriggered.EventType));
            firstSession.UnlockedCount.Should().Be(2);
        }

        var secondBus = new InMemoryEventBus();
        using var secondSession = new AchievementTracker(secondBus, store, SaveId);
        secondSession.UnlockedCount.Should().Be(2);
    }

    private static DomainEvent BuildEvent(string eventType) =>
        new(eventType, "test", "{}", DateTime.UtcNow, Guid.NewGuid().ToString("N"));

    private sealed class TestAchievementStateStore : IAchievementStateStore
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, AchievementStateSnapshot> _states = new(StringComparer.Ordinal);

        public Task<AchievementStateSnapshot?> LoadAsync(string saveId)
        {
            lock (_gate)
            {
                if (_states.TryGetValue(saveId, out var snapshot))
                    return Task.FromResult<AchievementStateSnapshot?>(snapshot);
            }

            return Task.FromResult<AchievementStateSnapshot?>(null);
        }

        public Task SaveAsync(string saveId, AchievementStateSnapshot snapshot)
        {
            lock (_gate)
                _states[saveId] = snapshot;

            return Task.CompletedTask;
        }
    }
}
