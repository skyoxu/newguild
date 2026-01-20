using System.Text.Json;
using Godot;
using Game.Core.Contracts.Social;
using Game.Godot.Adapters;

namespace Game.Godot.Scripts.UI;

/// <summary>
/// Minimal UI panel to display the current intimacy/relationship value from contract events.
/// Follows ADR-0018 (Godot UI layer) and ADR-0004 (event contracts).
/// </summary>
public partial class IntimacyPanel : Control
{
    [Export]
    public NodePath EventBusPath { get; set; } = new NodePath("/root/EventBus");

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        MaxDepth = 32,
    };

    private Label _valueLabel = default!;
    private EventBusAdapter? _eventBus;
    private Callable _domainEventCallable;

    public override void _Ready()
    {
        _valueLabel = GetNode<Label>("IntimacyValueLabel");
        _valueLabel.Text = "Intimacy: -";

        _eventBus = GetNodeOrNull<EventBusAdapter>(EventBusPath);
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
        if (type != SocialRelationshipChanged.EventType)
            return;

        try
        {
            using var doc = JsonDocument.Parse(dataJson, JsonOptions);
            if (!doc.RootElement.TryGetProperty("newValue", out var newValue))
                return;

            var v = newValue.GetInt32();
            _valueLabel.Text = $"Intimacy: {v}";
        }
        catch
        {
            // Ignore malformed events
        }
    }
}

