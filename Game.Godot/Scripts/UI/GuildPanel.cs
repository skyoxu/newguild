using Godot;
using Game.Core.Contracts.Guild;
using Game.Core.Contracts.Recruitment;
using Game.Godot.Adapters;
using Game.Godot.Scripts.Autoload;
using System;
using System.Text.Json;

namespace Game.Godot.Scripts.UI;

/// <summary>
/// Guild management panel UI component.
/// Displays guild information and handles guild creation/management events.
/// Follows ADR-0018 (Godot UI layer) and ADR-0004 (event contracts).
/// </summary>
public partial class GuildPanel : Control
{
    [Export]
    public NodePath GuildManagerPath { get; set; } = new NodePath("/root/GuildManager");

    [Export]
    public NodePath EventBusPath { get; set; } = new NodePath("/root/EventBus");

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        MaxDepth = 32,
    };

    private Label _guildNameLabel = default!;
    private Label _memberCountLabel = default!;
    private Button _createGuildButton = default!;
    private Button _disbandGuildButton = default!;
    private ItemList _membersList = default!;
    private LineEdit _userIdInput = default!;
    private Button _joinButton = default!;
    private Button _leaveButton = default!;
    private Button _promoteButton = default!;
    private Button _demoteButton = default!;
    private Button _kickButton = default!;
    private LineEdit _candidateIdInput = default!;
    private LineEdit _offerIdInput = default!;
    private ItemList _offersList = default!;
    private Button _applyButton = default!;
    private Button _approveButton = default!;
    private Button _rejectButton = default!;

    private string? _currentGuildId;
    private EventBusAdapter? _eventBus;
    private Callable _domainEventCallable;

    public override void _Ready()
    {
        // Get node references
        _guildNameLabel = GetNode<Label>("VBox/GuildInfo/GuildNameLabel");
        _memberCountLabel = GetNode<Label>("VBox/GuildInfo/MemberCountLabel");
        _createGuildButton = GetNode<Button>("VBox/Actions/CreateGuildButton");
        _disbandGuildButton = GetNode<Button>("VBox/Actions/DisbandGuildButton");
        _membersList = GetNode<ItemList>("VBox/MembersList");
        _userIdInput = GetNode<LineEdit>("VBox/RosterActions/UserIdRow/UserIdInput");
        _joinButton = GetNode<Button>("VBox/RosterActions/MemberActionsRow/JoinButton");
        _leaveButton = GetNode<Button>("VBox/RosterActions/MemberActionsRow/LeaveButton");
        _promoteButton = GetNode<Button>("VBox/RosterActions/MemberActionsRow/PromoteButton");
        _demoteButton = GetNode<Button>("VBox/RosterActions/MemberActionsRow/DemoteButton");
        _kickButton = GetNode<Button>("VBox/RosterActions/MemberActionsRow/KickButton");
        _candidateIdInput = GetNode<LineEdit>("VBox/RecruitmentSection/CandidateIdRow/CandidateIdInput");
        _offerIdInput = GetNode<LineEdit>("VBox/RecruitmentSection/OfferIdRow/OfferIdInput");
        _offersList = GetNode<ItemList>("VBox/RecruitmentSection/OffersList");
        _applyButton = GetNode<Button>("VBox/RecruitmentSection/RecruitmentActionsRow/ApplyButton");
        _approveButton = GetNode<Button>("VBox/RecruitmentSection/RecruitmentActionsRow/ApproveButton");
        _rejectButton = GetNode<Button>("VBox/RecruitmentSection/RecruitmentActionsRow/RejectButton");

        // Connect button signals
        _createGuildButton.Pressed += OnCreateGuildPressed;
        _disbandGuildButton.Pressed += OnDisbandGuildPressed;
        _joinButton.Pressed += OnJoinPressed;
        _leaveButton.Pressed += OnLeavePressed;
        _promoteButton.Pressed += OnPromotePressed;
        _demoteButton.Pressed += OnDemotePressed;
        _kickButton.Pressed += OnKickPressed;
        _applyButton.Pressed += OnApplyPressed;
        _approveButton.Pressed += OnApprovePressed;
        _rejectButton.Pressed += OnRejectPressed;

        // Subscribe to domain events via EventBusAdapter
        _eventBus = GetNodeOrNull<EventBusAdapter>(EventBusPath);
        if (_eventBus != null)
        {
            _domainEventCallable = new Callable(this, nameof(OnDomainEventEmitted));
            _eventBus.Connect(EventBusAdapter.SignalName.DomainEventEmitted, _domainEventCallable);
        }

        // Initial UI state
        UpdateUIState(hasGuild: false);
    }

    public override void _ExitTree()
    {
        if (_eventBus == null)
            return;
        if (_eventBus.IsConnected(EventBusAdapter.SignalName.DomainEventEmitted, _domainEventCallable))
            _eventBus.Disconnect(EventBusAdapter.SignalName.DomainEventEmitted, _domainEventCallable);
    }

    private void OnDomainEventEmitted(string type, string source, string dataJson, string id, string specVersion, string dataContentType, string timestampIso)
    {
        switch (type)
        {
            case GuildCreated.EventType:
                HandleGuildCreated(dataJson);
                break;
            case GuildDisbanded.EventType:
                HandleGuildDisbanded(dataJson);
                break;
            case GuildMemberJoined.EventType:
                HandleMemberJoined(dataJson);
                break;
            case GuildMemberLeft.EventType:
                HandleMemberLeft(dataJson);
                break;
            case GuildMemberRoleChanged.EventType:
                HandleMemberRoleChanged(dataJson);
                break;
            case RecruitmentOfferPresented.EventType:
                HandleRecruitmentOfferPresented(dataJson);
                break;
            case RecruitmentOfferResolved.EventType:
                HandleRecruitmentOfferResolved(dataJson);
                break;
        }
    }

    private void HandleGuildCreated(string dataJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(dataJson, JsonOptions);
            var root = doc.RootElement;

            if (root.TryGetProperty("guildId", out var guildId))
            {
                _currentGuildId = guildId.GetString();
            }

            string guildName = root.TryGetProperty("guildName", out var name) ? name.GetString() ?? "Unknown" : "Unknown";

            _guildNameLabel.Text = $"Guild: {guildName}";
            UpdateUIState(hasGuild: true);

            // Add creator as first member
            if (root.TryGetProperty("creatorId", out var creatorId))
            {
                _membersList.Clear();
                _membersList.AddItem($"{creatorId.GetString()} (Admin)", null, true);
                _memberCountLabel.Text = "Members: 1";
            }
        }
        catch
        {
            // Ignore malformed events
        }
    }

    private void HandleGuildDisbanded(string dataJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(dataJson, JsonOptions);
            if (doc.RootElement.TryGetProperty("guildId", out var guildId) &&
                guildId.GetString() == _currentGuildId)
            {
                _currentGuildId = null;
                _guildNameLabel.Text = "Guild: None";
                _membersList.Clear();
                _memberCountLabel.Text = "Members: 0";
                UpdateUIState(hasGuild: false);
            }
        }
        catch
        {
            // Ignore malformed events
        }
    }

    private void HandleMemberJoined(string dataJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(dataJson, JsonOptions);
            var root = doc.RootElement;

            if (root.TryGetProperty("guildId", out var guildId) &&
                guildId.GetString() == _currentGuildId &&
                root.TryGetProperty("userId", out var userId))
            {
                string role = root.TryGetProperty("role", out var r) ? r.GetString() ?? "member" : "member";
                _membersList.AddItem($"{userId.GetString()} ({FormatRoleDisplay(role)})", null, true);
                _memberCountLabel.Text = $"Members: {_membersList.ItemCount}";
            }
        }
        catch
        {
            // Ignore malformed events
        }
    }

    private void HandleMemberLeft(string dataJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(dataJson, JsonOptions);
            var root = doc.RootElement;

            if (root.TryGetProperty("guildId", out var guildId) &&
                guildId.GetString() == _currentGuildId &&
                root.TryGetProperty("userId", out var userId))
            {
                string userIdStr = userId.GetString() ?? "";
                for (int i = 0; i < _membersList.ItemCount; i++)
                {
                    if (_membersList.GetItemText(i).StartsWith(userIdStr))
                    {
                        _membersList.RemoveItem(i);
                        _memberCountLabel.Text = $"Members: {_membersList.ItemCount}";
                        break;
                    }
                }
            }
        }
        catch
        {
            // Ignore malformed events
        }
    }

    private void HandleMemberRoleChanged(string dataJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(dataJson, JsonOptions);
            var root = doc.RootElement;

            if (root.TryGetProperty("guildId", out var guildId) &&
                guildId.GetString() == _currentGuildId &&
                root.TryGetProperty("userId", out var userId))
            {
                string userIdStr = userId.GetString() ?? "";
                string newRole = root.TryGetProperty("newRole", out var r) ? r.GetString() ?? "member" : "member";

                for (int i = 0; i < _membersList.ItemCount; i++)
                {
                    if (_membersList.GetItemText(i).StartsWith(userIdStr))
                    {
                        _membersList.SetItemText(i, $"{userIdStr} ({FormatRoleDisplay(newRole)})");
                        break;
                    }
                }
            }
        }
        catch
        {
            // Ignore malformed events
        }
    }

    private void HandleRecruitmentOfferPresented(string dataJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(dataJson, JsonOptions);
            var root = doc.RootElement;

            if (!root.TryGetProperty("guildId", out var guildId) || guildId.GetString() != _currentGuildId)
                return;

            var offerId = root.TryGetProperty("offerId", out var o) ? o.GetString() ?? "" : "";
            var candidateId = root.TryGetProperty("candidateId", out var c) ? c.GetString() ?? "" : "";
            var role = root.TryGetProperty("role", out var r) ? r.GetString() ?? "member" : "member";

            if (string.IsNullOrWhiteSpace(offerId))
                return;

            _offersList.AddItem($"{offerId} | {candidateId} | {role}", null, true);
        }
        catch
        {
            // Ignore malformed events
        }
    }

    private void HandleRecruitmentOfferResolved(string dataJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(dataJson, JsonOptions);
            var root = doc.RootElement;

            if (!root.TryGetProperty("guildId", out var guildId) || guildId.GetString() != _currentGuildId)
                return;

            var offerId = root.TryGetProperty("offerId", out var o) ? o.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(offerId))
                return;

            for (int i = 0; i < _offersList.ItemCount; i++)
            {
                if (_offersList.GetItemText(i).StartsWith(offerId, StringComparison.Ordinal))
                {
                    _offersList.RemoveItem(i);
                    break;
                }
            }
        }
        catch
        {
            // Ignore malformed events
        }
    }

    private void OnCreateGuildPressed()
    {
        var guildManager = GetNode(GuildManagerPath);
        var session = GetNodeOrNull<PlayerSession>("/root/PlayerSession");
        string userId = session?.CurrentUserId ?? "player1";
        string guildName = $"Guild_{System.Guid.NewGuid().ToString("N").Substring(0, 6)}";

        guildManager.Call("CreateGuild", userId, guildName);
    }

    private void OnDisbandGuildPressed()
    {
        if (_currentGuildId == null) return;

        var guildManager = GetNode(GuildManagerPath);
        var session = GetNodeOrNull<PlayerSession>("/root/PlayerSession");
        string userId = session?.CurrentUserId ?? "player1";

        guildManager.Call("DisbandGuild", _currentGuildId, userId);
    }

    private void OnJoinPressed()
    {
        if (_currentGuildId == null) return;
        var userId = GetTargetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return;

        var guildManager = GetNode(GuildManagerPath);
        guildManager.Call("AddMember", _currentGuildId, userId);
    }

    private void OnLeavePressed()
    {
        if (_currentGuildId == null) return;
        var guildManager = GetNode(GuildManagerPath);
        guildManager.Call("LeaveCurrentUser", _currentGuildId);
    }

    private void OnPromotePressed()
    {
        if (_currentGuildId == null) return;
        var userId = GetTargetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return;

        var guildManager = GetNode(GuildManagerPath);
        guildManager.Call("PromoteMember", _currentGuildId, userId);
    }

    private void OnDemotePressed()
    {
        if (_currentGuildId == null) return;
        var userId = GetTargetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return;

        var guildManager = GetNode(GuildManagerPath);
        guildManager.Call("DemoteMember", _currentGuildId, userId);
    }

    private void OnKickPressed()
    {
        if (_currentGuildId == null) return;
        var userId = GetTargetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return;

        var guildManager = GetNode(GuildManagerPath);
        guildManager.Call("RemoveMember", _currentGuildId, userId);
    }

    private void OnApplyPressed()
    {
        if (_currentGuildId == null) return;
        var candidateId = _candidateIdInput.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(candidateId)) return;

        var guildManager = GetNode(GuildManagerPath);
        guildManager.Call("ApplyForGuild", _currentGuildId, candidateId, "member");
    }

    private void OnApprovePressed()
    {
        if (_currentGuildId == null) return;
        if (!TryGetOfferId(out var offerId)) return;

        var guildManager = GetNode(GuildManagerPath);
        guildManager.Call("ApproveOffer", _currentGuildId, offerId);
    }

    private void OnRejectPressed()
    {
        if (_currentGuildId == null) return;
        if (!TryGetOfferId(out var offerId)) return;

        var guildManager = GetNode(GuildManagerPath);
        guildManager.Call("RejectOffer", _currentGuildId, offerId, "rejected");
    }

    private bool TryGetOfferId(out string offerId)
    {
        offerId = _offerIdInput.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(offerId))
            return true;

        var selected = _offersList.GetSelectedItems();
        if (selected.Length == 0)
            return false;

        var text = _offersList.GetItemText(selected[0]);
        var idx = text.IndexOf(" | ", StringComparison.Ordinal);
        offerId = (idx > 0 ? text[..idx] : text).Trim();
        return !string.IsNullOrWhiteSpace(offerId);
    }

    private string GetTargetUserId()
    {
        var raw = _userIdInput.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(raw))
            return raw;

        var selected = _membersList.GetSelectedItems();
        if (selected.Length == 0)
            return string.Empty;

        var text = _membersList.GetItemText(selected[0]);
        var idx = text.IndexOf(" (", StringComparison.Ordinal);
        return idx > 0 ? text[..idx].Trim() : text.Trim();
    }

    private static string FormatRoleDisplay(string role) =>
        role.Trim().ToLowerInvariant() switch
        {
            "admin" => "Admin",
            "member" => "Member",
            _ => role,
        };

    private void UpdateUIState(bool hasGuild)
    {
        _createGuildButton.Disabled = hasGuild;
        _disbandGuildButton.Disabled = !hasGuild;
        _joinButton.Disabled = !hasGuild;
        _leaveButton.Disabled = !hasGuild;
        _promoteButton.Disabled = !hasGuild;
        _demoteButton.Disabled = !hasGuild;
        _kickButton.Disabled = !hasGuild;
        _applyButton.Disabled = !hasGuild;
        _approveButton.Disabled = !hasGuild;
        _rejectButton.Disabled = !hasGuild;
    }

    [Signal]
    public delegate void CreateGuildRequestedEventHandler();

    [Signal]
    public delegate void DisbandGuildRequestedEventHandler();
}
