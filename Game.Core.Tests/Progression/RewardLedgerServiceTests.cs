using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Contracts;
using Game.Core.Contracts.Engine;
using Game.Core.Contracts.Media;
using Game.Core.Contracts.Raid;
using Game.Core.Contracts.Recruitment;
using Game.Core.Services;
using Game.Core.Tests.TestDoubles;
using Xunit;

namespace Game.Core.Tests.Progression;

public sealed class RewardLedgerServiceTests
{
    // ACC:T35.7
    [Fact]
    public async Task Should_Apply_RewardGrants_From_DomainEvents()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var time = new FixedTime(now);
        var ids = new SequenceIdGenerator("evt-1", "evt-2", "evt-3", "evt-4", "evt-5", "evt-6");
        var bus = new InMemoryEventBus();
        var service = new RewardLedgerService(bus, time, ids);

        using var _ = service.Start();

        var observed = new List<DomainEvent>();
        using var __ = bus.Subscribe(evt =>
        {
            observed.Add(evt);
            return Task.CompletedTask;
        });

        var raid = new RaidResolved(
            RaidId: "raid-1",
            GuildId: "guild-1",
            Week: 1,
            Result: RaidResolved.ResultSuccess,
            RewardPoints: 10,
            ResolvedAt: now);
        await bus.PublishAsync(new DomainEvent(
            Type: RaidResolved.EventType,
            Source: "test",
            Data: raid,
            Timestamp: now.UtcDateTime,
            Id: "raid-evt"));

        var beat = new MediaBeatTriggered(
            BeatId: "beat-1",
            GuildId: "guild-1",
            SourceEventType: RaidResolved.EventType,
            Headline: "headline",
            TriggeredAt: now);
        await bus.PublishAsync(new DomainEvent(
            Type: MediaBeatTriggered.EventType,
            Source: "test",
            Data: beat,
            Timestamp: now.UtcDateTime,
            Id: "beat-evt"));

        var offer = new RecruitmentOfferResolved(
            OfferId: "offer-1",
            GuildId: "guild-1",
            CandidateId: "candidate-1",
            Decision: "accepted",
            Reason: "approved",
            ResolvedAt: now);
        await bus.PublishAsync(new DomainEvent(
            Type: RecruitmentOfferResolved.EventType,
            Source: "test",
            Data: offer,
            Timestamp: now.UtcDateTime,
            Id: "offer-evt"));

        var scoreEvents = observed.Where(evt => evt.Type == ScoreChanged.EventType).ToList();
        scoreEvents.Should().HaveCount(2);
        var lastScore = (ScoreChanged)scoreEvents.Last().Data!;
        lastScore.Score.Should().Be(15);
        lastScore.Added.Should().Be(5);

        var repEvents = observed.Where(evt => evt.Type == ReputationChanged.EventType).ToList();
        repEvents.Should().HaveCount(3);
        var lastRep = (ReputationChanged)repEvents.Last().Data!;
        lastRep.GuildId.Should().Be("guild-1");
        lastRep.NewValue.Should().Be(4);

        service.Replay().Should().HaveCount(3);
    }

    [Fact]
    public async Task Should_Ignore_Raid_When_No_RewardPoints()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var time = new FixedTime(now);
        var ids = new SequenceIdGenerator("evt-1");
        var bus = new InMemoryEventBus();
        var service = new RewardLedgerService(bus, time, ids);

        using var _ = service.Start();

        var observed = new List<DomainEvent>();
        using var __ = bus.Subscribe(evt =>
        {
            observed.Add(evt);
            return Task.CompletedTask;
        });

        var raid = new RaidResolved(
            RaidId: "raid-1",
            GuildId: "guild-1",
            Week: 1,
            Result: RaidResolved.ResultSuccess,
            RewardPoints: 0,
            ResolvedAt: now);
        await bus.PublishAsync(new DomainEvent(
            Type: RaidResolved.EventType,
            Source: "test",
            Data: raid,
            Timestamp: now.UtcDateTime,
            Id: "raid-evt"));

        observed.Where(evt => evt.Type == ScoreChanged.EventType).Should().BeEmpty();
        observed.Where(evt => evt.Type == ReputationChanged.EventType).Should().BeEmpty();
        service.Replay().Should().BeEmpty();
    }

    [Fact]
    public async Task Should_Ignore_Recruitment_When_Not_Accepted()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var time = new FixedTime(now);
        var ids = new SequenceIdGenerator("evt-1");
        var bus = new InMemoryEventBus();
        var service = new RewardLedgerService(bus, time, ids);

        using var _ = service.Start();

        var observed = new List<DomainEvent>();
        using var __ = bus.Subscribe(evt =>
        {
            observed.Add(evt);
            return Task.CompletedTask;
        });

        var offer = new RecruitmentOfferResolved(
            OfferId: "offer-1",
            GuildId: "guild-1",
            CandidateId: "candidate-1",
            Decision: "rejected",
            Reason: "declined",
            ResolvedAt: now);
        await bus.PublishAsync(new DomainEvent(
            Type: RecruitmentOfferResolved.EventType,
            Source: "test",
            Data: offer,
            Timestamp: now.UtcDateTime,
            Id: "offer-evt"));

        observed.Where(evt => evt.Type == ScoreChanged.EventType).Should().BeEmpty();
        observed.Where(evt => evt.Type == ReputationChanged.EventType).Should().BeEmpty();
        service.Replay().Should().BeEmpty();
    }

    [Fact]
    public async Task Should_Clamp_Reputation_On_Replay_When_Negative()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var time = new FixedTime(now);
        var ids = new SequenceIdGenerator("evt-1");
        var bus = new InMemoryEventBus();
        var service = new RewardLedgerService(bus, time, ids);

        using var _ = service.Start();

        var observed = new List<DomainEvent>();
        using var __ = bus.Subscribe(evt =>
        {
            observed.Add(evt);
            return Task.CompletedTask;
        });

        var json = "[{\"GrantId\":\"grant-1\",\"GuildId\":\"guild-1\",\"SourceType\":\"manual\",\"SourceId\":\"s1\",\"Rewards\":{\"reputation\":-5}}]";
        await service.LoadAsync(json);

        var repEvents = observed.Where(evt => evt.Type == ReputationChanged.EventType).ToList();
        repEvents.Should().HaveCount(1);
        var rep = (ReputationChanged)repEvents[0].Data!;
        rep.NewValue.Should().Be(0);
        rep.OldValue.Should().Be(0);
    }
}
