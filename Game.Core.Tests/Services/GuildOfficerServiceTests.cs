using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Contracts;
using Game.Core.Contracts.Guild;
using Game.Core.Domain;
using Game.Core.Repositories;
using Game.Core.Services;
using Game.Core.Tests.Repositories;
using Xunit;

namespace Game.Core.Tests.Services;

public class GuildOfficerServiceTests
{
    // ACC:T38.4
    [Fact]
    public async Task AssignOfficerAsync_Publishes_And_Delivers_OfficerAssigned_Event()
    {
        var repo = new InMemoryGuildRepository();
        var bus = new InMemoryEventBus();
        DomainEvent? received = null;

        using var sub = bus.Subscribe(evt =>
        {
            received = evt;
            return Task.CompletedTask;
        });

        var service = new GuildOfficerService(repo, bus);
        var guild = new Guild("g1", "u-admin", "Officers");
        guild.AddMember(new GuildMember("u1", "Alice", GuildRole.Member));
        await repo.CreateAsync(guild);

        var assignedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var ok = await service.AssignOfficerAsync(guild, OfficerSlot.Commander, "u1", "u-admin", assignedAt);

        ok.Should().BeTrue();
        received.Should().NotBeNull();
        received!.Type.Should().Be(GuildOfficerAssigned.EventType);

        var payload = received.Data.Should().BeOfType<GuildOfficerAssigned>().Subject;
        payload.GuildId.Should().Be("g1");
        payload.UserId.Should().Be("u1");
        payload.Slot.Should().Be("commander");
        payload.AssignedAt.Should().Be(assignedAt);
        payload.AssignedByUserId.Should().Be("u-admin");
    }

    [Fact]
    public async Task AssignOfficerAsync_ReturnsFalse_When_UserIdMissing()
    {
        var repository = new InMemoryGuildRepository();
        var eventBus = new InMemoryEventBus();
        DomainEvent? received = null;

        using var sub = eventBus.Subscribe(evt =>
        {
            received = evt;
            return Task.CompletedTask;
        });

        var service = new GuildOfficerService(repository, eventBus);
        var guild = new Guild("g2", "u-admin", "Officers");
        await repository.CreateAsync(guild);

        var ok = await service.AssignOfficerAsync(
            guild,
            OfficerSlot.Commander,
            string.Empty,
            "u-admin",
            DateTimeOffset.Parse("2026-01-02T00:00:00Z"));

        ok.Should().BeFalse();
        received.Should().BeNull();
        guild.GetOfficerAssignment(OfficerSlot.Commander).Should().BeNull();
    }

    [Fact]
    public async Task AssignOfficerAsync_ReturnsFalse_When_AssignedByMissing()
    {
        var repository = new InMemoryGuildRepository();
        var eventBus = new InMemoryEventBus();
        DomainEvent? received = null;

        using var sub = eventBus.Subscribe(evt =>
        {
            received = evt;
            return Task.CompletedTask;
        });

        var service = new GuildOfficerService(repository, eventBus);
        var guild = new Guild("g3", "u-admin", "Officers");
        guild.AddMember(new GuildMember("u1", "Alice", GuildRole.Member));
        await repository.CreateAsync(guild);

        var ok = await service.AssignOfficerAsync(
            guild,
            OfficerSlot.Commander,
            "u1",
            "  ",
            DateTimeOffset.Parse("2026-01-03T00:00:00Z"));

        ok.Should().BeFalse();
        received.Should().BeNull();
        guild.GetOfficerAssignment(OfficerSlot.Commander).Should().BeNull();
    }

    [Fact]
    public async Task AssignOfficerAsync_ReturnsFalse_When_AssignedByIsNotAdmin()
    {
        var repository = new InMemoryGuildRepository();
        var eventBus = new InMemoryEventBus();
        DomainEvent? received = null;

        using var sub = eventBus.Subscribe(evt =>
        {
            received = evt;
            return Task.CompletedTask;
        });

        var service = new GuildOfficerService(repository, eventBus);
        var guild = new Guild("g4", "u-admin", "Officers");
        guild.AddMember(new GuildMember("u1", "Alice", GuildRole.Member));
        guild.AddMember(new GuildMember("u-member", "Bob", GuildRole.Member));
        await repository.CreateAsync(guild);

        var ok = await service.AssignOfficerAsync(
            guild,
            OfficerSlot.Commander,
            "u1",
            "u-member",
            DateTimeOffset.Parse("2026-01-04T00:00:00Z"));

        ok.Should().BeFalse();
        received.Should().BeNull();
        guild.GetOfficerAssignment(OfficerSlot.Commander).Should().BeNull();
    }

    [Fact]
    public async Task AssignOfficerAsync_ReturnsFalse_When_AssigneeIsNotMember()
    {
        var repository = new InMemoryGuildRepository();
        var eventBus = new InMemoryEventBus();
        DomainEvent? received = null;

        using var sub = eventBus.Subscribe(evt =>
        {
            received = evt;
            return Task.CompletedTask;
        });

        var service = new GuildOfficerService(repository, eventBus);
        var guild = new Guild("g5", "u-admin", "Officers");
        await repository.CreateAsync(guild);

        var ok = await service.AssignOfficerAsync(
            guild,
            OfficerSlot.Commander,
            "u-missing",
            "u-admin",
            DateTimeOffset.Parse("2026-01-05T00:00:00Z"));

        ok.Should().BeFalse();
        received.Should().BeNull();
        guild.GetOfficerAssignment(OfficerSlot.Commander).Should().BeNull();
    }

    [Fact]
    public async Task AssignOfficerAsync_RollsBack_When_PersistFails()
    {
        var repository = new FailingUpdateGuildRepository();
        var eventBus = new InMemoryEventBus();
        DomainEvent? received = null;

        using var sub = eventBus.Subscribe(evt =>
        {
            received = evt;
            return Task.CompletedTask;
        });

        var service = new GuildOfficerService(repository, eventBus);
        var guild = new Guild("g6", "u-admin", "Officers");
        guild.AddMember(new GuildMember("u1", "Alice", GuildRole.Member));

        var ok = await service.AssignOfficerAsync(
            guild,
            OfficerSlot.Commander,
            "u1",
            "u-admin",
            DateTimeOffset.Parse("2026-01-06T00:00:00Z"));

        ok.Should().BeFalse();
        received.Should().BeNull();
        guild.GetOfficerAssignment(OfficerSlot.Commander).Should().BeNull();
    }

    private sealed class FailingUpdateGuildRepository : IGuildRepository
    {
        public Task<Guild> CreateAsync(Guild guild) => Task.FromResult(guild);
        public Task<Guild?> GetByIdAsync(string guildId) => Task.FromResult<Guild?>(null);
        public Task<Guild> UpdateAsync(Guild guild) =>
            throw new InvalidOperationException("Persist failed");
        public Task<bool> DeleteAsync(string guildId) => Task.FromResult(false);
        public Task<IReadOnlyList<Guild>> GetAllAsync() =>
            Task.FromResult<IReadOnlyList<Guild>>(Array.Empty<Guild>());
        public Task<IReadOnlyList<Guild>> FindByMemberAsync(string userId) =>
            Task.FromResult<IReadOnlyList<Guild>>(Array.Empty<Guild>());
    }
}
