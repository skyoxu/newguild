using System.Threading.Tasks;
using Godot;
using System.Text.Json;
using Game.Godot.Adapters;
using Game.Godot.Autoloads;
using Game.Core.Domain.Turn;
using Game.Core.Engine;
using Game.Core.Services;
using Game.Core.Ports;

namespace Game.Godot.Scripts.UI;

public partial class HUD : Control
{
    private Label _score = default!;
    private Label _health = default!;
    private Label _week = default!;
    private Label _phase = default!;
    private Button _nextTurnButton = default!;

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

        var bus = GetNodeOrNull<EventBusAdapter>("/root/EventBus");
        if (bus != null)
        {
            bus.Connect(EventBusAdapter.SignalName.DomainEventEmitted, new Callable(this, nameof(OnDomainEventEmitted)));
        }

        // Resolve GameTurnSystem from CompositionRoot if available; fall back to in-memory wiring for T2 demo.
        var root = CompositionRoot.Instance;
        ITime timePort;
        IEventBus eventBus;

        if (root != null)
        {
            timePort = root.Time ?? new TimeAdapter();
            var busNode = root.EventBus ?? GetNodeOrNull<EventBusAdapter>("/root/EventBus");
            eventBus = busNode ?? new InMemoryEventBus();
        }
        else
        {
            // Defensive: still allow HUD to demonstrate turn system even if CompositionRoot is not available.
            timePort = new TimeAdapter();
            eventBus = new InMemoryEventBus();
        }

        IEventCatalog catalog = new EmptyEventCatalog();
        IAICoordinator aiCoordinator = new NoopAICoordinator();
        _turnSystem = new GameTurnSystem(new EventEngine(catalog, eventBus), aiCoordinator, eventBus, timePort);
        _currentTurn = _turnSystem.StartNewWeek("t2-demo");
        UpdateTurnLabels();
    }

    private void OnDomainEventEmitted(string type, string source, string dataJson, string id, string specVersion, string dataContentType, string timestampIso)
    {
        if (type == "core.score.updated" || type == "score.changed")
        {
            try
            {
                var doc = JsonDocument.Parse(dataJson);
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
                var doc = JsonDocument.Parse(dataJson);
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
    }
}
