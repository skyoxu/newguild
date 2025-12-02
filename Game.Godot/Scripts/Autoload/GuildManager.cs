using Godot;
using Game.Core.Domain;
using Game.Core.Repositories;
using Game.Core.Contracts.Guild;
using Game.Godot.Adapters;
using Game.Godot.Adapters.Db;
using System;
using System.Threading.Tasks;

namespace Game.Godot.Scripts.Autoload;

/// <summary>
/// Guild management singleton (Autoload).
/// Coordinates guild operations between UI layer and Core domain logic.
/// Follows ADR-0018 (adapter pattern) and ADR-0004 (event contracts).
/// </summary>
public partial class GuildManager : Node
{
    private IGuildRepository _repository = default!;
    private EventBusAdapter _eventBus = default!;
    private Guild? _currentGuild;

    public override void _Ready()
    {
        // Initialize database adapter
        var db = new GodotSQLiteDatabase("user://game.db");
        _repository = new SQLiteGuildRepository(db);

        // Get EventBus reference
        _eventBus = GetNode<EventBusAdapter>("/root/EventBus");

        GD.Print("[GuildManager] Initialized with SQLite repository");
    }

    public async void CreateGuild(string creatorId, string guildName)
    {
        try
        {
            if (_currentGuild != null)
            {
                GD.PushWarning($"[GuildManager] User {creatorId} already has a guild: {_currentGuild.Name}");
                return;
            }

            // Create guild via Core domain logic
            string guildId = Guid.NewGuid().ToString("N");
            var guild = new Guild(guildId, creatorId, guildName);

            // Persist to database
            await _repository.CreateAsync(guild);
            _currentGuild = guild;

            // Publish domain event
            await PublishGuildCreatedEvent(guild);

            GD.Print($"[GuildManager] Created guild '{guildName}' for user {creatorId}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GuildManager] Failed to create guild: {ex.Message}");
        }
    }

    public async void DisbandGuild(string guildId, string requestingUserId)
    {
        try
        {
            if (_currentGuild == null || _currentGuild.GuildId != guildId)
            {
                GD.PushWarning($"[GuildManager] Guild {guildId} not found or not current");
                return;
            }

            // Check if requesting user is creator
            if (_currentGuild.CreatorId != requestingUserId)
            {
                GD.PushWarning($"[GuildManager] User {requestingUserId} is not authorized to disband guild {guildId}");
                return;
            }

            // Delete from database
            bool success = await _repository.DeleteAsync(guildId);
            if (success)
            {
                await PublishGuildDisbandedEvent(guildId, requestingUserId);
                _currentGuild = null;

                GD.Print($"[GuildManager] Disbanded guild {guildId}");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GuildManager] Failed to disband guild: {ex.Message}");
        }
    }

    public async void AddMember(string guildId, string userId)
    {
        try
        {
            if (_currentGuild == null || _currentGuild.GuildId != guildId)
            {
                GD.PushWarning($"[GuildManager] Guild {guildId} not found");
                return;
            }

            // Add member via Core domain logic
            _currentGuild.AddMember(userId, GuildRole.Member);

            // Persist changes
            await _repository.UpdateAsync(_currentGuild);

            // Publish domain event
            await PublishMemberJoinedEvent(guildId, userId, "Member");

            GD.Print($"[GuildManager] Added member {userId} to guild {guildId}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GuildManager] Failed to add member: {ex.Message}");
        }
    }

    public async void RemoveMember(string guildId, string userId)
    {
        try
        {
            if (_currentGuild == null || _currentGuild.GuildId != guildId)
            {
                GD.PushWarning($"[GuildManager] Guild {guildId} not found");
                return;
            }

            // Remove member via Core domain logic
            _currentGuild.RemoveMember(userId);

            // Persist changes
            await _repository.UpdateAsync(_currentGuild);

            // Publish domain event
            await PublishMemberLeftEvent(guildId, userId);

            GD.Print($"[GuildManager] Removed member {userId} from guild {guildId}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GuildManager] Failed to remove member: {ex.Message}");
        }
    }

