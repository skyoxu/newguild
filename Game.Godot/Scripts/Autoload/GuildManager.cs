using Godot;
using Game.Core.Domain;
using Game.Core.Contracts.Guild;
using Game.Core.Ports;
using Game.Core.Repositories;
using Game.Core.Services;
using Game.Godot.Adapters;
using Game.Godot.Adapters.Db;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Game.Godot.Scripts.Autoload;

/// <summary>
/// Guild management singleton (Autoload).
/// Coordinates guild operations between UI layer and Core domain logic.
/// Follows ADR-0018 (adapter pattern) and ADR-0004 (event contracts).
/// </summary>
public partial class GuildManager : Node
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private IGuildRepository _repository = default!;
    private EventBusAdapter _eventBus = default!;
    private Guild? _currentGuild;
    private GuildRosterService _roster = default!;
    private GuildOfficerService _officers = default!;
    private GuildRecruitmentService _recruitment = default!;
    private PlayerSession? _session;
    private LoggerAdapter? _logger;
    private string? _lastError;
    private bool _initializedOk;

    public override void _Ready()
    {
        try
        {
            _lastError = null;
            _initializedOk = false;

            // Initialize database adapter
            //
            // Prefer Godot's environment accessor so GDScript tests can configure the DB path deterministically
            // via OS.set_environment() before this node initializes. Fall back to the process environment for CI.
            var rawDbRel = OS.GetEnvironment("GD_GUILD_DB_PATH");
            if (string.IsNullOrWhiteSpace(rawDbRel))
                rawDbRel = System.Environment.GetEnvironmentVariable("GD_GUILD_DB_PATH");
            rawDbRel ??= "data/game.db";
            var dbRel = SanitizeEnvPath(rawDbRel);
            var dbPath = SafeResourcePath.UserPath(dbRel);
            if (dbPath == null)
            {
                // Stop-loss: ignore invalid env var (often quoted) and fall back to a safe default.
                _lastError = $"Invalid GD_GUILD_DB_PATH='{rawDbRel}'. Falling back to 'data/game.db'.";
                dbPath = SafeResourcePath.UserPath("data/game.db");
            }
            if (dbPath == null)
                throw new InvalidOperationException("Invalid database path (ADR-0019)");

            var db = new GodotSQLiteDatabase(dbPath);
            _repository = new SQLiteGuildRepository(db);
            var recruitmentOffers = new SQLiteRecruitmentOfferRepository(db);

            // Get EventBus reference
            _eventBus = GetNode<EventBusAdapter>("/root/EventBus");
            _roster = new GuildRosterService(_repository, _eventBus);
            _session = GetNodeOrNull<PlayerSession>("/root/PlayerSession");
            _logger = GetNodeOrNull<LoggerAdapter>("/root/Logger");

            var loggerPort = (ILogger?)_logger ?? new DevNullLogger();
            _officers = new GuildOfficerService(_repository, _eventBus, loggerPort);
            _recruitment = new GuildRecruitmentService(
                _repository,
                recruitmentOffers,
                _eventBus,
                _roster,
                new NoopTimePort(),
                loggerPort,
                new AlwaysEnabledEventCatalog());

            _initializedOk = true;
            if (!string.IsNullOrWhiteSpace(_lastError))
                GD.PushWarning("[GuildManager] " + _lastError);
            GD.Print("[GuildManager] Initialized with SQLite repository");
        }
        catch (Exception ex)
        {
            _lastError = $"GuildManager init failed exType={ex.GetType().Name} msg={ex.Message}";
            GD.PrintErr("[GuildManager] " + _lastError);
            _initializedOk = false;
        }
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
        var msg = "[DEBUG] " + message + $" exType={ex.GetType().Name} msg={ex.Message}";
        if (_logger != null) _logger.Error(msg);
        else GD.PrintErr(msg);
#endif
    }

    public async void ApplyForGuild(string guildId, string candidateId, string role)
    {
        try
        {
            if (_currentGuild == null || _currentGuild.GuildId != guildId)
            {
                DebugWarn($"ApplyForGuild denied: guild not current guildId={guildId}");
                return;
            }

            await _recruitment
                .ApplyAsync(_currentGuild, candidateId, role, DateTimeOffset.UtcNow)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DebugError("ApplyForGuild failed", ex);
        }
    }

    public async void ApproveOffer(string guildId, string offerId)
    {
        try
        {
            if (_currentGuild == null || _currentGuild.GuildId != guildId)
            {
                DebugWarn($"ApproveOffer denied: guild not current guildId={guildId}");
                return;
            }

            if (!TryGetCurrentUserId(out var approvedByUserId))
            {
                DebugWarn("ApproveOffer denied: PlayerSession.CurrentUserId missing");
                return;
            }

            await _recruitment
                .ApproveAsync(_currentGuild, offerId, approvedByUserId, DateTimeOffset.UtcNow)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DebugError("ApproveOffer failed", ex);
        }
    }

    public async void RejectOffer(string guildId, string offerId, string reason)
    {
        try
        {
            if (_currentGuild == null || _currentGuild.GuildId != guildId)
            {
                DebugWarn($"RejectOffer denied: guild not current guildId={guildId}");
                return;
            }

            if (!TryGetCurrentUserId(out var rejectedByUserId))
            {
                DebugWarn("RejectOffer denied: PlayerSession.CurrentUserId missing");
                return;
            }

            await _recruitment
                .RejectAsync(_currentGuild, offerId, rejectedByUserId, reason, DateTimeOffset.UtcNow)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DebugError("RejectOffer failed", ex);
        }
    }

    private sealed class AlwaysEnabledEventCatalog : IEventCatalog
    {
        public bool IsEventEnabled(string eventType) => true;
    }

    private sealed class NoopTimePort : ITime
    {
        public double DeltaSeconds => 0.0;
        public DateTimeOffset UtcNowOffset => DateTimeOffset.UtcNow;
    }

    private sealed class DevNullLogger : ILogger
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
        public void Error(string message, Exception ex) { }
    }

    public string CreateGuild(string creatorId, string guildName)
    {
        _ = CreateGuildAsync(creatorId, guildName);
        return "PENDING";
    }

    public async Task<string> CreateGuildAsync(string creatorId, string guildName)
    {
        try
        {
            _lastError = null;
            if (!_initializedOk)
                return "ERROR:NOT_READY";

            if (_currentGuild != null)
            {
                GD.PushWarning($"[GuildManager] User {creatorId} already has a guild: {_currentGuild.Name}");
                PublishGuildCreatedSnapshot(_currentGuild);
                return "ALREADY";
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
            return "OK";
        }
        catch (Exception ex)
        {
            DebugError("CreateGuild failed", ex);
            _lastError = $"CreateGuild failed exType={ex.GetType().Name} msg={ex.Message}";
            return "ERROR:" + ex.GetType().Name;
        }
    }

    public bool HasCurrentGuild() => _currentGuild != null;

    public string GetCurrentGuildSummaryJson()
    {
        if (!_initializedOk || _currentGuild == null)
            return "{}";

        return JsonSerializer.Serialize(new
        {
            guildId = _currentGuild.GuildId,
            creatorId = _currentGuild.CreatorId,
            guildName = _currentGuild.Name,
            createdAt = _currentGuild.CreatedAt,
        }, JsonOptions);
    }

    private void PublishGuildCreatedSnapshot(Guild guild)
    {
        _ = PublishGuildCreatedSnapshotAsync(guild);
    }

    private async Task PublishGuildCreatedSnapshotAsync(Guild guild)
    {
        try
        {
            var createdEvt = new GuildCreated(
                guild.GuildId,
                guild.CreatorId,
                guild.Name,
                guild.CreatedAt);

            await _eventBus.PublishAsync(new Game.Core.Contracts.DomainEvent(
                GuildCreated.EventType,
                nameof(GuildManager),
                createdEvt,
                createdEvt.CreatedAt.UtcDateTime,
                Guid.NewGuid().ToString("N")));
        }
        catch (Exception ex)
        {
            DebugError("PublishGuildCreatedSnapshot failed", ex);
            _lastError = $"PublishGuildCreatedSnapshot failed exType={ex.GetType().Name} msg={ex.Message}";
        }
    }

    public string GetLastError() => _lastError ?? string.Empty;

    private static string SanitizeEnvPath(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var s = raw.Trim();
        // Common Windows env var mistake: set GD_GUILD_DB_PATH="data/game.db" includes quotes.
        if (s.Length >= 2 && ((s.StartsWith("\"", StringComparison.Ordinal) && s.EndsWith("\"", StringComparison.Ordinal)) ||
                              (s.StartsWith("'", StringComparison.Ordinal) && s.EndsWith("'", StringComparison.Ordinal))))
            s = s.Substring(1, s.Length - 2);

        return s.Trim();
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

    public void AssignOfficer(string guildId, string userId, int slotValue)
    {
        _ = AssignOfficerAsync(guildId, userId, slotValue);
    }

    public void RevokeOfficer(string guildId, int slotValue)
    {
        _ = RevokeOfficerAsync(guildId, slotValue);
    }

    private async Task AssignOfficerAsync(string guildId, string userId, int slotValue)
    {
        try
        {
            if (_currentGuild == null || _currentGuild.GuildId != guildId)
            {
                GD.PushWarning($"[GuildManager] Guild {guildId} not found");
                return;
            }

            if (!TryGetCurrentUserId(out var assignedByUserId))
            {
                DebugWarn("AssignOfficer denied: PlayerSession.CurrentUserId missing");
                return;
            }

            if (!Enum.IsDefined(typeof(OfficerSlot), slotValue))
            {
                DebugWarn($"AssignOfficer denied: invalid slot value {slotValue}");
                return;
            }

            var ok = await _officers.AssignOfficerAsync(
                _currentGuild,
                (OfficerSlot)slotValue,
                userId: userId,
                assignedByUserId: assignedByUserId,
                assignedAt: DateTimeOffset.UtcNow);

            if (!ok)
            {
                GD.PushWarning($"[GuildManager] AssignOfficer denied for user {userId}");
                DebugWarn($"AssignOfficer denied_or_persist_failed targetUserId={userId} requestedByUserId={assignedByUserId}");
                return;
            }

            GD.Print($"[GuildManager] Assigned officer {userId} to slot {slotValue} in guild {guildId}");
        }
        catch (Exception ex)
        {
            DebugError("AssignOfficer failed", ex);
        }
    }

    private async Task RevokeOfficerAsync(string guildId, int slotValue)
    {
        try
        {
            if (_currentGuild == null || _currentGuild.GuildId != guildId)
            {
                GD.PushWarning($"[GuildManager] Guild {guildId} not found");
                return;
            }

            if (!TryGetCurrentUserId(out var revokedByUserId))
            {
                DebugWarn("RevokeOfficer denied: PlayerSession.CurrentUserId missing");
                return;
            }

            if (!Enum.IsDefined(typeof(OfficerSlot), slotValue))
            {
                DebugWarn($"RevokeOfficer denied: invalid slot value {slotValue}");
                return;
            }

            var ok = await _officers.RevokeOfficerAsync(
                _currentGuild,
                (OfficerSlot)slotValue,
                revokedByUserId: revokedByUserId,
                revokedAt: DateTimeOffset.UtcNow);

            if (!ok)
            {
                GD.PushWarning($"[GuildManager] RevokeOfficer denied for slot {slotValue}");
                DebugWarn($"RevokeOfficer denied_or_persist_failed slot={slotValue} requestedByUserId={revokedByUserId}");
                return;
            }

            GD.Print($"[GuildManager] Revoked officer slot {slotValue} in guild {guildId}");
        }
        catch (Exception ex)
        {
            DebugError("RevokeOfficer failed", ex);
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
