using System;
using System.Threading.Tasks;
using Godot;
using System.Text.Json;
using Game.Godot.Adapters;
using Game.Godot.Autoloads;
using Game.Core.Domain.Turn;
using Game.Core.Engine;
using Game.Core.Services;
using Game.Core.Ports;
using Game.Core.Ports.AI;

namespace Game.Godot.Scripts.UI;

public partial class HUD : Control
{
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        MaxDepth = 32,
    };

    private Label _score = default!;
    private Label _health = default!;
    private Label _week = default!;
    private Label _phase = default!;
    private Button _nextTurnButton = default!;

    private EventBusAdapter? _eventBus;
    private Callable _domainEventCallable;

    private IGameTurnSystem? _turnSystem;
    private GameTurnState? _currentTurn;

    public override void _Ready()
    {
        _score = GetNode<Label>("TopBar/HBox/ScoreLabel");
        _health = GetNode<Label>("TopBar/HBox/HealthLabel");
        _week = GetNodeOrNull<Label>("TopBar/HBox/WeekLabel");
        _phase = GetNodeOrNull<Label>("TopBar/HBox/PhaseLabel");
        _nextTurnButton = GetNodeOrNull<Button>("TopBar/HBox/NextTurnButton");

        if (_week != null)
            _week.Text = "Week: -";
        if (_phase != null)
            _phase.Text = "Phase: -";
        if (_nextTurnButton != null)
            _nextTurnButton.Pressed += OnNextTurnPressed;

        _eventBus = GetNodeOrNull<EventBusAdapter>("/root/EventBus");
        if (_eventBus != null)
        {
            _domainEventCallable = new Callable(this, nameof(OnDomainEventEmitted));
            _eventBus.Connect(EventBusAdapter.SignalName.DomainEventEmitted, _domainEventCallable);
        }

        // Resolve GameTurnSystem from CompositionRoot if available; fall back to in-memory wiring for T2 demo.
        var root = CompositionRoot.Instance;
        ITime timePort;
        IEventBus eventBus;

        if (root != null)
        {
            timePort = root.Time ?? new FixedTimePort();
            var busNode = root.EventBus ?? _eventBus;
            eventBus = busNode ?? new InMemoryEventBus();
        }
        else
        {
            // Defensive: still allow HUD to demonstrate turn system even if CompositionRoot is not available.
            timePort = new FixedTimePort();
            eventBus = (IEventBus?)_eventBus ?? new InMemoryEventBus();
        }

        IEventCatalog catalog = new EmptyEventCatalog();
        var saveId = new SaveIdValue("t2-demo");

        var world = new InMemoryAiWorldStatePort();
        world.Seed(saveId, week: 1, CreateDemoWorldSnapshot());

        IAICoordinator aiCoordinator = new AICoordinator(world, new GuidIdGenerator());
        _turnSystem = new GameTurnSystem(new EventEngine(catalog, eventBus, timePort, aiCoordinator: aiCoordinator), aiCoordinator, eventBus, timePort);
        _currentTurn = _turnSystem.StartNewWeek(saveId);
        UpdateTurnLabels();
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
        if (type == "core.score.updated" || type == "score.changed")
        {
            try
            {
                using var doc = JsonDocument.Parse(dataJson, JsonOptions);
                int v = 0;
                if (doc.RootElement.TryGetProperty("value", out var val)) v = val.GetInt32();
                else if (doc.RootElement.TryGetProperty("score", out var sc)) v = sc.GetInt32();
                _score.Text = $"Score: {v}";
            }
            catch { }
        }
        else if (type == "core.health.updated" || type == "player.health.changed")
        {
            try
            {
                using var doc = JsonDocument.Parse(dataJson, JsonOptions);
                int v = 0;
                if (doc.RootElement.TryGetProperty("value", out var val)) v = val.GetInt32();
                else if (doc.RootElement.TryGetProperty("health", out var hp)) v = hp.GetInt32();
                _health.Text = $"HP: {v}";
            }
            catch { }
        }
    }

    public void SetScore(int v) => _score.Text = $"Score: {v}";
    public void SetHealth(int v) => _health.Text = $"HP: {v}";

    // Public entry for GDScript debug button to advance turn once.
    public void AdvanceTurnFromGd() => OnNextTurnPressed();

    private async void OnNextTurnPressed()
    {
        if (_turnSystem == null || _currentTurn == null)
            return;
        try
        {
            _currentTurn = await _turnSystem.Advance(_currentTurn);
            UpdateTurnLabels();
        }
        catch
        {
            // For demo/T2 only: ignore errors to avoid breaking HUD.
        }
    }

    private void UpdateTurnLabels()
    {
        if (_currentTurn == null)
            return;
        if (_week != null)
            _week.Text = $"Week: {_currentTurn.Week}";
        if (_phase != null)
            _phase.Text = $"Phase: {_currentTurn.Phase}";
    }

    private sealed class EmptyEventCatalog : IEventCatalog
    {
    }

    private sealed class NoopAICoordinator : IAICoordinator
    {
        public GameTurnState StepAiCycle(GameTurnState state) => state;

        public System.Collections.Generic.IReadOnlyList<Game.Core.Contracts.DomainEvent> GenerateAiEvents(GameTurnState state) =>
            Array.Empty<Game.Core.Contracts.DomainEvent>();
    }

    private static AiWorldSnapshot CreateDemoWorldSnapshot()
    {
        var guilds = new System.Collections.Generic.Dictionary<string, AiWorldGuild>(StringComparer.Ordinal)
        {
            ["npc-guild-01"] = new AiWorldGuild("npc-guild-01", CurrentMembers: 0, MaxMembers: 5),
            ["npc-guild-02"] = new AiWorldGuild("npc-guild-02", CurrentMembers: 0, MaxMembers: 5),
        };

        var members = new System.Collections.Generic.Dictionary<string, AiWorldMember>(StringComparer.Ordinal)
        {
            ["npc-0001"] = new AiWorldMember("npc-0001", CurrentGuildId: null),
            ["npc-0002"] = new AiWorldMember("npc-0002", CurrentGuildId: null),
        };

        var affinity = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IReadOnlyDictionary<string, int>>(StringComparer.Ordinal)
        {
            ["npc-0001"] = new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["npc-guild-01"] = 10,
                ["npc-guild-02"] = 2,
            },
            ["npc-0002"] = new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["npc-guild-01"] = 10,
                ["npc-guild-02"] = 9,
            },
        };

        return new AiWorldSnapshot(guilds, members, affinity);
    }

    private sealed class FixedTimePort : ITime
    {
        public double DeltaSeconds => 1.0 / 60.0;
        public DateTimeOffset UtcNowOffset => DateTimeOffset.UtcNow;
    }
}
