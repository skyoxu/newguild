using Godot;
using Game.Godot.Adapters;
using System.Text.Json;

namespace Game.Godot.Scripts.UI;

public partial class HUD : Control
{
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        MaxDepth = 32,
    };

    private Label _score = default!;
    private Label _health = default!;
    private EventBusAdapter? _eventBus;
    private Callable _domainEventCallable;

    public override void _Ready()
    {
        _score = GetNode<Label>("TopBar/HBox/ScoreLabel");
        _health = GetNode<Label>("TopBar/HBox/HealthLabel");

        _eventBus = GetNodeOrNull<EventBusAdapter>("/root/EventBus");
        if (_eventBus != null)
        {
            _domainEventCallable = new Callable(this, nameof(OnDomainEventEmitted));
            _eventBus.Connect(EventBusAdapter.SignalName.DomainEventEmitted, _domainEventCallable);
        }
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
}
