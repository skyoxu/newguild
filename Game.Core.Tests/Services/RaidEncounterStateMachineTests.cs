using System;
using System.Linq;
using FluentAssertions;
using Game.Core.Contracts;
using Game.Core.Contracts.Raid;
using Game.Core.Services;
using Game.Core.Tests.TestDoubles;
using Xunit;

namespace Game.Core.Tests.Services;

public sealed class RaidEncounterStateMachineTests
{
    // ACC:T17.1
    // ACC:T51.5
    // ACC:T51.6
    [Fact]
    public void ShouldExposePublicApi_WhenTypeLoaded()
    {
        typeof(RaidEncounterStateMachine).IsPublic.Should().BeTrue();
        typeof(RaidEncounterStateMachine).GetMethod(nameof(RaidEncounterStateMachine.Start)).Should().NotBeNull();
        typeof(RaidEncounterStateMachine).GetMethod(nameof(RaidEncounterStateMachine.Advance)).Should().NotBeNull();
        typeof(RaidEncounterStateMachine).GetMethod(nameof(RaidEncounterStateMachine.Fail)).Should().NotBeNull();
        typeof(RaidEncounterStateMachine).GetMethod(nameof(RaidEncounterStateMachine.DequeueEvents)).Should().NotBeNull();
    }

    // ACC:T17.4
    [Fact]
    public void ShouldContainEncounterLifecyclePhases_WhenEnumeratingPhaseNames()
    {
        var names = Enum.GetNames(typeof(RaidEncounterPhase));
        names.Should().Contain(nameof(RaidEncounterPhase.Entering));
        names.Should().Contain(nameof(RaidEncounterPhase.Combat));
        names.Should().Contain(nameof(RaidEncounterPhase.Resolution));
        names.Should().Contain(nameof(RaidEncounterPhase.Completed));
        names.Should().Contain(nameof(RaidEncounterPhase.Failed));
    }

    // ACC:T51.7
    [Fact]
    public void ShouldMatchExpectedRaidEventTypes_WhenReadingConstants()
    {
        RaidScheduled.EventType.Should().Be("core.raid.scheduled");
        RaidResolved.EventType.Should().Be("core.raid.resolved");
    }

    [Fact]
    public void ShouldUseStableDomainEventDefaults_WhenConstructed()
    {
        var evt = new DomainEvent(
            Type: "core.test.event",
            Source: "core.tests",
            Data: null,
            Timestamp: DateTime.UnixEpoch,
            Id: "test-id"
        );

        evt.SpecVersion.Should().Be("1.0");
        evt.DataContentType.Should().Be("application/json");
    }

    // ACC:T17.1
    // ACC:T51.1
    [Fact]
    public void ShouldReachCompletedAndEmitResolvedEvent_WhenAdvancingThroughAllPhases()
    {
        var sm = new RaidEncounterStateMachine(new FixedTime(DateTimeOffset.UnixEpoch), new SequenceIdGenerator("evt-1", "evt-2"));
        sm.Start(raidId: "raid-1", guildId: "guild-1", week: 1, encounterId: "enc-1");
        var scheduledEvents = sm.DequeueEvents();
        scheduledEvents.Should().ContainSingle().Subject.Id.Should().Be("evt-1");

        sm.Phase.Should().Be(RaidEncounterPhase.Entering);

        sm.Advance().Should().BeTrue();
        sm.Phase.Should().Be(RaidEncounterPhase.Combat);

        sm.Advance().Should().BeTrue();
        sm.Phase.Should().Be(RaidEncounterPhase.Resolution);

        sm.Advance().Should().BeTrue();
        sm.Phase.Should().Be(RaidEncounterPhase.Completed);

        sm.Advance().Should().BeFalse();

        var events = sm.DequeueEvents();
        var resolvedEvent = events.Should().ContainSingle(e => e.Type == RaidResolved.EventType).Subject;
        resolvedEvent.Id.Should().Be("evt-2");
        resolvedEvent.Data.Should().BeOfType<RaidResolved>().Which.RewardPoints.Should().Be(10);
    }

