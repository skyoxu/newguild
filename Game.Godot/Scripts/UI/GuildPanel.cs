using Godot;
using Game.Godot.Adapters;
using System.Text.Json;

namespace Game.Godot.Scripts.UI;

/// <summary>
/// Guild management panel UI component.
/// Displays guild information and handles guild creation/management events.
/// Follows ADR-0018 (Godot UI layer) and ADR-0004 (event contracts).
/// </summary>
public partial class GuildPanel : Control
{
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        MaxDepth = 32,
    };

    private Label _guildNameLabel = default!;
    private Label _memberCountLabel = default!;
    private Button _createGuildButton = default!;
    private Button _disbandGuildButton = default!;
    private ItemList _membersList = default!;

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

        // Connect button signals
        _createGuildButton.Pressed += OnCreateGuildPressed;
        _disbandGuildButton.Pressed += OnDisbandGuildPressed;

        // Subscribe to domain events via EventBusAdapter
        _eventBus = GetNodeOrNull<EventBusAdapter>("/root/EventBus");
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
            case "core.guild.created":
                HandleGuildCreated(dataJson);
                break;
            case "core.guild.disbanded":
                HandleGuildDisbanded(dataJson);
                break;
            case "core.guild.member.joined":
                HandleMemberJoined(dataJson);
                break;
            case "core.guild.member.left":
                HandleMemberLeft(dataJson);
                break;
            case "core.guild.member.role.changed":
                HandleMemberRoleChanged(dataJson);
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
                string role = root.TryGetProperty("role", out var r) ? r.GetString() ?? "Member" : "Member";
                _membersList.AddItem($"{userId.GetString()} ({role})", null, true);
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
                string newRole = root.TryGetProperty("newRole", out var r) ? r.GetString() ?? "Member" : "Member";

                for (int i = 0; i < _membersList.ItemCount; i++)
                {
                    if (_membersList.GetItemText(i).StartsWith(userIdStr))
                    {
                        _membersList.SetItemText(i, $"{userIdStr} ({newRole})");
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

    private void OnCreateGuildPressed()
    {
        // Call GuildManager singleton
        var guildManager = GetNode("/root/GuildManager");
        string userId = "player1"; // TODO: Get from actual player/session
        string guildName = $"Guild_{System.Guid.NewGuid().ToString("N").Substring(0, 6)}";

        guildManager.Call("CreateGuild", userId, guildName);
    }

    private void OnDisbandGuildPressed()
    {
        if (_currentGuildId == null) return;

        // Call GuildManager singleton
        var guildManager = GetNode("/root/GuildManager");
        string userId = "player1"; // TODO: Get from actual player/session

        guildManager.Call("DisbandGuild", _currentGuildId, userId);
    }

    private void UpdateUIState(bool hasGuild)
    {
        _createGuildButton.Disabled = hasGuild;
        _disbandGuildButton.Disabled = !hasGuild;
    }

    [Signal]
    public delegate void CreateGuildRequestedEventHandler();

    [Signal]
    public delegate void DisbandGuildRequestedEventHandler();
}
