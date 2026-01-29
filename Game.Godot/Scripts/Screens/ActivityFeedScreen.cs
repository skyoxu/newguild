using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Godot;
using Game.Godot.Adapters;

namespace Game.Godot.Scripts.Screens;

public partial class ActivityFeedScreen : Control
{
    private const int MaxEntries = 200;

    private static readonly HashSet<string> AllowedEventTypes = new(StringComparer.Ordinal)
    {
        "core.ai.cycle.completed",
        "core.ai.cycle.started",
        "core.ai.ecosystem.step.completed",
        "core.ai.intent.issued",
        "core.content.manifest.loaded",
        "core.event_catalog.loaded",
        "core.game_turn.phase_changed",
        "core.game_turn.started",
        "core.game_turn.week_advanced",
        "core.guild.created",
        "core.guild.disbanded",
        "core.guild.member.joined",
        "core.guild.member.left",
        "core.guild.member.role_changed",
        "core.load.completed",
        "core.load.failed",
        "core.load.requested",
        "core.media.beat.triggered",
        "core.raid.resolved",
        "core.raid.scheduled",
        "core.recruitment.offer.presented",
        "core.recruitment.offer.resolved",
        "core.reputation.changed",
        "core.save.completed",
        "core.save.failed",
        "core.save.format.migration.applied",
        "core.save.requested",
        "core.social.interaction.triggered",
        "core.social.relationship.changed"
    };

    private Button? _back;
    private RichTextLabel? _feed;
    private Label? _status;
    private EventBusAdapter? _bus;
    private Callable _domainEventCallable;
    private readonly List<ActivityFeedEntry> _entries = new();

    private sealed record ActivityFeedEntry(
        string Id,
        string Kind,
        string Type,
        string Source,
        DateTimeOffset Timestamp,
        string DataPreview);

    public override void _Ready()
    {
        _back = GetNodeOrNull<Button>("Top/Back");
        _feed = GetNodeOrNull<RichTextLabel>("Body/Scroll/Feed");
        _status = GetNodeOrNull<Label>("Body/Status");

        if (_back != null)
            _back.Pressed += OnBack;

        _bus = GetNodeOrNull<EventBusAdapter>("/root/EventBus");
        if (_bus != null)
        {
            _domainEventCallable = new Callable(this, nameof(OnDomainEventEmitted));
            _bus.Connect(EventBusAdapter.SignalName.DomainEventEmitted, _domainEventCallable);
        }

        UpdateStatus();
        RefreshFeed();
    }

    public override void _ExitTree()
    {
        if (_bus != null)
        {
            if (_bus.IsConnected(EventBusAdapter.SignalName.DomainEventEmitted, _domainEventCallable))
                _bus.Disconnect(EventBusAdapter.SignalName.DomainEventEmitted, _domainEventCallable);
        }
    }

    public void Enter()
    {
        GD.Print("[ActivityFeedScreen] Enter");
    }

    public void Exit()
    {
        GD.Print("[ActivityFeedScreen] Exit");
    }

    private void OnBack()
    {
        var nav = GetNodeOrNull<Node>("/root/Main/ScreenNavigator");
        if (nav != null && nav.HasMethod("SwitchTo"))
            nav.Call("SwitchTo", "res://Game.Godot/Scenes/Screens/StartScreen.tscn");
    }

    private void OnDomainEventEmitted(string type, string source, string dataJson, string id, string specVersion, string dataContentType, string timestampIso)
    {
        if (!IsEventTracked(type))
            return;

        var ts = ParseTimestamp(timestampIso);
        var kind = ClassifyKind(type);
        var preview = BuildPreview(dataJson);

        AddEntry(new ActivityFeedEntry(id, kind, type, source, ts, preview));
    }

    private void AddEntry(ActivityFeedEntry entry)
    {
        _entries.Add(entry);
        _entries.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
        if (_entries.Count > MaxEntries)
            _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
        UpdateStatus();
        RefreshFeed();
    }

