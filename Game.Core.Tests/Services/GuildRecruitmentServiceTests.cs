using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Contracts;
using Game.Core.Contracts.Guild;
using Game.Core.Contracts.Recruitment;
using Game.Core.Domain;
using Game.Core.Ports;
using Game.Core.Repositories;
using Game.Core.Services;
using Xunit;

namespace Game.Core.Tests.Services;

public sealed partial class GuildRecruitmentServiceTests
{
    private sealed class CapturingEventBus : IEventBus
    {
        public List<DomainEvent> Published { get; } = new();

        public Task PublishAsync(DomainEvent evt)
        {
            Published.Add(evt);
            return Task.CompletedTask;
        }

        public IDisposable Subscribe(Func<DomainEvent, Task> handler) => new DummySubscription();

        private sealed class DummySubscription : IDisposable
        {
            public void Dispose() { }
        }
    }

    private sealed class InMemoryGuildRepository : IGuildRepository
    {
        private readonly Dictionary<string, Guild> _guilds = new(StringComparer.Ordinal);

        public void Seed(Guild guild)
        {
            _guilds[guild.GuildId] = guild;
        }

        public Task<Guild> CreateAsync(Guild guild)
        {
            _guilds[guild.GuildId] = guild;
            return Task.FromResult(guild);
        }

        public Task<Guild?> GetByIdAsync(string guildId)
        {
            _guilds.TryGetValue(guildId, out var guild);
            return Task.FromResult(guild);
        }

        public Task<Guild> UpdateAsync(Guild guild)
        {
            _guilds[guild.GuildId] = guild;
            return Task.FromResult(guild);
        }

        public Task<bool> DeleteAsync(string guildId)
        {
            var removed = _guilds.Remove(guildId);
            return Task.FromResult(removed);
        }

        public Task<IReadOnlyList<Guild>> GetAllAsync()
            => Task.FromResult((IReadOnlyList<Guild>)_guilds.Values.ToList());

        public Task<IReadOnlyList<Guild>> FindByMemberAsync(string userId)
        {
            var result = _guilds.Values
                .Where(g => g.Members.Any(m => m.UserId == userId))
                .ToList();
            return Task.FromResult((IReadOnlyList<Guild>)result);
        }
    }

    private sealed class FixedTime : ITime
    {
        public FixedTime(double deltaSeconds = 0.0) => DeltaSeconds = deltaSeconds;
        public double DeltaSeconds { get; }
    }