    // ACC:T17.4
    // ACC:T51.1
    [Fact]
    public void ShouldStopAdvancingAndEmitFailedResult_WhenFailCalled()
    {
        var sm = new RaidEncounterStateMachine(new FixedTime(DateTimeOffset.UnixEpoch), new SequenceIdGenerator("evt-1", "evt-2"));
        sm.Start(raidId: "raid-1", guildId: "guild-1", week: 1, encounterId: "enc-1");
        var scheduledEvents = sm.DequeueEvents();
        scheduledEvents.Should().ContainSingle().Subject.Id.Should().Be("evt-1");

        sm.Fail().Should().BeTrue();
        sm.Phase.Should().Be(RaidEncounterPhase.Failed);

        sm.Advance().Should().BeFalse();
        sm.Fail().Should().BeFalse();

        var events = sm.DequeueEvents();
        var resolved = events.Should().ContainSingle(e => e.Type == RaidResolved.EventType).Subject;
        resolved.Id.Should().Be("evt-2");
        resolved.Data.Should().BeOfType<RaidResolved>().Which.Result.Should().Be(RaidResolved.ResultFailed);
        resolved.Data.Should().BeOfType<RaidResolved>().Which.RewardPoints.Should().Be(0);
    }

    // ACC:T51.1
    [Fact]
    public void ShouldNotEmitSecondResolvedEvent_WhenFailCalledAfterCompleted()
    {
        var sm = new RaidEncounterStateMachine(new FixedTime(DateTimeOffset.UnixEpoch), new SequenceIdGenerator("evt-1", "evt-2"));
        sm.Start(raidId: "raid-1", guildId: "guild-1", week: 1, encounterId: "enc-1");
        sm.DequeueEvents().Should().ContainSingle(e => e.Type == RaidScheduled.EventType);

        sm.Advance().Should().BeTrue();
        sm.Advance().Should().BeTrue();
        sm.Advance().Should().BeTrue();
        sm.Phase.Should().Be(RaidEncounterPhase.Completed);

        var firstResolved = sm.DequeueEvents().Where(e => e.Type == RaidResolved.EventType).ToList();
        firstResolved.Should().HaveCount(1);

        sm.Fail().Should().BeFalse();
        sm.Advance().Should().BeFalse();

        sm.DequeueEvents().Should().NotContain(e => e.Type == RaidResolved.EventType);
    }

    [Fact]
    public void ShouldThrowInvalidOperationException_WhenAdvanceBeforeStart()
    {
        var sm = new RaidEncounterStateMachine(new FixedTime(DateTimeOffset.UnixEpoch), new SequenceIdGenerator("evt-1"));
        var act = () => sm.Advance();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ShouldThrowInvalidOperationException_WhenFailBeforeStart()
    {
        var sm = new RaidEncounterStateMachine(new FixedTime(DateTimeOffset.UnixEpoch), new SequenceIdGenerator("evt-1"));
        var act = () => sm.Fail();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ShouldThrowArgumentOutOfRangeException_WhenWeekLessThanOneOnStart()
    {
        var sm = new RaidEncounterStateMachine(new FixedTime(DateTimeOffset.UnixEpoch), new SequenceIdGenerator("evt-1"));
        var act = () => sm.Start(raidId: "raid-1", guildId: "guild-1", week: 0, encounterId: "enc-1");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ShouldThrowArgumentException_WhenRaidIdInvalidOnStart(string raidId)
    {
        var sm = new RaidEncounterStateMachine(new FixedTime(DateTimeOffset.UnixEpoch), new SequenceIdGenerator("evt-1"));
        var act = () => sm.Start(raidId: raidId, guildId: "guild-1", week: 1, encounterId: "enc-1");
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ShouldThrowArgumentException_WhenGuildIdInvalidOnStart(string guildId)
    {
        var sm = new RaidEncounterStateMachine(new FixedTime(DateTimeOffset.UnixEpoch), new SequenceIdGenerator("evt-1"));
        var act = () => sm.Start(raidId: "raid-1", guildId: guildId, week: 1, encounterId: "enc-1");
        act.Should().Throw<ArgumentException>();
    }

    // ACC:T51.10
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ShouldThrowArgumentException_WhenEncounterIdInvalidOnStart(string encounterId)
    {
        var sm = new RaidEncounterStateMachine(new FixedTime(DateTimeOffset.UnixEpoch), new SequenceIdGenerator("evt-1"));
        var act = () => sm.Start(raidId: "raid-1", guildId: "guild-1", week: 1, encounterId: encounterId);
        act.Should().Throw<ArgumentException>();
    }

}
