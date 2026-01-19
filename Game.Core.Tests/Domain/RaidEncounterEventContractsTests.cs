using System;
using FluentAssertions;
using Game.Core.Contracts;
using Game.Core.Contracts.Raid;
using Xunit;

namespace Game.Core.Tests.Domain;

public sealed class RaidEncounterEventContractsTests
{
    // ACC:T17.2
    [Fact]
    public void Should_Expose_Stable_EventType_Constants_For_Raid_Events()
    {
        RaidScheduled.EventType.Should().Be("core.raid.scheduled");
        RaidResolved.EventType.Should().Be("core.raid.resolved");

        RaidScheduled.EventType.Should().StartWith("core.raid.");
        RaidResolved.EventType.Should().StartWith("core.raid.");
    }

    [Fact]
    public void Should_Live_In_Expected_Namespace_For_Raid_Contracts()
    {
        typeof(RaidScheduled).Namespace.Should().Be("Game.Core.Contracts.Raid");
        typeof(RaidResolved).Namespace.Should().Be("Game.Core.Contracts.Raid");
    }

    [Fact]
    public void Should_Construct_RaidScheduled_With_Expected_Fields()
    {
        var scheduledAt = DateTimeOffset.UnixEpoch;

        var evt = new RaidScheduled(
            RaidId: "raid-1",
            GuildId: "guild-1",
            Week: 42,
            EncounterId: "enc-1",
            ScheduledAt: scheduledAt
        );

        evt.RaidId.Should().Be("raid-1");
        evt.GuildId.Should().Be("guild-1");
        evt.Week.Should().Be(42);
        evt.EncounterId.Should().Be("enc-1");
        evt.ScheduledAt.Should().Be(scheduledAt);

        typeof(RaidScheduled).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void Should_Construct_RaidResolved_With_Expected_Fields()
    {
        var resolvedAt = DateTimeOffset.UnixEpoch;

        var evt = new RaidResolved(
            RaidId: "raid-1",
            GuildId: "guild-1",
            Week: 42,
            Result: RaidResolved.ResultSuccess,
            RewardPoints: 100,
            ResolvedAt: resolvedAt
        );

        evt.RaidId.Should().Be("raid-1");
        evt.GuildId.Should().Be("guild-1");
        evt.Week.Should().Be(42);
        evt.Result.Should().Be(RaidResolved.ResultSuccess);
        evt.RewardPoints.Should().Be(100);
        evt.ResolvedAt.Should().Be(resolvedAt);

        typeof(RaidResolved).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void Should_Default_DomainEvent_CloudEvents_Fields_When_Omitted()
    {
        var now = DateTime.UnixEpoch;

        var evt = new DomainEvent(
            Type: "core.raid.scheduled",
            Source: "test",
            Data: null,
            Timestamp: now,
            Id: "evt-1"
        );

        evt.Type.Should().Be("core.raid.scheduled");
        evt.Source.Should().Be("test");
        evt.Data.Should().BeNull();
        evt.Timestamp.Should().Be(now);
        evt.Id.Should().Be("evt-1");
        evt.SpecVersion.Should().Be("1.0");
        evt.DataContentType.Should().Be("application/json");
    }
}
