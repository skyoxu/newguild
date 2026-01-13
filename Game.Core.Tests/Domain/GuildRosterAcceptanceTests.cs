using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Contracts;
using Game.Core.Contracts.Guild;
using Game.Core.Domain;
using Game.Core.Repositories;
using Game.Core.Services;
using Xunit;

namespace Game.Core.Tests.Domain;

public class GuildRosterAcceptanceTests
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

    private static string ToContractRole(GuildRole role) =>
        role switch
        {
            GuildRole.Member => "member",
            GuildRole.Admin => "admin",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Role must be a defined GuildRole value")
        };

    private static object CreateRosterServiceOrFail(IGuildRepository repository, IEventBus bus)
    {
        var assembly = typeof(Guild).Assembly;
        var type = assembly.GetType("Game.Core.Services.GuildRosterService", throwOnError: false);

        type.Should().NotBeNull("Roster acceptance requires Game.Core.Services.GuildRosterService to exist");

        var ctor = type!.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(c =>
            {
                var parameters = c.GetParameters();
                return parameters.Length == 2
                       && parameters[0].ParameterType == typeof(IGuildRepository)
                       && parameters[1].ParameterType == typeof(IEventBus);
            });

        ctor.Should().NotBeNull("GuildRosterService must have a public constructor: .ctor(IGuildRepository repository, IEventBus bus)");

        return ctor!.Invoke(new object[] { repository, bus });
    }

    private static MethodInfo FindMethodOrFail(Type type, string methodName, object[] args)
    {
        var candidates = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => string.Equals(m.Name, methodName, StringComparison.Ordinal))
            .ToList();

        candidates.Should().NotBeEmpty($"Expected public method {type.FullName}.{methodName}(...) to exist");

        foreach (var candidate in candidates)
        {
            var parameters = candidate.GetParameters();
            if (parameters.Length != args.Length) continue;

            var matched = true;
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameterType = parameters[i].ParameterType;
                var arg = args[i];

                if (arg is null)
                {
                    matched = !parameterType.IsValueType || Nullable.GetUnderlyingType(parameterType) != null;
                    if (!matched) break;
                    continue;
                }

                if (!parameterType.IsInstanceOfType(arg))
                {
                    matched = false;
                    break;
                }
            }

            if (matched) return candidate;
        }

        throw new InvalidOperationException($"No overload matched {type.FullName}.{methodName} with {args.Length} argument(s).");
    }

    private static async Task<bool> InvokeBoolOrTaskBool(object instance, string methodName, params object[] args)
    {
        var type = instance.GetType();
        var method = FindMethodOrFail(type, methodName, args);
        var result = method.Invoke(instance, args);

        if (result is Task<bool> taskBool) return await taskBool;
        if (result is bool b) return b;

        throw new InvalidOperationException(
            $"Expected {type.FullName}.{methodName} to return bool or Task<bool>, but got {result?.GetType().FullName ?? "null"}");
    }

    private static DomainEvent SinglePublishedOfType(CapturingEventBus bus, string type)
    {
        bus.Published.Should().ContainSingle(e => e.Type == type);
        return bus.Published.Single(e => e.Type == type);
    }

    // ACC:T13.1
    [Theory]
    [InlineData(999)]
    [InlineData(-1)]
    public void Should_RejectInvalidRole_When_AddingMember(int rawRole)
    {
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");
        var invalidRole = (GuildRole)rawRole;

        var added = guild.AddMember(userId: "u1", role: invalidRole);

        added.Should().BeFalse("invalid roles must be rejected");
        guild.Members.Should().NotContain(m => m.UserId == "u1");
    }

    [Fact]
    public void Should_NotAddDuplicateMember_When_AddingSameUserTwice()
    {
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");

        var first = guild.AddMember(userId: "u1", role: GuildRole.Member);
        var second = guild.AddMember(userId: "u1", role: GuildRole.Member);

        first.Should().BeTrue();
        second.Should().BeFalse("duplicate members must be rejected");
        guild.Members.Count(m => m.UserId == "u1").Should().Be(1);
    }

    [Fact]
    public void Should_RefuseToRemoveCreator_When_RemovingMember()
    {
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");

        var removed = guild.RemoveMember(userId: "u-admin");

        removed.Should().BeFalse("the creator must not be removable");
        guild.Members.Should().ContainSingle(m => m.UserId == "u-admin" && m.Role == GuildRole.Admin);
    }

    // ACC:T13.2
    [Fact]
    public async Task Should_PublishGuildMemberJoinedEvent_When_MemberJoins()
    {
        var bus = new CapturingEventBus();
        var service = CreateRosterServiceOrFail(new NoopGuildRepository(), bus);
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");
        var joinedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        var ok = await InvokeBoolOrTaskBool(
            service,
            methodName: "JoinAsync",
            guild,
            "u1",
            GuildRole.Member,
            "u-admin",
            joinedAt);

        ok.Should().BeTrue();
        guild.Members.Should().ContainSingle(m => m.UserId == "u1" && m.Role == GuildRole.Member);

        var evt = SinglePublishedOfType(bus, GuildMemberJoined.EventType);
        evt.Source.Should().Be("GuildRosterService");
        evt.Data.Should().BeOfType<GuildMemberJoined>();

        var data = (GuildMemberJoined)evt.Data!;
        data.UserId.Should().Be("u1");
        data.GuildId.Should().Be("g1");
        data.JoinedAt.Should().Be(joinedAt);
        data.Role.Should().Be(ToContractRole(GuildRole.Member));
    }

    [Fact]
    public async Task Should_RefuseRoleChange_When_RequesterIsNotAdmin_AndNotPublishEvent()
    {
        var bus = new CapturingEventBus();
        var service = CreateRosterServiceOrFail(new NoopGuildRepository(), bus);
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");
        var t0 = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        await InvokeBoolOrTaskBool(service, "JoinAsync", guild, "u1", GuildRole.Member, "u-admin", t0);
        bus.Published.Clear();

        var changed = await InvokeBoolOrTaskBool(
            service,
            methodName: "ChangeRoleAsync",
            guild,
            "u1",
            GuildRole.Admin,
            "u1",
            t0.AddMinutes(5));

        changed.Should().BeFalse("role changes must be denied when requester lacks permission");
        guild.Members.Should().ContainSingle(m => m.UserId == "u1" && m.Role == GuildRole.Member);
        bus.Published.Should().NotContain(e => e.Type == GuildMemberRoleChanged.EventType);
    }

    [Fact]
    public async Task Should_PublishGuildMemberLeftEvent_WithReasonKicked_When_AdminKicksMember()
    {
        var bus = new CapturingEventBus();
        var service = CreateRosterServiceOrFail(new NoopGuildRepository(), bus);
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");
        var t0 = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        await InvokeBoolOrTaskBool(service, "JoinAsync", guild, "u1", GuildRole.Member, "u-admin", t0);
        bus.Published.Clear();

        var ok = await InvokeBoolOrTaskBool(
            service,
            methodName: "KickAsync",
            guild,
            "u1",
            "u-admin",
            "kicked",
            t0.AddMinutes(10));

        ok.Should().BeTrue();
        guild.Members.Should().NotContain(m => m.UserId == "u1");

        var evt = SinglePublishedOfType(bus, GuildMemberLeft.EventType);
        evt.Data.Should().BeOfType<GuildMemberLeft>();

        var data = (GuildMemberLeft)evt.Data!;
        data.UserId.Should().Be("u1");
        data.GuildId.Should().Be("g1");
        data.LeftAt.Should().Be(t0.AddMinutes(10));
        data.Reason.Should().Be("kicked");
    }

    // ACC:T13.4
    [Fact]
    public async Task Should_EmitEventsInOrder_And_KeepRosterConsistent_ForJoinPromoteKickFlow()
    {
        var bus = new CapturingEventBus();
        var service = CreateRosterServiceOrFail(new NoopGuildRepository(), bus);
        var guild = new Guild(guildId: "g1", creatorId: "u-admin", name: "Test Guild");
        var t0 = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        var joined = await InvokeBoolOrTaskBool(service, "JoinAsync", guild, "u1", GuildRole.Member, "u-admin", t0);
        var promoted = await InvokeBoolOrTaskBool(service, "ChangeRoleAsync", guild, "u1", GuildRole.Admin, "u-admin", t0.AddMinutes(1));
        var kicked = await InvokeBoolOrTaskBool(service, "KickAsync", guild, "u1", "u-admin", "kicked", t0.AddMinutes(2));

        joined.Should().BeTrue();
        promoted.Should().BeTrue();
        kicked.Should().BeTrue();

        guild.Members.Should().ContainSingle(m => m.UserId == "u-admin" && m.Role == GuildRole.Admin);
        guild.Members.Should().NotContain(m => m.UserId == "u1");

        bus.Published.Select(e => e.Type).Should().Equal(
            GuildMemberJoined.EventType,
            GuildMemberRoleChanged.EventType,
            GuildMemberLeft.EventType);

        var roleChanged = bus.Published.Single(e => e.Type == GuildMemberRoleChanged.EventType);
        roleChanged.Data.Should().BeOfType<GuildMemberRoleChanged>();

        var rc = (GuildMemberRoleChanged)roleChanged.Data!;
        rc.UserId.Should().Be("u1");
        rc.GuildId.Should().Be("g1");
        rc.OldRole.Should().Be("member");
        rc.NewRole.Should().Be("admin");
        rc.ChangedByUserId.Should().Be("u-admin");
    }
}
