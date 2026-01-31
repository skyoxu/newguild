using Godot;
using Game.Core.Contracts.Guild;
using Game.Core.Contracts.Recruitment;
using Game.Godot.Adapters;
using Game.Godot.Scripts.Autoload;
using Game.Godot.Scripts.UI.Components;
using System;
using System.Text.Json;
using System.Threading.Tasks;

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

    private LineEdit? _guildNameInput;
    private Label _memberCountLabel = default!;
    private StatusPanel? _statusPanel;
    private ErrorPanel? _errorPanel;
    private ConfirmDialogPanel? _confirmDialog;
    private Button _createGuildButton = default!;
    private Button _disbandGuildButton = default!;
    private ListPanel? _membersListPanel;
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
    private bool _confirmDialogWired;

    private const string InteractionStateReady = "ready";
    private const string InteractionStateLoading = "loading";
    private const string InteractionStateError = "error";

    private Script? _interactionStateUiScript;
    private Control? _interactionRoot;
    private Label? _interactionStatusLabel;
    private Button? _interactionRetryButton;
    private Button? _interactionCloseButton;
    private Func<Task>? _retryAsync;

    private void SetGuildStatus(string message)
    {
        SetInteractionState(InteractionStateReady, message, retryAsync: null);
        GD.Print($"[GuildPanel] {message}");
    }

    private void ShowError(string title, string message)
    {
        SetInteractionState(InteractionStateError, message, retryAsync: null, errorTitle: title);
    }

    private void SetInteractionState(string state, string message, Func<Task>? retryAsync, string errorTitle = "Error")
    {
        _retryAsync = retryAsync;

        _interactionStateUiScript ??= GD.Load<Script>("res://Game.Godot/Scripts/UI/InteractionStateUi.gd");
        _interactionRoot ??= GetNodeOrNull<Control>("Scroll/Margin/VBox");
        _interactionStatusLabel ??= GetNodeOrNull<Label>("Scroll/Margin/VBox/GuildInfo/StatusPanel/Root/Message");
        _interactionRetryButton ??= GetNodeOrNull<Button>("Scroll/Margin/VBox/GuildInfo/ErrorPanel/Root/Buttons/RetryButton");
        _interactionCloseButton ??= GetNodeOrNull<Button>("Scroll/Margin/VBox/GuildInfo/ErrorPanel/Root/Buttons/CloseButton");

        if (_errorPanel != null)
        {
            _errorPanel.Visible = state == InteractionStateError;
            if (_errorPanel.Visible)
                _errorPanel.SetError(errorTitle, message);
        }

        if (_interactionStateUiScript == null || _interactionRoot == null || _interactionStatusLabel == null || _interactionRetryButton == null)
            return;

        var exceptions = new global::Godot.Collections.Array<global::Godot.Node> { _interactionRetryButton };
        if (_interactionCloseButton != null)
            exceptions.Add(_interactionCloseButton);

        _interactionStateUiScript.Call(
            "apply_state_with_exceptions",
            _interactionRoot,
            _interactionStatusLabel,
            _interactionRetryButton,
            exceptions,
            state,
            message
        );

        if (state == InteractionStateError && _retryAsync == null)
        {
            _interactionRetryButton.Visible = false;
        }
    }

    private async void OnErrorRetryRequested()
    {
        if (_retryAsync == null)
        {
            SetInteractionState(InteractionStateReady, string.Empty, retryAsync: null);
            return;
        }

        try
        {
            await _retryAsync();
        }
        catch (Exception ex)
        {
            ShowError("Retry failed", ex.Message);
        }
    }

    private void OnErrorCloseRequested()
    {
        SetInteractionState(InteractionStateReady, string.Empty, retryAsync: null);
    }

    private Node? GetGuildManagerOrReport()
    {
        var gm = GetNodeOrNull(GuildManagerPath);
        if (gm == null)
        {
            SetGuildStatus("ERROR: GuildManager not found.");
            GD.PushWarning("[GuildPanel] GuildManager not found. Check autoload /root/GuildManager.");
            return null;
        }
        return gm;
    }

    public override void _Ready()
    {
        // Get node references
        _guildNameInput = GetNodeOrNull<LineEdit>("Scroll/Margin/VBox/GuildInfo/GuildNameRow/GuildNameInput");
        _memberCountLabel = GetNode<Label>("Scroll/Margin/VBox/GuildInfo/MemberCountLabel");
        _statusPanel = GetNodeOrNull<StatusPanel>("Scroll/Margin/VBox/GuildInfo/StatusPanel");
        _errorPanel = GetNodeOrNull<ErrorPanel>("Scroll/Margin/VBox/GuildInfo/ErrorPanel");
        _confirmDialog = GetNodeOrNull<ConfirmDialogPanel>("Scroll/Margin/VBox/GuildInfo/ConfirmDisbandDialog");
        _createGuildButton = GetNode<Button>("Scroll/Margin/VBox/Actions/CreateGuildButton");
        _disbandGuildButton = GetNode<Button>("Scroll/Margin/VBox/Actions/DisbandGuildButton");
        _membersListPanel = GetNodeOrNull<ListPanel>("Scroll/Margin/VBox/MembersListPanel");
        _membersList = GetNode<ItemList>("Scroll/Margin/VBox/MembersListPanel/Root/Items");
        _userIdInput = GetNode<LineEdit>("Scroll/Margin/VBox/RosterActions/UserIdRow/UserIdInput");
        _joinButton = GetNode<Button>("Scroll/Margin/VBox/RosterActions/MemberActionsRow/JoinButton");
        _leaveButton = GetNode<Button>("Scroll/Margin/VBox/RosterActions/MemberActionsRow/LeaveButton");
        _promoteButton = GetNode<Button>("Scroll/Margin/VBox/RosterActions/MemberActionsRow/PromoteButton");
        _demoteButton = GetNode<Button>("Scroll/Margin/VBox/RosterActions/MemberActionsRow/DemoteButton");
        _kickButton = GetNode<Button>("Scroll/Margin/VBox/RosterActions/MemberActionsRow/KickButton");
        _candidateIdInput = GetNode<LineEdit>("Scroll/Margin/VBox/RecruitmentSection/CandidateIdRow/CandidateIdInput");
        _offerIdInput = GetNode<LineEdit>("Scroll/Margin/VBox/RecruitmentSection/OfferIdRow/OfferIdInput");
        _offersList = GetNode<ItemList>("Scroll/Margin/VBox/RecruitmentSection/OffersList");
        _applyButton = GetNode<Button>("Scroll/Margin/VBox/RecruitmentSection/RecruitmentActionsRow/ApplyButton");
        _approveButton = GetNode<Button>("Scroll/Margin/VBox/RecruitmentSection/RecruitmentActionsRow/ApproveButton");
        _rejectButton = GetNode<Button>("Scroll/Margin/VBox/RecruitmentSection/RecruitmentActionsRow/RejectButton");

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

        if (_statusPanel != null)
            _statusPanel.SetStatus("Guild", string.Empty);

        if (_membersListPanel != null)
            _membersListPanel.SetTitle("Guild Members");

        if (_errorPanel != null)
        {
            _errorPanel.Visible = false;
            _errorPanel.CloseRequested += OnErrorCloseRequested;
            _errorPanel.RetryRequested += OnErrorRetryRequested;
        }

        if (_confirmDialog != null)
        {
            _confirmDialog.Visible = false;
            WireConfirmDialog();
        }

        // Subscribe to domain events via EventBusAdapter
        _eventBus = GetNodeOrNull<EventBusAdapter>(EventBusPath);
        if (_eventBus == null)
            _eventBus = GetNodeOrNull<EventBusAdapter>("/root/EventBus");
        if (_eventBus != null)
        {
            _domainEventCallable = new Callable(this, nameof(OnDomainEventEmitted));
            _eventBus.Connect(EventBusAdapter.SignalName.DomainEventEmitted, _domainEventCallable);
        }
        else
        {
            SetGuildStatus("ERROR: EventBus not found.");
            GD.PushWarning("[GuildPanel] EventBus not found. Check autoload /root/EventBus.");
        }

        // Initial UI state
        UpdateUIState(hasGuild: false);
        SetInteractionState(InteractionStateReady, string.Empty, retryAsync: null);
    }

    private void WireConfirmDialog()
    {
        if (_confirmDialog == null || _confirmDialogWired)
            return;

        _confirmDialog.Confirmed += () =>
        {
            _confirmDialog.Visible = false;
            PerformDisband();
        };

        _confirmDialog.Cancelled += () => _confirmDialog.Visible = false;
        _confirmDialogWired = true;
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
            case GuildOfficerAssigned.EventType:
                HandleOfficerAssigned(dataJson);
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

            if (_guildNameInput != null)
                _guildNameInput.Text = guildName;
            UpdateUIState(hasGuild: true);
            SetGuildStatus($"Created: {guildName}");

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
                if (_guildNameInput != null)
                    _guildNameInput.Text = string.Empty;
                _membersList.Clear();
                _memberCountLabel.Text = "Members: 0";
                UpdateUIState(hasGuild: false);
                SetGuildStatus("Disbanded.");
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
                string role = root.TryGetProperty("role", out var roleElement) ? roleElement.GetString() ?? "member" : "member";
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
                string newRole = root.TryGetProperty("newRole", out var roleElement) ? roleElement.GetString() ?? "member" : "member";

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

    private void HandleOfficerAssigned(string dataJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(dataJson, JsonOptions);
            var root = doc.RootElement;

            if (root.TryGetProperty("guildId", out var guildId) &&
                guildId.GetString() == _currentGuildId &&
                root.TryGetProperty("userId", out var userId) &&
                root.TryGetProperty("slot", out var slot))
            {
                var userIdStr = userId.GetString() ?? string.Empty;
                var slotStr = slot.GetString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(userIdStr) && !string.IsNullOrWhiteSpace(slotStr))
                {
                    SetGuildStatus($"Officer assigned: {userIdStr} ({slotStr})");
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

            var offerId = root.TryGetProperty("offerId", out var offerIdElement) ? offerIdElement.GetString() ?? "" : "";
            var candidateId = root.TryGetProperty("candidateId", out var candidateIdElement) ? candidateIdElement.GetString() ?? "" : "";
            var role = root.TryGetProperty("role", out var roleElement) ? roleElement.GetString() ?? "member" : "member";

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

            var offerId = root.TryGetProperty("offerId", out var offerIdElement) ? offerIdElement.GetString() ?? "" : "";
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

    private async void OnCreateGuildPressed()
    {
        await CreateGuildAsync();
    }

    private async Task CreateGuildAsync()
    {
        var guildManagerNode = GetGuildManagerOrReport();
        if (guildManagerNode == null)
            return;
        if (guildManagerNode is not GuildManager guildManager)
        {
            SetGuildStatus("ERROR: GuildManager type mismatch.");
            return;
        }

        _guildNameInput ??= GetNodeOrNull<LineEdit>("Scroll/Margin/VBox/GuildInfo/GuildNameRow/GuildNameInput");
        if (_guildNameInput == null)
        {
            SetGuildStatus("ERROR: GuildNameInput missing.");
            return;
        }

        try
        {
            if (guildManager.HasCurrentGuild())
            {
                SetGuildStatus("Already has a guild. Syncing UI...");
                var json = guildManager.GetCurrentGuildSummaryJson();
                if (!string.IsNullOrWhiteSpace(json))
                    HandleGuildCreated(json);
                return;
            }
        }
        catch
        {
            // Best-effort only.
        }

        var session = GetNodeOrNull<PlayerSession>("/root/PlayerSession");
        string userId = session?.CurrentUserId ?? "player1";
        var inputName = _guildNameInput.Text?.Trim() ?? string.Empty;
        string guildName = string.IsNullOrWhiteSpace(inputName)
            ? $"Guild_{Guid.NewGuid().ToString("N").Substring(0, 6)}"
            : inputName;
        if (string.IsNullOrWhiteSpace(inputName))
            _guildNameInput.Text = guildName;

        SetInteractionState(InteractionStateLoading, "Creating...", retryAsync: CreateGuildAsync);
        try
        {
            var result = await guildManager.CreateGuildAsync(userId, guildName);
            if (!string.IsNullOrWhiteSpace(result))
            {
                if (result.StartsWith("ERROR:", StringComparison.Ordinal))
                {
                    var details = string.Empty;
                    try
                    {
                        details = guildManager.GetLastError();
                    }
                    catch
                    {
                        // Best-effort only.
                    }

                    var message = string.IsNullOrWhiteSpace(details) ? result : $"{result} ({details})";
                    SetInteractionState(InteractionStateError, message, retryAsync: CreateGuildAsync, errorTitle: "CreateGuild failed");
                    return;
                }
            }

            try
            {
                var json = guildManager.GetCurrentGuildSummaryJson();
                if (!string.IsNullOrWhiteSpace(json) && json != "{}")
                    HandleGuildCreated(json);
            }
            catch
            {
                // Best-effort only.
            }
        }
        catch (Exception ex)
        {
            SetInteractionState(InteractionStateError, ex.Message, retryAsync: CreateGuildAsync, errorTitle: "CreateGuild failed");
        }
    }

    private void OnDisbandGuildPressed()
    {
        if (_currentGuildId == null)
            return;

        if (_confirmDialog != null)
        {
            _confirmDialog.SetPrompt("Disband Guild", "Are you sure?");
            _confirmDialog.Visible = true;
            return;
        }

        PerformDisband();
    }

    private void PerformDisband()
    {
        if (_currentGuildId == null)
            return;

        var guildManager = GetGuildManagerOrReport();
        if (guildManager == null)
            return;
        var session = GetNodeOrNull<PlayerSession>("/root/PlayerSession");
        string userId = session?.CurrentUserId ?? "player1";

        if (!guildManager.HasMethod("DisbandGuild"))
        {
            ShowError("Guild", "ERROR: GuildManager.DisbandGuild missing.");
            return;
        }

        guildManager.Call("DisbandGuild", _currentGuildId, userId);
    }

    private void OnJoinPressed()
    {
        if (_currentGuildId == null) return;
        var userId = GetTargetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return;

        var guildManager = GetGuildManagerOrReport();
        if (guildManager == null)
            return;
        if (!guildManager.HasMethod("AddMember"))
        {
            SetGuildStatus("ERROR: GuildManager.AddMember missing.");
            return;
        }
        guildManager.Call("AddMember", _currentGuildId, userId);
    }

    private void OnLeavePressed()
    {
        if (_currentGuildId == null) return;
        var guildManager = GetGuildManagerOrReport();
        if (guildManager == null)
            return;
        if (!guildManager.HasMethod("LeaveCurrentUser"))
        {
            SetGuildStatus("ERROR: GuildManager.LeaveCurrentUser missing.");
            return;
        }
        guildManager.Call("LeaveCurrentUser", _currentGuildId);
    }

    private void OnPromotePressed()
    {
        if (_currentGuildId == null) return;
        var userId = GetTargetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return;

        var guildManager = GetGuildManagerOrReport();
        if (guildManager == null)
            return;
        if (!guildManager.HasMethod("PromoteMember"))
        {
            SetGuildStatus("ERROR: GuildManager.PromoteMember missing.");
            return;
        }
        guildManager.Call("PromoteMember", _currentGuildId, userId);
    }

    private void OnDemotePressed()
    {
        if (_currentGuildId == null) return;
        var userId = GetTargetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return;

        var guildManager = GetGuildManagerOrReport();
        if (guildManager == null)
            return;
        if (!guildManager.HasMethod("DemoteMember"))
        {
            SetGuildStatus("ERROR: GuildManager.DemoteMember missing.");
            return;
        }
        guildManager.Call("DemoteMember", _currentGuildId, userId);
    }

    private void OnKickPressed()
    {
        if (_currentGuildId == null) return;
        var userId = GetTargetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return;

        var guildManager = GetGuildManagerOrReport();
        if (guildManager == null)
            return;
        if (!guildManager.HasMethod("RemoveMember"))
        {
            SetGuildStatus("ERROR: GuildManager.RemoveMember missing.");
            return;
        }
        guildManager.Call("RemoveMember", _currentGuildId, userId);
    }

    private void OnApplyPressed()
    {
        if (_currentGuildId == null) return;
        var candidateId = _candidateIdInput.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(candidateId)) return;

        var guildManager = GetGuildManagerOrReport();
        if (guildManager == null)
            return;
        if (!guildManager.HasMethod("ApplyForGuild"))
        {
            SetGuildStatus("ERROR: GuildManager.ApplyForGuild missing.");
            return;
        }
        guildManager.Call("ApplyForGuild", _currentGuildId, candidateId, "member");
    }

    private void OnApprovePressed()
    {
        if (_currentGuildId == null) return;
        if (!TryGetOfferId(out var offerId)) return;

        var guildManager = GetGuildManagerOrReport();
        if (guildManager == null)
            return;
        if (!guildManager.HasMethod("ApproveOffer"))
        {
            SetGuildStatus("ERROR: GuildManager.ApproveOffer missing.");
            return;
        }
        guildManager.Call("ApproveOffer", _currentGuildId, offerId);
    }

    private void OnRejectPressed()
    {
        if (_currentGuildId == null) return;
        if (!TryGetOfferId(out var offerId)) return;

        var guildManager = GetGuildManagerOrReport();
        if (guildManager == null)
            return;
        if (!guildManager.HasMethod("RejectOffer"))
        {
            SetGuildStatus("ERROR: GuildManager.RejectOffer missing.");
            return;
        }
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
        if (_guildNameInput != null)
            _guildNameInput.Editable = !hasGuild;
    }

    [Signal]
    public delegate void CreateGuildRequestedEventHandler();

    [Signal]
    public delegate void DisbandGuildRequestedEventHandler();
}
