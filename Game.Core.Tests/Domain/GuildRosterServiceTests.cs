using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Contracts;
using Game.Core.Contracts.Guild;
using Game.Core.Domain;
using Game.Core.Repositories;
using Game.Core.Services;
using Xunit;

namespace Game.Core.Tests.Domain;

public class GuildRosterServiceTests
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

    private sealed class NoopGuildRepository : IGuildRepository
    {
        public Task<Guild> CreateAsync(Guild guild) => throw new NotSupportedException("Not used by these tests");
        public Task<Guild?> GetByIdAsync(string guildId) => throw new NotSupportedException("Not used by these tests");
        public Task<bool> DeleteAsync(string guildId) => throw new NotSupportedException("Not used by these tests");
        public Task<IReadOnlyList<Guild>> GetAllAsync() => throw new NotSupportedException("Not used by these tests");
        public Task<IReadOnlyList<Guild>> FindByMemberAsync(string userId) => throw new NotSupportedException("Not used by these tests");

        public Task<Guild> UpdateAsync(Guild guild) => Task.FromResult(guild);
    }

    private sealed class ThrowingOnUpdateGuildRepository : IGuildRepository
    {
        public Task<Guild> CreateAsync(Guild guild) => throw new NotSupportedException("Not used by these tests");
        public Task<Guild?> GetByIdAsync(string guildId) => throw new NotSupportedException("Not used by these tests");
        public Task<bool> DeleteAsync(string guildId) => throw new NotSupportedException("Not used by these tests");
        public Task<IReadOnlyList<Guild>> GetAllAsync() => throw new NotSupportedException("Not used by these tests");
        public Task<IReadOnlyList<Guild>> FindByMemberAsync(string userId) => throw new NotSupportedException("Not used by these tests");

        public Task<Guild> UpdateAsync(Guild guild) => throw new InvalidOperationException("Simulated persistence failure");
    }

    private static GuildRosterService CreateService(CapturingEventBus bus, IGuildRepository? repository = null) =>
        new(repository ?? new NoopGuildRepository(), bus);

    [Fact]
    public void Constructor_WithNullEventBus_ThrowsArgumentNullException()
    {
        var repo = new NoopGuildRepository();
        IEventBus? bus = null;
        var ex = Assert.Throws<ArgumentNullException>(() => new GuildRosterService(repo, bus!));
        ex.ParamName.Should().Be("bus");
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        IGuildRepository? repo = null;
        var bus = new CapturingEventBus();
        var ex = Assert.Throws<ArgumentNullException>(() => new GuildRosterService(repo!, bus));
        ex.ParamName.Should().Be("repository");
    }

    [Fact]
    public async Task JoinAsync_ReturnsFalse_WhenRequesterNotAdmin()
    {
        var bus = new CapturingEventBus();
        var svc = CreateService(bus);
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");

        var ok = await svc.JoinAsync(
            guild,
            userId: "u1",
            role: GuildRole.Member,
            requestedByUserId: "u1",
            joinedAt: DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        ok.Should().BeFalse();
        bus.Published.Should().BeEmpty();
        guild.Members.Should().ContainSingle(m => m.UserId == "u-admin" && m.Role == GuildRole.Admin);
    }

    [Fact]
    public async Task JoinAsync_ReturnsFalse_WhenRequestedByIsWhitespace()
    {
        var bus = new CapturingEventBus();
        var svc = CreateService(bus);
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");

        var ok = await svc.JoinAsync(
            guild,
            userId: "u1",
            role: GuildRole.Member,
            requestedByUserId: " ",
            joinedAt: DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        ok.Should().BeFalse();
        bus.Published.Should().BeEmpty();
        guild.Members.Should().NotContain(m => m.UserId == "u1");
    }

    [Fact]
    public async Task JoinAsync_ReturnsFalse_WhenUserIdIsWhitespace()
    {
        var bus = new CapturingEventBus();
        var svc = CreateService(bus);
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");

        var ok = await svc.JoinAsync(
            guild,
            userId: " ",
            role: GuildRole.Member,
            requestedByUserId: "u-admin",
            joinedAt: DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        ok.Should().BeFalse();
        bus.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task JoinAsync_ReturnsFalse_WhenRoleInvalid()
    {
        var bus = new CapturingEventBus();
        var svc = CreateService(bus);
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");

        var ok = await svc.JoinAsync(
            guild,
            userId: "u1",
            role: (GuildRole)999,
            requestedByUserId: "u-admin",
            joinedAt: DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        ok.Should().BeFalse();
        bus.Published.Should().BeEmpty();
        guild.Members.Should().NotContain(m => m.UserId == "u1");
    }

    [Fact]
    public async Task JoinAsync_ReturnsFalse_WhenDuplicateMember()
    {
        var bus = new CapturingEventBus();
        var svc = CreateService(bus);
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");
        var t0 = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        (await svc.JoinAsync(guild, "u1", GuildRole.Member, "u-admin", t0)).Should().BeTrue();
        bus.Published.Clear();

        var ok = await svc.JoinAsync(guild, "u1", GuildRole.Member, "u-admin", t0.AddMinutes(1));

        ok.Should().BeFalse();
        bus.Published.Should().BeEmpty();
        guild.Members.Count(m => m.UserId == "u1").Should().Be(1);
    }

    [Fact]
    public async Task JoinAsync_RollsBackAndDoesNotPublishEvent_WhenRepositoryUpdateFails()
    {
        var bus = new CapturingEventBus();
        var svc = CreateService(bus, new ThrowingOnUpdateGuildRepository());
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");

        var ok = await svc.JoinAsync(
            guild,
            userId: "u1",
            role: GuildRole.Member,
            requestedByUserId: "u-admin",
            joinedAt: DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        ok.Should().BeFalse();
        bus.Published.Should().BeEmpty();
        guild.Members.Should().NotContain(m => m.UserId == "u1");
    }

    [Fact]
    public async Task ChangeRoleAsync_ReturnsFalse_WhenMemberNotFound()
    {
        var bus = new CapturingEventBus();
        var svc = CreateService(bus);
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");

        var ok = await svc.ChangeRoleAsync(
            guild,
            userId: "missing",
            newRole: GuildRole.Admin,
            requestedByUserId: "u-admin",
            changedAt: DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        ok.Should().BeFalse();
        bus.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task ChangeRoleAsync_ReturnsFalse_WhenUserIdMissing()
    {
        var bus = new CapturingEventBus();
        var svc = CreateService(bus);
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");

        var ok = await svc.ChangeRoleAsync(
            guild,
            userId: " ",
            newRole: GuildRole.Admin,
            requestedByUserId: "u-admin",
            changedAt: DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        ok.Should().BeFalse();
        bus.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task ChangeRoleAsync_ReturnsFalse_WhenRequesterMissing()
    {
        var bus = new CapturingEventBus();
        var svc = CreateService(bus);
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");
        var t0 = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        (await svc.JoinAsync(guild, "u1", GuildRole.Member, "u-admin", t0)).Should().BeTrue();
        bus.Published.Clear();

        var ok = await svc.ChangeRoleAsync(guild, "u1", GuildRole.Admin, requestedByUserId: " ", changedAt: t0.AddMinutes(1));

        ok.Should().BeFalse();
        bus.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task ChangeRoleAsync_ReturnsFalse_WhenNewRoleSameAsOld()
    {
        var bus = new CapturingEventBus();
        var svc = CreateService(bus);
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");
        var t0 = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        (await svc.JoinAsync(guild, "u1", GuildRole.Member, "u-admin", t0)).Should().BeTrue();
        bus.Published.Clear();

        var ok = await svc.ChangeRoleAsync(guild, "u1", GuildRole.Member, "u-admin", t0.AddMinutes(1));

        ok.Should().BeFalse();
        bus.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task ChangeRoleAsync_ReturnsFalse_WhenNewRoleInvalid()
    {
        var bus = new CapturingEventBus();
        var svc = CreateService(bus);
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");
        var t0 = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        (await svc.JoinAsync(guild, "u1", GuildRole.Member, "u-admin", t0)).Should().BeTrue();
        bus.Published.Clear();

        var ok = await svc.ChangeRoleAsync(guild, "u1", (GuildRole)999, "u-admin", t0.AddMinutes(1));

        ok.Should().BeFalse();
        bus.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task KickAsync_ReturnsFalse_WhenRequesterNotAdmin()
    {
        var bus = new CapturingEventBus();
        var svc = CreateService(bus);
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");
        var t0 = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        (await svc.JoinAsync(guild, "u1", GuildRole.Member, "u-admin", t0)).Should().BeTrue();
        bus.Published.Clear();

        var ok = await svc.KickAsync(guild, "u1", requestedByUserId: "u1", reason: "kicked", leftAt: t0.AddMinutes(1));

        ok.Should().BeFalse();
        bus.Published.Should().BeEmpty();
        guild.Members.Should().Contain(m => m.UserId == "u1");
    }

    [Fact]
    public async Task KickAsync_ReturnsFalse_WhenUserIdMissing()
    {
        var bus = new CapturingEventBus();
        var svc = CreateService(bus);
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");

        var ok = await svc.KickAsync(
            guild,
            userId: " ",
            requestedByUserId: "u-admin",
            reason: "kicked",
            leftAt: DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        ok.Should().BeFalse();
        bus.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task KickAsync_ReturnsFalse_WhenRequesterMissing()
    {
        var bus = new CapturingEventBus();
        var svc = CreateService(bus);
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");

        var ok = await svc.KickAsync(
            guild,
            userId: "u1",
            requestedByUserId: " ",
            reason: "kicked",
            leftAt: DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        ok.Should().BeFalse();
        bus.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task KickAsync_ReturnsFalse_WhenReasonMissing()
    {
        var bus = new CapturingEventBus();
        var svc = CreateService(bus);
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");
        var t0 = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        (await svc.JoinAsync(guild, "u1", GuildRole.Member, "u-admin", t0)).Should().BeTrue();
        bus.Published.Clear();

        var ok = await svc.KickAsync(guild, "u1", requestedByUserId: "u-admin", reason: "", leftAt: t0.AddMinutes(1));

        ok.Should().BeFalse();
        bus.Published.Should().BeEmpty();
        guild.Members.Should().Contain(m => m.UserId == "u1");
    }

    [Fact]
    public async Task KickAsync_ReturnsFalse_WhenTargetIsCreator()
    {
        var bus = new CapturingEventBus();
        var svc = CreateService(bus);
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");

        var ok = await svc.KickAsync(
            guild,
            userId: "u-admin",
            requestedByUserId: "u-admin",
            reason: "kicked",
            leftAt: DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        ok.Should().BeFalse();
        bus.Published.Should().BeEmpty();
        guild.Members.Should().ContainSingle(m => m.UserId == "u-admin" && m.Role == GuildRole.Admin);
    }

    [Fact]
    public async Task ChangeRoleAsync_ReturnsFalse_WhenExistingRoleInvalid()
    {
        var bus = new CapturingEventBus();
        var svc = CreateService(bus);
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");
        var t0 = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        // Force invalid role into state (defensive scenario).
        guild.Members.Add(new GuildMember("u1", (GuildRole)999));

        var ok = await svc.ChangeRoleAsync(guild, "u1", GuildRole.Admin, "u-admin", t0);

        ok.Should().BeFalse();
        bus.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task LeaveAsync_ReturnsFalse_WhenUserIdMissing()
    {
        var bus = new CapturingEventBus();
        var svc = CreateService(bus);
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");

        var ok = await svc.LeaveAsync(guild, userId: " ", leftAt: DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        ok.Should().BeFalse();
        bus.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task LeaveAsync_PublishesGuildMemberLeftEvent_WhenMemberLeaves()
    {
        var bus = new CapturingEventBus();
        var svc = CreateService(bus);
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");
        var t0 = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        (await svc.JoinAsync(guild, "u1", GuildRole.Member, "u-admin", t0)).Should().BeTrue();
        bus.Published.Clear();

        var leftAt = t0.AddMinutes(1);
        var ok = await svc.LeaveAsync(guild, userId: "u1", leftAt: leftAt);

        ok.Should().BeTrue();
        guild.Members.Should().NotContain(m => m.UserId == "u1");
        bus.Published.Should().ContainSingle(e => e.Type == GuildMemberLeft.EventType);
        var evt = (GuildMemberLeft)bus.Published.Single(e => e.Type == GuildMemberLeft.EventType).Data!;
        evt.UserId.Should().Be("u1");
        evt.GuildId.Should().Be("g1");
        evt.LeftAt.Should().Be(leftAt);
        evt.Reason.Should().Be("left");
    }

    [Fact]
    public async Task LeaveAsync_ReturnsFalse_WhenMemberNotFound()
    {
        var bus = new CapturingEventBus();
        var svc = CreateService(bus);
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");

        var ok = await svc.LeaveAsync(
            guild,
            userId: "missing",
            leftAt: DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        ok.Should().BeFalse();
        bus.Published.Should().BeEmpty();
    }
}
