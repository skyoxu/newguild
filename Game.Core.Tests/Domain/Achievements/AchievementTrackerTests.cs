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
    // ACC:T36.3 ACC:T50.1
    [Fact]
    public void ShouldBeZero_WhenTrackerCreated()
    {
        var bus = new InMemoryEventBus();
        using var tracker = new AchievementTracker(bus);

        tracker.UnlockedCount.Should().Be(0);
    }

    // ACC:T36.3 ACC:T50.1 ACC:T50.5 ACC:T50.6 ACC:T50.10
    [Fact]
    public async Task ShouldIncrementOnce_WhenSameTriggerTypePublishedRepeatedly()
    {
        var bus = new InMemoryEventBus();
        using var tracker = new AchievementTracker(bus);

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
        using var tracker = new AchievementTracker(bus);

        await bus.PublishAsync(BuildEvent("core.test.non.trigger.event"));

        tracker.UnlockedCount.Should().Be(0);
    }


    // ACC:T50.4 RED-FIRST
    [Fact]
    public async Task ShouldRestoreUnlockedCount_WhenTrackerRecreatedOnSameEventBus()
    {
        var bus = new InMemoryEventBus();

        using (var tracker = new AchievementTracker(bus))
        {
            await bus.PublishAsync(BuildEvent(GuildCreated.EventType));
            tracker.UnlockedCount.Should().Be(1);
        }

        using var reloaded = new AchievementTracker(bus);
        reloaded.UnlockedCount.Should().Be(1);
    }

    private static DomainEvent BuildEvent(string eventType) =>
        new(eventType, "test", "{}", DateTime.UtcNow, Guid.NewGuid().ToString("N"));
}
