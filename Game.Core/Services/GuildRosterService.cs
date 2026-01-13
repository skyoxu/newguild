using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Core.Contracts;
using Game.Core.Contracts.Guild;
using Game.Core.Domain;
using Game.Core.Repositories;

namespace Game.Core.Services;

/// <summary>
/// Core service for guild roster operations (join/kick/role changes) with event emission.
/// Follows ADR-0004 for domain event naming and uses strong-typed contracts in Game.Core/Contracts.
/// </summary>
public sealed class GuildRosterService
{
    private readonly IGuildRepository _repository;
    private readonly IEventBus _eventBus;
    private static bool IsValidRole(GuildRole role) => role is GuildRole.Member or GuildRole.Admin;

    public GuildRosterService(IGuildRepository repository, IEventBus bus)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _eventBus = bus ?? throw new ArgumentNullException(nameof(bus));
    }

    public async Task<bool> JoinAsync(
        Guild guild,
        string userId,
        GuildRole role,
        string requestedByUserId,
        DateTimeOffset joinedAt)
    {
        if (guild == null) throw new ArgumentNullException(nameof(guild));
        if (string.IsNullOrWhiteSpace(userId)) return false;
        if (string.IsNullOrWhiteSpace(requestedByUserId)) return false;
        if (!IsValidRole(role)) return false;
        if (!IsAdmin(guild, requestedByUserId)) return false;

        var snapshotMembers = guild.Members.ToList();
        if (!guild.AddMember(userId, role))
            return false;

        if (!await PersistOrRollbackAsync(guild, snapshotMembers))
            return false;

        var evt = new GuildMemberJoined(
            UserId: userId,
            GuildId: guild.GuildId,
            JoinedAt: joinedAt,
            Role: ToContractRole(role));

        await _eventBus.PublishAsync(ToDomainEvent(GuildMemberJoined.EventType, evt, joinedAt));
        return true;
    }

    public async Task<bool> ChangeRoleAsync(
        Guild guild,
        string userId,
        GuildRole newRole,
        string requestedByUserId,
        DateTimeOffset changedAt)
    {
        if (guild == null) throw new ArgumentNullException(nameof(guild));
        if (string.IsNullOrWhiteSpace(userId)) return false;
        if (string.IsNullOrWhiteSpace(requestedByUserId)) return false;
        if (!IsValidRole(newRole)) return false;
        if (!IsAdmin(guild, requestedByUserId)) return false;

        var existing = guild.Members.FirstOrDefault(m => m.UserId == userId);
        if (existing == null) return false;

        var oldRole = existing.Role;
        if (oldRole == newRole) return false;

        if (!IsValidRole(oldRole)) return false;

        var snapshotMembers = guild.Members.ToList();
        if (!guild.ChangeRole(userId, newRole))
            return false;

        if (!await PersistOrRollbackAsync(guild, snapshotMembers))
            return false;

        var evt = new GuildMemberRoleChanged(
            UserId: userId,
            GuildId: guild.GuildId,
            OldRole: ToContractRole(oldRole),
            NewRole: ToContractRole(newRole),
            ChangedAt: changedAt,
            ChangedByUserId: requestedByUserId);

        await _eventBus.PublishAsync(ToDomainEvent(GuildMemberRoleChanged.EventType, evt, changedAt));
        return true;
    }

    public async Task<bool> KickAsync(
        Guild guild,
        string userId,
        string requestedByUserId,
        string reason,
        DateTimeOffset leftAt)
    {
        if (guild == null) throw new ArgumentNullException(nameof(guild));
        if (string.IsNullOrWhiteSpace(userId)) return false;
        if (string.IsNullOrWhiteSpace(requestedByUserId)) return false;
        if (!IsAdmin(guild, requestedByUserId)) return false;
        if (string.IsNullOrWhiteSpace(reason)) return false;

        var snapshotMembers = guild.Members.ToList();
        if (!guild.RemoveMember(userId))
            return false;

        if (!await PersistOrRollbackAsync(guild, snapshotMembers))
            return false;

        var evt = new GuildMemberLeft(
            UserId: userId,
            GuildId: guild.GuildId,
            LeftAt: leftAt,
            Reason: reason);

        await _eventBus.PublishAsync(ToDomainEvent(GuildMemberLeft.EventType, evt, leftAt));
        return true;
    }

    public async Task<bool> LeaveAsync(
        Guild guild,
        string userId,
        DateTimeOffset leftAt)
    {
        if (guild == null) throw new ArgumentNullException(nameof(guild));
        if (string.IsNullOrWhiteSpace(userId)) return false;

        var snapshotMembers = guild.Members.ToList();
        if (!guild.RemoveMember(userId))
            return false;

        if (!await PersistOrRollbackAsync(guild, snapshotMembers))
            return false;

        var evt = new GuildMemberLeft(
            UserId: userId,
            GuildId: guild.GuildId,
            LeftAt: leftAt,
            Reason: "left");

        await _eventBus.PublishAsync(ToDomainEvent(GuildMemberLeft.EventType, evt, leftAt));
        return true;
    }

    private async Task<bool> PersistOrRollbackAsync(Guild guild, List<GuildMember> snapshotMembers)
    {
        try
        {
            await _repository.UpdateAsync(guild);
            return true;
        }
        catch
        {
            guild.Members.Clear();
            guild.Members.AddRange(snapshotMembers);
            return false;
        }
    }

    private static bool IsAdmin(Guild guild, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;
        var requesterMember = guild.Members.FirstOrDefault(member => member.UserId == userId);
        return requesterMember != null && requesterMember.Role == GuildRole.Admin;
    }

    private static string ToContractRole(GuildRole role) =>
        role switch
        {
            GuildRole.Member => "member",
            GuildRole.Admin => "admin",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Role must be a defined GuildRole value"),
        };

    private static DomainEvent ToDomainEvent(string type, object data, DateTimeOffset ts) =>
        new(
            Type: type,
            Source: nameof(GuildRosterService),
            Data: data,
            Timestamp: ts.UtcDateTime,
            Id: Guid.NewGuid().ToString("N"));
}