    private sealed class NoopLogger : ILogger
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
        public void Error(string message, Exception ex) { }
    }

    private sealed class DummyEventCatalog : IEventCatalog
    {
    }

    private sealed class InMemoryRecruitmentOfferRepository : IRecruitmentOfferRepository
    {
        private readonly Dictionary<string, RecruitmentOffer> _offersById = new(StringComparer.Ordinal);

        public Task AddAsync(RecruitmentOffer offer)
        {
            _offersById[offer.OfferId] = offer;
            return Task.CompletedTask;
        }

        public Task<bool> RemoveAsync(string offerId)
        {
            if (string.IsNullOrWhiteSpace(offerId))
                return Task.FromResult(false);
            return Task.FromResult(_offersById.Remove(offerId.Trim()));
        }

        public Task<RecruitmentOffer?> GetByIdAsync(string offerId)
        {
            if (string.IsNullOrWhiteSpace(offerId))
                return Task.FromResult<RecruitmentOffer?>(null);
            _offersById.TryGetValue(offerId.Trim(), out var offer);
            return Task.FromResult<RecruitmentOffer?>(offer);
        }

        public Task<IReadOnlyList<RecruitmentOffer>> GetByGuildAsync(string guildId)
        {
            if (string.IsNullOrWhiteSpace(guildId))
                return Task.FromResult<IReadOnlyList<RecruitmentOffer>>(Array.Empty<RecruitmentOffer>());

            var key = guildId.Trim();
            var list = _offersById.Values.Where(o => string.Equals(o.GuildId, key, StringComparison.Ordinal)).ToList();
            return Task.FromResult<IReadOnlyList<RecruitmentOffer>>(list);
        }
    }

    private static (Guild Guild, CapturingEventBus Bus, InMemoryGuildRepository Repo, InMemoryRecruitmentOfferRepository Offers, GuildRosterService Roster) Arrange()
    {
        var bus = new CapturingEventBus();
        var repo = new InMemoryGuildRepository();
        var offers = new InMemoryRecruitmentOfferRepository();
        var guild = new Guild(guildId: "g-1", creatorId: "u-admin", name: "Test Guild");
        repo.Seed(guild);
        var roster = new GuildRosterService(repo, bus);
        return (guild, bus, repo, offers, roster);
    }

    // ACC:T14.1
    [Fact]
    public async Task ApplyAsync_Should_Publish_OfferPresented_When_Candidate_Applies()
    {
        var (guild, bus, repo, offers, roster) = Arrange();
        var appliedAt = DateTimeOffset.Parse("2030-01-01T00:00:00Z");

        var sut = RecruitmentServiceDriver.Create(
            guildRepository: repo,
            offerRepository: offers,
            eventBus: bus,
            rosterService: roster,
            time: new FixedTime(),
            logger: new NoopLogger(),
            eventCatalog: new DummyEventCatalog());

        await RecruitmentServiceDriver.ApplyAsync(
            sut,
            guild: guild,
            candidateId: "u-candidate",
            role: "member",
            appliedAt: appliedAt);

        var presentedEvt = bus.Published
            .Should().ContainSingle(e => e.Type == RecruitmentOfferPresented.EventType)
            .Which;

        var presented = presentedEvt.Data.Should().BeOfType<RecruitmentOfferPresented>().Subject;
        presented.GuildId.Should().Be(guild.GuildId);
        presented.CandidateId.Should().Be("u-candidate");
        presented.Role.Should().Be("member");
        presented.PresentedAt.Should().Be(appliedAt);
        presented.OfferId.Should().NotBeNullOrWhiteSpace();

        bus.Published.Should().NotContain(e => e.Type == RecruitmentOfferResolved.EventType);
        bus.Published.Should().NotContain(e => e.Type == GuildMemberJoined.EventType);
    }

    // ACC:T14.4
    [Fact]
    public async Task ApproveAsync_Should_Publish_OfferResolved_And_GuildMemberJoined_When_Admin_Approves()
    {
        var (guild, bus, repo, offers, roster) = Arrange();
        var t0 = DateTimeOffset.Parse("2030-01-01T00:00:00Z");
        var t1 = DateTimeOffset.Parse("2030-01-01T00:01:00Z");

        var sut = RecruitmentServiceDriver.Create(
            guildRepository: repo,
            offerRepository: offers,
            eventBus: bus,
            rosterService: roster,
            time: new FixedTime(),
            logger: new NoopLogger(),
            eventCatalog: new DummyEventCatalog());

        await RecruitmentServiceDriver.ApplyAsync(sut, guild, candidateId: "u-candidate", role: "member", appliedAt: t0);

        var offerId = bus.Published
            .Single(e => e.Type == RecruitmentOfferPresented.EventType)
            .Data.Should().BeOfType<RecruitmentOfferPresented>().Subject
            .OfferId;

        bus.Published.Clear();

        await RecruitmentServiceDriver.ApproveAsync(
            sut,
            guild: guild,
            offerId: offerId,
            approvedByUserId: "u-admin",
            resolvedAt: t1);

        bus.Published.Should().Contain(e => e.Type == GuildMemberJoined.EventType);
        bus.Published.Should().Contain(e => e.Type == RecruitmentOfferResolved.EventType);

        var resolvedEvt = bus.Published.Single(e => e.Type == RecruitmentOfferResolved.EventType);
        var resolved = resolvedEvt.Data.Should().BeOfType<RecruitmentOfferResolved>().Subject;
        resolved.OfferId.Should().Be(offerId);
        resolved.GuildId.Should().Be(guild.GuildId);
        resolved.CandidateId.Should().Be("u-candidate");
        resolved.Decision.Should().Be("accepted");
        resolved.Reason.Should().NotBeNullOrWhiteSpace();
        resolved.ResolvedAt.Should().Be(t1);

        var joinedEvt = bus.Published.Single(e => e.Type == GuildMemberJoined.EventType);
        var joined = joinedEvt.Data.Should().BeOfType<GuildMemberJoined>().Subject;
        joined.UserId.Should().Be("u-candidate");
        joined.GuildId.Should().Be(guild.GuildId);
        joined.Role.Should().Be("member");
    }

    // ACC:T14.1
    [Fact]
    public async Task RejectAsync_Should_Resolve_Offer_And_Not_Join_Member()
    {
        var (guild, bus, repo, offers, roster) = Arrange();
        var t0 = DateTimeOffset.Parse("2030-01-01T00:00:00Z");
        var t1 = DateTimeOffset.Parse("2030-01-01T00:01:00Z");

        var sut = RecruitmentServiceDriver.Create(
            guildRepository: repo,
            offerRepository: offers,
            eventBus: bus,
            rosterService: roster,
            time: new FixedTime(),
            logger: new NoopLogger(),
            eventCatalog: new DummyEventCatalog());

        await RecruitmentServiceDriver.ApplyAsync(sut, guild, candidateId: "u-candidate", role: "member", appliedAt: t0);

        var offerId = bus.Published
            .Single(e => e.Type == RecruitmentOfferPresented.EventType)
            .Data.Should().BeOfType<RecruitmentOfferPresented>().Subject
            .OfferId;

        bus.Published.Clear();

        await RecruitmentServiceDriver.RejectAsync(
            sut,
            guild: guild,
            offerId: offerId,
            rejectedByUserId: "u-admin",
            reason: "rejected",
            resolvedAt: t1);

        bus.Published.Should().NotContain(e => e.Type == GuildMemberJoined.EventType);

        var resolvedEvt = bus.Published
            .Should().ContainSingle(e => e.Type == RecruitmentOfferResolved.EventType)
            .Which;

        var resolved = resolvedEvt.Data.Should().BeOfType<RecruitmentOfferResolved>().Subject;
        resolved.OfferId.Should().Be(offerId);
        resolved.GuildId.Should().Be(guild.GuildId);
        resolved.CandidateId.Should().Be("u-candidate");
        resolved.Decision.Should().Be("rejected");
        resolved.Reason.Should().NotBeNullOrWhiteSpace();
        resolved.ResolvedAt.Should().Be(t1);
    }

    [Fact]
    public async Task WithdrawAsync_Should_Resolve_Offer_And_Not_Join_Member()
    {
        var (guild, bus, repo, offers, roster) = Arrange();
        var t0 = DateTimeOffset.Parse("2030-01-01T00:00:00Z");
        var t1 = DateTimeOffset.Parse("2030-01-01T00:01:00Z");

        var sut = RecruitmentServiceDriver.Create(
            guildRepository: repo,
            offerRepository: offers,
            eventBus: bus,
            rosterService: roster,
            time: new FixedTime(),
            logger: new NoopLogger(),
            eventCatalog: new DummyEventCatalog());

        await RecruitmentServiceDriver.ApplyAsync(sut, guild, candidateId: "u-candidate", role: "member", appliedAt: t0);

        var offerId = bus.Published
            .Single(e => e.Type == RecruitmentOfferPresented.EventType)
            .Data.Should().BeOfType<RecruitmentOfferPresented>().Subject
            .OfferId;

        bus.Published.Clear();

        await RecruitmentServiceDriver.WithdrawAsync(
            sut,
            guild: guild,
            offerId: offerId,
            candidateId: "u-candidate",
            resolvedAt: t1);

        bus.Published.Should().NotContain(e => e.Type == GuildMemberJoined.EventType);

        var resolvedEvt = bus.Published
            .Should().ContainSingle(e => e.Type == RecruitmentOfferResolved.EventType)
            .Which;

        var resolved = resolvedEvt.Data.Should().BeOfType<RecruitmentOfferResolved>().Subject;
        resolved.OfferId.Should().Be(offerId);
        resolved.GuildId.Should().Be(guild.GuildId);
        resolved.CandidateId.Should().Be("u-candidate");
        resolved.Decision.Should().BeOneOf("withdrawn", "withdraw");
        resolved.Reason.Should().NotBeNullOrWhiteSpace();
        resolved.ResolvedAt.Should().Be(t1);
    }

    private static class RecruitmentServiceDriver
    {
        private static readonly string[] CandidateTypeNames =
        {
            "Game.Core.Services.GuildRecruitmentService",
            "Game.Core.Services.RecruitmentService",
            "Game.Core.Services.RecruitmentSystem",
        };

        public static object Create(
            IGuildRepository guildRepository,
            IRecruitmentOfferRepository offerRepository,
            IEventBus eventBus,
            GuildRosterService rosterService,
            ITime time,
            ILogger logger,
            IEventCatalog eventCatalog)
        {
            var type = ResolveServiceType();

            var available = new List<object>
            {
                guildRepository,
                offerRepository,
                eventBus,
                rosterService,
                time,
                logger,
                eventCatalog,
            };

            foreach (var ctor in type.GetConstructors(BindingFlags.Instance | BindingFlags.Public))
            {
                if (TryBuildArguments(ctor, available, out var args))
                    return ctor.Invoke(args);
            }

            throw new InvalidOperationException(
                "Recruitment service found but no public constructor could be satisfied. " +
                "Expected a constructor that can be built from: IGuildRepository, IRecruitmentOfferRepository, IEventBus, GuildRosterService, ITime, ILogger, IEventCatalog.");
        }

        public static Task ApplyAsync(object service, Guild guild, string candidateId, string role, DateTimeOffset appliedAt)
            => InvokeTaskAsync(service, "ApplyAsync", new object?[] { guild, candidateId, role, appliedAt },
                expectedParameterTypes: new[] { typeof(Guild), typeof(string), typeof(string), typeof(DateTimeOffset) });

        public static Task ApproveAsync(object service, Guild guild, string offerId, string approvedByUserId, DateTimeOffset resolvedAt)
            => InvokeTaskAsync(service, "ApproveAsync", new object?[] { guild, offerId, approvedByUserId, resolvedAt },
                expectedParameterTypes: new[] { typeof(Guild), typeof(string), typeof(string), typeof(DateTimeOffset) });

        public static Task WithdrawAsync(object service, Guild guild, string offerId, string candidateId, DateTimeOffset resolvedAt)
            => InvokeTaskAsync(service, "WithdrawAsync", new object?[] { guild, offerId, candidateId, resolvedAt },
                expectedParameterTypes: new[] { typeof(Guild), typeof(string), typeof(string), typeof(DateTimeOffset) });

        public static Task RejectAsync(object service, Guild guild, string offerId, string rejectedByUserId, string reason, DateTimeOffset resolvedAt)
            => InvokeTaskAsync(service, "RejectAsync", new object?[] { guild, offerId, rejectedByUserId, reason, resolvedAt },
                expectedParameterTypes: new[] { typeof(Guild), typeof(string), typeof(string), typeof(string), typeof(DateTimeOffset) });

        private static Type ResolveServiceType()
        {
            var asm = typeof(GuildRosterService).Assembly;

            foreach (var name in CandidateTypeNames)
            {
                var t = asm.GetType(name, throwOnError: false, ignoreCase: false);
                if (t != null)
                    return t;
            }

            var fallback = asm.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => string.Equals(t.Namespace, "Game.Core.Services", StringComparison.Ordinal))
                .FirstOrDefault(t => t.Name.Contains("Recruit", StringComparison.Ordinal) && t.Name.Contains("Service", StringComparison.Ordinal));

            if (fallback != null)
                return fallback;

            throw new InvalidOperationException(
                "Recruitment service type not found. Implement a public service in Game.Core.Services, e.g. Game.Core.Services.GuildRecruitmentService, " +
                "supporting ApplyAsync/ApproveAsync/WithdrawAsync and emitting typed contracts via IEventBus.");
        }

        private static bool TryBuildArguments(ConstructorInfo ctor, List<object> available, out object?[] args)
        {
            var parameters = ctor.GetParameters();
            args = new object?[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                var pType = parameters[i].ParameterType;

                var match = available.FirstOrDefault(a => pType.IsInstanceOfType(a));
                if (match == null)
                    return false;

                args[i] = match;
            }

            return true;
        }

        private static async Task InvokeTaskAsync(
            object service,
            string methodName,
            object?[] args,
            Type[] expectedParameterTypes)
        {
            var type = service.GetType();

            var method = type.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: expectedParameterTypes,
                modifiers: null);

            if (method == null)
            {
                var sig = string.Join(", ", expectedParameterTypes.Select(t => t.Name));
                throw new InvalidOperationException($"Missing method: {type.FullName}.{methodName}({sig}).");
            }

            var result = method.Invoke(service, args);
            result.Should().BeAssignableTo<Task>();
            await ((Task)result!).ConfigureAwait(false);
        }
    }
}
