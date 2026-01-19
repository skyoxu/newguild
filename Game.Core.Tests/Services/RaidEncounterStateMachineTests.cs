using System;
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
    [Fact]
    public void RaidEncounterStateMachine_Should_Exist_And_Expose_Public_Api()
    {
        typeof(RaidEncounterStateMachine).IsPublic.Should().BeTrue();
        typeof(RaidEncounterStateMachine).GetMethod(nameof(RaidEncounterStateMachine.Start)).Should().NotBeNull();
        typeof(RaidEncounterStateMachine).GetMethod(nameof(RaidEncounterStateMachine.Advance)).Should().NotBeNull();
        typeof(RaidEncounterStateMachine).GetMethod(nameof(RaidEncounterStateMachine.Fail)).Should().NotBeNull();
        typeof(RaidEncounterStateMachine).GetMethod(nameof(RaidEncounterStateMachine.DequeueEvents)).Should().NotBeNull();
    }

    // ACC:T17.4
    [Fact]
    public void RaidEncounterPhase_Should_Contain_Entering_Combat_And_Resolution_Semantics()
    {
        var names = Enum.GetNames(typeof(RaidEncounterPhase));
        names.Should().Contain(nameof(RaidEncounterPhase.Entering));
        names.Should().Contain(nameof(RaidEncounterPhase.Combat));
        names.Should().Contain(nameof(RaidEncounterPhase.Resolution));
        names.Should().Contain(nameof(RaidEncounterPhase.Completed));
        names.Should().Contain(nameof(RaidEncounterPhase.Failed));
    }

    [Fact]
    public void Raid_EventTypes_Should_Match_Expected()
    {
        RaidScheduled.EventType.Should().Be("core.raid.scheduled");
        RaidResolved.EventType.Should().Be("core.raid.resolved");
    }

    [Fact]
    public void DomainEvent_Defaults_Should_Be_Stable()
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
    [Fact]
    public void RaidEncounterStateMachine_Should_Advance_Through_Phases_And_Reach_Completed()
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
    [Fact]
    public void RaidEncounterStateMachine_Should_Allow_Fail_And_Stop_Advancing()
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

    [Fact]
    public void Advance_Before_Start_Should_Throw()
    {
        var sm = new RaidEncounterStateMachine(new FixedTime(DateTimeOffset.UnixEpoch), new SequenceIdGenerator("evt-1"));
        var act = () => sm.Advance();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Fail_Before_Start_Should_Throw()
    {
        var sm = new RaidEncounterStateMachine(new FixedTime(DateTimeOffset.UnixEpoch), new SequenceIdGenerator("evt-1"));
        var act = () => sm.Fail();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Start_With_Week_LessThanOne_Should_Throw()
    {
        var sm = new RaidEncounterStateMachine(new FixedTime(DateTimeOffset.UnixEpoch), new SequenceIdGenerator("evt-1"));
        var act = () => sm.Start(raidId: "raid-1", guildId: "guild-1", week: 0, encounterId: "enc-1");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Start_With_Invalid_RaidId_Should_Throw(string raidId)
    {
        var sm = new RaidEncounterStateMachine(new FixedTime(DateTimeOffset.UnixEpoch), new SequenceIdGenerator("evt-1"));
        var act = () => sm.Start(raidId: raidId, guildId: "guild-1", week: 1, encounterId: "enc-1");
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Start_With_Invalid_GuildId_Should_Throw(string guildId)
    {
        var sm = new RaidEncounterStateMachine(new FixedTime(DateTimeOffset.UnixEpoch), new SequenceIdGenerator("evt-1"));
        var act = () => sm.Start(raidId: "raid-1", guildId: guildId, week: 1, encounterId: "enc-1");
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Start_With_Invalid_EncounterId_Should_Throw(string encounterId)
    {
        var sm = new RaidEncounterStateMachine(new FixedTime(DateTimeOffset.UnixEpoch), new SequenceIdGenerator("evt-1"));
        var act = () => sm.Start(raidId: "raid-1", guildId: "guild-1", week: 1, encounterId: encounterId);
        act.Should().Throw<ArgumentException>();
    }

}
