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
    // ACC:T36.3
    [Fact]
    public void ShouldStartAtZero()
    {
        var bus = new InMemoryEventBus();
        using var tracker = new AchievementTracker(bus);

        tracker.UnlockedCount.Should().Be(0);
    }

    // ACC:T36.3
    [Fact]
    public async Task ShouldIncrementOncePerTriggerEventType()
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

    // ACC:T36.3
    [Fact]
    public async Task ShouldIgnoreNonTriggerEvent()
    {
        var bus = new InMemoryEventBus();
        using var tracker = new AchievementTracker(bus);

        await bus.PublishAsync(BuildEvent("core.test.non.trigger.event"));

        tracker.UnlockedCount.Should().Be(0);
    }

    private static DomainEvent BuildEvent(string eventType) =>
        new(eventType, "test", "{}", DateTime.UtcNow, Guid.NewGuid().ToString("N"));
}
