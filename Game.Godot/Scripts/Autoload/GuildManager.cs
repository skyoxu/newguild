using Godot;
using Game.Core.Domain;
using Game.Core.Contracts.Guild;
using Game.Core.Repositories;
using Game.Core.Services;
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
    private GuildRosterService _roster = default!;
    private PlayerSession? _session;
    private LoggerAdapter? _logger;

    public override void _Ready()
    {
        // Initialize database adapter
        var dbRel = System.Environment.GetEnvironmentVariable("GD_GUILD_DB_PATH") ?? "data/game.db";
        var dbPath = SafeResourcePath.UserPath(dbRel) ?? throw new InvalidOperationException("Invalid database path (ADR-0019)");
        var db = new GodotSQLiteDatabase(dbPath);
        _repository = new SQLiteGuildRepository(db);

        // Get EventBus reference
        _eventBus = GetNode<EventBusAdapter>("/root/EventBus");
        _roster = new GuildRosterService(_repository, _eventBus);
        _session = GetNodeOrNull<PlayerSession>("/root/PlayerSession");
        _logger = GetNodeOrNull<LoggerAdapter>("/root/Logger");

        GD.Print("[GuildManager] Initialized with SQLite repository");
    }

    private bool TryGetCurrentUserId(out string userId)
    {
        userId = _session?.CurrentUserId?.Trim() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(userId);
    }

    private void DebugWarn(string message)
    {
#if DEBUG
        var msg = "[DEBUG] " + message;
        if (_logger != null) _logger.Warn(msg);
        else GD.PushWarning(msg);
#endif
    }

    private void DebugError(string message, Exception ex)
    {
#if DEBUG
        var msg = "[DEBUG] " + message + $" exType={ex.GetType().Name}";
        if (_logger != null) _logger.Error(msg);
        else GD.PrintErr(msg);
#endif
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
            _currentGuild = await _repository.CreateAsync(guild);

            var createdEvt = new GuildCreated(
                _currentGuild.GuildId,
                _currentGuild.CreatorId,
                _currentGuild.Name,
                _currentGuild.CreatedAt);

            await _eventBus.PublishAsync(new Game.Core.Contracts.DomainEvent(
                GuildCreated.EventType,
                nameof(GuildManager),
                createdEvt,
                createdEvt.CreatedAt.UtcDateTime,
                Guid.NewGuid().ToString("N")));

            GD.Print($"[GuildManager] Created guild '{guildName}' for user {creatorId}");
        }
        catch (Exception ex)
        {
            DebugError("CreateGuild failed", ex);
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
                var disbandedEvt = new GuildDisbanded(
                    guildId,
                    requestingUserId,
                    DateTimeOffset.UtcNow,
                    "disbanded");

                await _eventBus.PublishAsync(new Game.Core.Contracts.DomainEvent(
                    GuildDisbanded.EventType,
                    nameof(GuildManager),
                    disbandedEvt,
                    disbandedEvt.DisbandedAt.UtcDateTime,
                    Guid.NewGuid().ToString("N")));

                _currentGuild = null;

                GD.Print($"[GuildManager] Disbanded guild {guildId}");
            }
        }
        catch (Exception ex)
        {
            DebugError("DisbandGuild failed", ex);
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

            if (!TryGetCurrentUserId(out var requestedByUserId))
            {
                DebugWarn("AddMember denied: PlayerSession.CurrentUserId missing");
                return;
            }

            var ok = await _roster.JoinAsync(
                _currentGuild,
                userId: userId,
                role: GuildRole.Member,
                requestedByUserId: requestedByUserId,
                joinedAt: DateTimeOffset.UtcNow);

            if (!ok)
            {
                GD.PushWarning($"[GuildManager] AddMember denied for user {userId}");
                DebugWarn($"AddMember denied_or_persist_failed targetUserId={userId} requestedByUserId={requestedByUserId}");
                return;
            }

            GD.Print($"[GuildManager] Added member {userId} to guild {guildId}");
        }
        catch (Exception ex)
        {
            DebugError("AddMember failed", ex);
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

            if (!TryGetCurrentUserId(out var requestedByUserId))
            {
                DebugWarn("RemoveMember denied: PlayerSession.CurrentUserId missing");
                return;
            }

            var ok = await _roster.KickAsync(
                _currentGuild,
                userId: userId,
                requestedByUserId: requestedByUserId,
                reason: "kicked",
                leftAt: DateTimeOffset.UtcNow);

            if (!ok)
            {
                GD.PushWarning($"[GuildManager] RemoveMember denied for user {userId}");
                DebugWarn($"RemoveMember denied_or_persist_failed targetUserId={userId} requestedByUserId={requestedByUserId}");
                return;
            }

            GD.Print($"[GuildManager] Removed member {userId} from guild {guildId}");
        }
        catch (Exception ex)
        {
            DebugError("RemoveMember failed", ex);
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

            if (!TryGetCurrentUserId(out var requestedByUserId))
            {
                DebugWarn("PromoteMember denied: PlayerSession.CurrentUserId missing");
                return;
            }

            var ok = await _roster.ChangeRoleAsync(
                _currentGuild,
                userId: userId,
                newRole: GuildRole.Admin,
                requestedByUserId: requestedByUserId,
                changedAt: DateTimeOffset.UtcNow);

            if (!ok)
            {
                GD.PushWarning($"[GuildManager] PromoteMember denied for user {userId}");
                DebugWarn($"PromoteMember denied_or_persist_failed targetUserId={userId} requestedByUserId={requestedByUserId}");
                return;
            }

            GD.Print($"[GuildManager] Promoted member {userId} to Admin in guild {guildId}");
        }
        catch (Exception ex)
        {
            DebugError("PromoteMember failed", ex);
        }
    }

    public async void DemoteMember(string guildId, string userId)
    {
        try
        {
            if (_currentGuild == null || _currentGuild.GuildId != guildId)
            {
                GD.PushWarning($"[GuildManager] Guild {guildId} not found");
                return;
            }

            if (!TryGetCurrentUserId(out var requestedByUserId))
            {
                DebugWarn("DemoteMember denied: PlayerSession.CurrentUserId missing");
                return;
            }

            var ok = await _roster.ChangeRoleAsync(
                _currentGuild,
                userId: userId,
                newRole: GuildRole.Member,
                requestedByUserId: requestedByUserId,
                changedAt: DateTimeOffset.UtcNow);

            if (!ok)
            {
                GD.PushWarning($"[GuildManager] DemoteMember denied for user {userId}");
                DebugWarn($"DemoteMember denied_or_persist_failed targetUserId={userId} requestedByUserId={requestedByUserId}");
                return;
            }

            GD.Print($"[GuildManager] Demoted member {userId} to Member in guild {guildId}");
        }
        catch (Exception ex)
        {
            DebugError("DemoteMember failed", ex);
        }
    }

    public async void LeaveCurrentUser(string guildId)
    {
        try
        {
            if (_currentGuild == null || _currentGuild.GuildId != guildId)
            {
                GD.PushWarning($"[GuildManager] Guild {guildId} not found");
                return;
            }

            if (!TryGetCurrentUserId(out var currentUserId))
            {
                DebugWarn("LeaveCurrentUser denied: PlayerSession.CurrentUserId missing");
                return;
            }

            var ok = await _roster.LeaveAsync(_currentGuild, userId: currentUserId, leftAt: DateTimeOffset.UtcNow);
            if (!ok)
            {
                GD.PushWarning($"[GuildManager] LeaveCurrentUser denied for user {currentUserId}");
                DebugWarn($"LeaveCurrentUser denied_or_persist_failed userId={currentUserId}");
                return;
            }

            GD.Print($"[GuildManager] Member {currentUserId} left guild {guildId}");
        }
        catch (Exception ex)
        {
            DebugError("LeaveCurrentUser failed", ex);
        }
    }
}
