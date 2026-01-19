using System;
using System.Linq;
using FluentAssertions;
using Game.Core.Contracts;
using Game.Core.Contracts.Raid;
using Game.Core.Tests.TestDoubles;
using Xunit;

namespace Game.Core.Tests.Services;

public sealed class RaidEncounterDomainEventsTests
{
    // ACC:T17.2
    [Fact]
    public void Start_Should_Enqueue_RaidScheduled_DomainEvent_With_Expected_Type_And_Data()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var time = new FixedTime(now);
        var idGen = new SequenceIdGenerator("evt-raid-scheduled-1");

        var sm = new Game.Core.Services.RaidEncounterStateMachine(time, idGen);
        sm.Start(raidId: "raid-1", guildId: "guild-1", week: 3, encounterId: "enc-1");

        var events = sm.DequeueEvents();
        events.Should().ContainSingle();

        var evt = events.Single();
        evt.Type.Should().Be(RaidScheduled.EventType);
        evt.Id.Should().Be("evt-raid-scheduled-1");
        evt.Source.Should().Be("game.core/raid-encounter");

        var scheduled = evt.Data.Should().BeOfType<RaidScheduled>().Subject;
        scheduled.RaidId.Should().Be("raid-1");
        scheduled.GuildId.Should().Be("guild-1");
        scheduled.Week.Should().Be(3);
        scheduled.EncounterId.Should().Be("enc-1");
        scheduled.ScheduledAt.Should().Be(now);
    }

    // ACC:T17.2
    [Theory]
    [InlineData(true, RaidResolved.ResultSuccess)]
    [InlineData(false, RaidResolved.ResultFailed)]
    public void Completion_Or_Fail_Should_Enqueue_RaidResolved_DomainEvent_With_Result(bool successPath, string expectedResult)
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 5, 0, TimeSpan.Zero);
        var time = new FixedTime(now);
        var idGen = new SequenceIdGenerator("evt-raid-scheduled-1", "evt-raid-resolved-1");

        var sm = new Game.Core.Services.RaidEncounterStateMachine(time, idGen);
        sm.Start(raidId: "raid-1", guildId: "guild-1", week: 3, encounterId: "enc-1");
        var scheduledEvents = sm.DequeueEvents();
        scheduledEvents.Should().ContainSingle();
        scheduledEvents.Single().Id.Should().Be("evt-raid-scheduled-1");

        if (successPath)
        {
            sm.Advance().Should().BeTrue(); // entering -> combat
            sm.Advance().Should().BeTrue(); // combat -> resolution
            sm.Advance().Should().BeTrue(); // resolution -> completed (resolved event)
        }
        else
        {
            sm.Fail().Should().BeTrue();
        }

        var events = sm.DequeueEvents();
        events.Should().ContainSingle(e => e.Type == RaidResolved.EventType);
        var evt = events.Single(e => e.Type == RaidResolved.EventType);

        evt.Id.Should().Be("evt-raid-resolved-1");
        evt.Source.Should().Be("game.core/raid-encounter");
        var resolved = evt.Data.Should().BeOfType<RaidResolved>().Subject;
        resolved.Result.Should().Be(expectedResult);
        resolved.RewardPoints.Should().Be(successPath ? 10 : 0);
        resolved.ResolvedAt.Should().Be(now);
    }

}
