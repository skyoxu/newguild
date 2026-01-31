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
/// Core service for guild officer assignments with event emission.
/// Follows ADR-0004 for domain event naming and uses strong-typed contracts in Game.Core/Contracts.
/// </summary>
public sealed class GuildOfficerService
{
    private readonly IGuildRepository _repository;
    private readonly IEventBus _eventBus;

    public GuildOfficerService(IGuildRepository repository, IEventBus eventBus)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    public async Task<bool> AssignOfficerAsync(
        Guild guild,
        OfficerSlot slot,
        string userId,
        string assignedByUserId,
        DateTimeOffset assignedAt)
    {
        if (guild == null) throw new ArgumentNullException(nameof(guild));
        if (string.IsNullOrWhiteSpace(userId)) return false;
        if (string.IsNullOrWhiteSpace(assignedByUserId)) return false;
        if (!IsAdmin(guild, assignedByUserId)) return false;

        var snapshot = new Dictionary<OfficerSlot, string>(guild.OfficerAssignments);
        if (!guild.AssignOfficer(slot, userId))
            return false;

        if (!await PersistOrRollbackAsync(guild, snapshot).ConfigureAwait(false))
            return false;

        var evt = new GuildOfficerAssigned(
            GuildId: guild.GuildId,
            UserId: userId,
            Slot: ToSlotLabel(slot),
            AssignedAt: assignedAt,
            AssignedByUserId: assignedByUserId);

        await _eventBus.PublishAsync(ToDomainEvent(GuildOfficerAssigned.EventType, evt, assignedAt))
            .ConfigureAwait(false);
        return true;
    }

    public async Task<bool> RevokeOfficerAsync(
        Guild guild,
        OfficerSlot slot,
        string revokedByUserId,
        DateTimeOffset revokedAt)
    {
        if (guild == null) throw new ArgumentNullException(nameof(guild));
        if (string.IsNullOrWhiteSpace(revokedByUserId)) return false;
        if (!IsAdmin(guild, revokedByUserId)) return false;

        var snapshot = new Dictionary<OfficerSlot, string>(guild.OfficerAssignments);
        if (!guild.TryRevokeOfficer(slot, out var revokedUserId) || string.IsNullOrWhiteSpace(revokedUserId))
            return false;

        if (!await PersistOrRollbackAsync(guild, snapshot).ConfigureAwait(false))
            return false;

        var evt = new GuildOfficerRevoked(
            GuildId: guild.GuildId,
            UserId: revokedUserId,
            Slot: ToSlotLabel(slot),
            RevokedAt: revokedAt,
            RevokedByUserId: revokedByUserId);

        await _eventBus.PublishAsync(ToDomainEvent(GuildOfficerRevoked.EventType, evt, revokedAt))
            .ConfigureAwait(false);
        return true;
    }

    private async Task<bool> PersistOrRollbackAsync(Guild guild, Dictionary<OfficerSlot, string> snapshot)
    {
        try
        {
            await _repository.UpdateAsync(guild).ConfigureAwait(false);
            return true;
        }
        catch
        {
            guild.RestoreOfficerAssignments(snapshot);
            return false;
        }
    }

    private static bool IsAdmin(Guild guild, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;
        var requester = guild.Members.FirstOrDefault(member => member.UserId == userId);
        return requester != null && requester.Role == GuildRole.Admin;
    }

    private static string ToSlotLabel(OfficerSlot slot) => slot.ToString().ToLowerInvariant();

    private static DomainEvent ToDomainEvent(string type, object data, DateTimeOffset ts) =>
        new(
            Type: type,
            Source: nameof(GuildOfficerService),
            Data: data,
            Timestamp: ts.UtcDateTime,
            Id: Guid.NewGuid().ToString("N"));
}
