using System;
using FluentAssertions;
using Game.Core.Contracts.Activity;
using Game.Core.Contracts.Pvp;
using Game.Core.Contracts.WorldBoss;
using Xunit;

namespace Game.Core.Tests.Contracts;

public sealed class V11GameplayDepthEventContractsTests
{
    // ACC:T72.1
    [Fact]
    public void ShouldExposeStableEventTypes_WhenReadingV11GameplayDepthContracts()
    {
        ActivityFeedAppended.EventType.Should().Be("core.activity.feed.appended");
        WorldBossEntered.EventType.Should().Be("core.worldboss.entered");
        WorldBossResolved.EventType.Should().Be("core.worldboss.resolved");
        PvpMatchStarted.EventType.Should().Be("core.pvp.match.started");
        PvpMatchResolved.EventType.Should().Be("core.pvp.match.resolved");
    }

    [Fact]
    public void ShouldConstructActivityFeedAppended_WhenProvidingRequiredFields()
    {
        var appendedAt = DateTimeOffset.UnixEpoch;

        var evt = new ActivityFeedAppended(
            FeedEntryId: "feed-1",
            GuildId: "guild-1",
            SourceEventType: "core.raid.resolved",
            Message: "raid resolved",
            AppendedAt: appendedAt
        );

        evt.FeedEntryId.Should().Be("feed-1");
        evt.GuildId.Should().Be("guild-1");
        evt.SourceEventType.Should().Be("core.raid.resolved");
        evt.Message.Should().Be("raid resolved");
        evt.AppendedAt.Should().Be(appendedAt);
    }

    [Fact]
    public void ShouldConstructWorldBossContracts_WhenProvidingRequiredFields()
    {
        var enteredAt = DateTimeOffset.UnixEpoch;
        var resolvedAt = DateTimeOffset.UnixEpoch.AddMinutes(10);

        var entered = new WorldBossEntered(
            EncounterId: "enc-1",
            GuildId: "guild-1",
            Week: 12,
            EnteredAt: enteredAt
        );

        var resolved = new WorldBossResolved(
            EncounterId: "enc-1",
            GuildId: "guild-1",
            Week: 12,
            Result: WorldBossResolved.ResultVictory,
            RewardPoints: 200,
            ResolvedAt: resolvedAt
        );

        entered.EncounterId.Should().Be("enc-1");
        entered.GuildId.Should().Be("guild-1");
        entered.Week.Should().Be(12);
        entered.EnteredAt.Should().Be(enteredAt);

        resolved.Result.Should().Be(WorldBossResolved.ResultVictory);
        resolved.RewardPoints.Should().Be(200);
        WorldBossResolved.ResultDefeat.Should().Be("defeat");
    }

    [Fact]
    public void ShouldConstructPvpContracts_WhenProvidingRequiredFields()
    {
        var startedAt = DateTimeOffset.UnixEpoch;
        var resolvedAt = DateTimeOffset.UnixEpoch.AddMinutes(5);

        var started = new PvpMatchStarted(
            MatchId: "match-1",
            GuildId: "guild-1",
            OpponentGuildId: "guild-2",
            Week: 8,
            StartedAt: startedAt
        );

        var resolved = new PvpMatchResolved(
            MatchId: "match-1",
            GuildId: "guild-1",
            OpponentGuildId: "guild-2",
            Result: PvpMatchResolved.ResultWin,
            RatingDelta: 15,
            ResolvedAt: resolvedAt
        );

        started.MatchId.Should().Be("match-1");
        started.OpponentGuildId.Should().Be("guild-2");
        started.Week.Should().Be(8);

        resolved.Result.Should().Be(PvpMatchResolved.ResultWin);
        resolved.RatingDelta.Should().Be(15);
        PvpMatchResolved.ResultLoss.Should().Be("loss");
        PvpMatchResolved.ResultDraw.Should().Be("draw");
    }
}