    public async void PromoteMember(string guildId, string userId)
    {
        try
        {
            if (_currentGuild == null || _currentGuild.GuildId != guildId)
            {
                GD.PushWarning($"[GuildManager] Guild {guildId} not found");
                return;
            }

            // Promote member via Core domain logic
            _currentGuild.ChangeRole(userId, GuildRole.Admin);

            // Persist changes
            await _repository.UpdateAsync(_currentGuild);

            // Publish domain event
            await PublishMemberRoleChangedEvent(guildId, userId, "Member", "Admin");

            GD.Print($"[GuildManager] Promoted member {userId} to Admin in guild {guildId}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GuildManager] Failed to promote member: {ex.Message}");
        }
    }

    // Event publishing helpers
    private async Task PublishGuildCreatedEvent(Guild guild)
    {
        var evt = new GuildCreated(
            guild.GuildId,
            guild.CreatorId,
            guild.Name,
            guild.CreatedAt
        );

        await _eventBus.PublishAsync(new Game.Core.Contracts.DomainEvent(
            GuildCreated.EventType,
            "GuildManager",
            System.Text.Json.JsonSerializer.Serialize(new
            {
                guildId = evt.GuildId,
                creatorId = evt.CreatorId,
                guildName = evt.GuildName,
                createdAt = evt.CreatedAt.ToString("o")
            }),
            evt.CreatedAt.DateTime,
            Guid.NewGuid().ToString("N")
        ));
    }

    private async Task PublishGuildDisbandedEvent(string guildId, string disbandedBy)
    {
        var evt = new GuildDisbanded(
            guildId,
            disbandedBy,
            DateTimeOffset.UtcNow,
            "Disbanded by admin" // Reason
        );

        await _eventBus.PublishAsync(new Game.Core.Contracts.DomainEvent(
            GuildDisbanded.EventType,
            "GuildManager",
            System.Text.Json.JsonSerializer.Serialize(new
            {
                guildId = evt.GuildId,
                disbandedByUserId = evt.DisbandedByUserId,
                disbandedAt = evt.DisbandedAt.ToString("o"),
                reason = evt.Reason
            }),
            evt.DisbandedAt.DateTime,
            Guid.NewGuid().ToString("N")
        ));
    }

    private async Task PublishMemberJoinedEvent(string guildId, string userId, string role)
    {
        var evt = new GuildMemberJoined(
            userId,
            guildId,
            DateTimeOffset.UtcNow,
            role
        );

        await _eventBus.PublishAsync(new Game.Core.Contracts.DomainEvent(
            GuildMemberJoined.EventType,
            "GuildManager",
            System.Text.Json.JsonSerializer.Serialize(new
            {
                userId = evt.UserId,
                guildId = evt.GuildId,
                joinedAt = evt.JoinedAt.ToString("o"),
                role = evt.Role
            }),
            evt.JoinedAt.DateTime,
            Guid.NewGuid().ToString("N")
        ));
    }

    private async Task PublishMemberLeftEvent(string guildId, string userId)
    {
        var evt = new GuildMemberLeft(
            userId,
            guildId,
            DateTimeOffset.UtcNow,
            "Member left" // Reason
        );

        await _eventBus.PublishAsync(new Game.Core.Contracts.DomainEvent(
            GuildMemberLeft.EventType,
            "GuildManager",
            System.Text.Json.JsonSerializer.Serialize(new
            {
                userId = evt.UserId,
                guildId = evt.GuildId,
                leftAt = evt.LeftAt.ToString("o"),
                reason = evt.Reason
            }),
            evt.LeftAt.DateTime,
            Guid.NewGuid().ToString("N")
        ));
    }

    private async Task PublishMemberRoleChangedEvent(string guildId, string userId, string oldRole, string newRole)
    {
        var evt = new GuildMemberRoleChanged(
            userId,
            guildId,
            oldRole,
            newRole,
            DateTimeOffset.UtcNow,
            "system" // ChangedByUserId - TODO: Get from actual admin/session
        );

        await _eventBus.PublishAsync(new Game.Core.Contracts.DomainEvent(
            GuildMemberRoleChanged.EventType,
            "GuildManager",
            System.Text.Json.JsonSerializer.Serialize(new
            {
                userId = evt.UserId,
                guildId = evt.GuildId,
                oldRole = evt.OldRole,
                newRole = evt.NewRole,
                changedAt = evt.ChangedAt.ToString("o"),
                changedByUserId = evt.ChangedByUserId
            }),
            evt.ChangedAt.DateTime,
            Guid.NewGuid().ToString("N")
        ));
    }
}
