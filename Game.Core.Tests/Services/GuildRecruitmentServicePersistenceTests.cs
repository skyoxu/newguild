using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Contracts;
using Game.Core.Contracts.Guild;
using Game.Core.Contracts.Recruitment;
using Game.Core.Domain;
using Game.Core.Ports;
using Game.Core.Repositories;
using Game.Core.Services;
using Game.Core.Tests.Mocks;
using Xunit;

namespace Game.Core.Tests.Services;

public sealed class GuildRecruitmentServicePersistenceTests
{
    private sealed class CapturingEventBus : IEventBus
    {
        public readonly System.Collections.Generic.List<DomainEvent> Published = new();
        public Task PublishAsync(DomainEvent evt)
        {
            Published.Add(evt);
            return Task.CompletedTask;
        }

        public IDisposable Subscribe(Func<DomainEvent, Task> handler) => new Dummy();
        private sealed class Dummy : IDisposable { public void Dispose() { } }
    }

    private sealed class NoopTime : ITime { public double DeltaSeconds => 0.0; }
    private sealed class NoopLogger : ILogger
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
        public void Error(string message, Exception ex) { }
    }
    private sealed class DummyEventCatalog : IEventCatalog { }

    [Fact]
    public async Task RecruitmentOffers_Should_Survive_Service_Recreate_And_Be_Consumable()
    {
        var db = new MockSQLiteDatabase();
        var guildRepo = new SQLiteGuildRepository(db);
        var offerRepo = new SQLiteRecruitmentOfferRepository(db);

        var guild = new Guild("g1", "u-admin", "Guild");
        await guildRepo.CreateAsync(guild);

        var t0 = DateTimeOffset.Parse("2030-01-01T00:00:00Z");
        var t1 = DateTimeOffset.Parse("2030-01-01T00:01:00Z");

        var bus1 = new CapturingEventBus();
        var roster1 = new GuildRosterService(guildRepo, bus1);
        var svc1 = new GuildRecruitmentService(guildRepo, offerRepo, bus1, roster1, new NoopTime(), new NoopLogger(), new DummyEventCatalog());

        await svc1.ApplyAsync(guild, candidateId: "u2", role: "member", appliedAt: t0);
        var offerId = bus1.Published.Single(e => e.Type == RecruitmentOfferPresented.EventType)
            .Data.Should().BeOfType<RecruitmentOfferPresented>().Subject.OfferId;

        var bus2 = new CapturingEventBus();
        var roster2 = new GuildRosterService(guildRepo, bus2);
        var svc2 = new GuildRecruitmentService(guildRepo, offerRepo, bus2, roster2, new NoopTime(), new NoopLogger(), new DummyEventCatalog());

        await svc2.ApproveAsync(guild, offerId: offerId, approvedByUserId: "u-admin", resolvedAt: t1);

        bus2.Published.Should().Contain(e => e.Type == GuildMemberJoined.EventType);
        bus2.Published.Should().Contain(e => e.Type == RecruitmentOfferResolved.EventType);

        var remaining = await offerRepo.GetByIdAsync(offerId);
        remaining.Should().BeNull();
    }
}