    private void UpdateStatus()
    {
        if (_status == null)
            return;

        _status.Text = _entries.Count == 0 ? "No activity yet." : $"Events: {_entries.Count}";
    }

    private void RefreshFeed()
    {
        if (_feed == null)
            return;

        _feed.Clear();
        if (_entries.Count == 0)
        {
            _feed.AppendText("Waiting for events...\n");
            return;
        }

        var sb = new StringBuilder();
        foreach (var entry in _entries)
        {
            sb.Append(entry.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            sb.Append(" [");
            sb.Append(entry.Kind);
            sb.Append("] ");
            sb.Append(entry.Type);
            if (!string.IsNullOrWhiteSpace(entry.Source))
            {
                sb.Append(" source=");
                sb.Append(entry.Source);
            }
            if (!string.IsNullOrWhiteSpace(entry.Id))
            {
                sb.Append(" id=");
                sb.Append(entry.Id);
            }
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(entry.DataPreview))
            {
                sb.Append("  ");
                sb.AppendLine(entry.DataPreview);
            }
        }
        _feed.AppendText(sb.ToString());
    }

    private static DateTimeOffset ParseTimestamp(string timestampIso)
    {
        if (DateTimeOffset.TryParse(timestampIso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var ts))
            return ts;
        return DateTimeOffset.UtcNow;
    }

    private static string BuildPreview(string dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
            return string.Empty;
        var trimmed = dataJson.Trim();
        if (trimmed.Length > 180)
            trimmed = trimmed[..180] + "...";
        return trimmed.Replace("\n", " ").Replace("\r", " ");
    }

    private static bool IsEventTracked(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return false;
        if (AllowedEventTypes.Contains(type))
            return true;

        return type.StartsWith("core.raid.", StringComparison.Ordinal) ||
               type.StartsWith("core.media.", StringComparison.Ordinal) ||
               type.StartsWith("core.social.", StringComparison.Ordinal) ||
               type.StartsWith("core.reputation.", StringComparison.Ordinal) ||
               type.StartsWith("core.recruitment.", StringComparison.Ordinal) ||
               type.StartsWith("core.guild.", StringComparison.Ordinal) ||
               type.StartsWith("core.game_turn.", StringComparison.Ordinal) ||
               type.StartsWith("core.ai.", StringComparison.Ordinal) ||
               type.StartsWith("core.save.", StringComparison.Ordinal) ||
               type.StartsWith("core.load.", StringComparison.Ordinal) ||
               type.StartsWith("core.content.", StringComparison.Ordinal) ||
               type.StartsWith("core.event_catalog.", StringComparison.Ordinal);
    }

    private static string ClassifyKind(string type)
    {
        if (type.StartsWith("core.raid.", StringComparison.Ordinal))
            return "raid";
        if (type.StartsWith("core.media.", StringComparison.Ordinal))
            return "media";
        if (type.StartsWith("core.social.", StringComparison.Ordinal))
            return "social";
        if (type.StartsWith("core.reputation.", StringComparison.Ordinal))
            return "reputation";
        if (type.StartsWith("core.recruitment.", StringComparison.Ordinal))
            return "recruitment";
        if (type.StartsWith("core.guild.", StringComparison.Ordinal))
            return "guild";
        if (type.StartsWith("core.game_turn.", StringComparison.Ordinal))
            return "turn";
        if (type.StartsWith("core.ai.", StringComparison.Ordinal))
            return "ai";
        if (type.StartsWith("core.save.", StringComparison.Ordinal) || type.StartsWith("core.load.", StringComparison.Ordinal))
            return "persistence";
        if (type.StartsWith("core.content.", StringComparison.Ordinal) || type.StartsWith("core.event_catalog.", StringComparison.Ordinal))
            return "content";
        if (type.Contains("reward", StringComparison.OrdinalIgnoreCase) || type.Contains("score", StringComparison.OrdinalIgnoreCase))
            return "reward";

        return "system";
    }
}
