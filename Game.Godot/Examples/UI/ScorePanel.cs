using Godot;
using Game.Godot.Adapters;
using Game.Core.Contracts.Engine;
using System;
using System.Text.Json;

namespace Game.Godot.Scripts.UI;

public partial class ScorePanel : Control
{
    private Label _score = default!;
    private Button _add10 = default!;
    private Button _add50 = default!;
    private int _scoreValue;

    public override void _Ready()
    {
        _score = GetNode<Label>("VBox/ScoreValue");
        _add10 = GetNode<Button>("VBox/Buttons/Add10");
        _add50 = GetNode<Button>("VBox/Buttons/Add50");
        _scoreValue = 0;
        _score.Text = _scoreValue.ToString();

        _add10.Pressed += () => OnAdd(10);
        _add50.Pressed += () => OnAdd(50);

        var bus = GetNodeOrNull<EventBusAdapter>("/root/EventBus");
        if (bus != null)
        {
            bus.Connect(EventBusAdapter.SignalName.DomainEventEmitted, new Callable(this, nameof(OnDomainEventEmitted)));
        }
    }

    private void OnAdd(int amount)
    {
        var engine = GetNodeOrNull<Node>("/root/Main/EngineDemo");
        if (engine != null && engine.HasMethod("AddScore"))
        {
            engine.Call("AddScore", amount);
            return;
        }
        // Fallback: publish UI event
        var bus = GetNodeOrNull<EventBusAdapter>("/root/EventBus");
        _scoreValue += amount;
        _score.Text = _scoreValue.ToString();
        bus?.PublishSimple(ScoreChanged.EventType, "ui", $"{{\"score\":{_scoreValue},\"added\":{amount}}}");
    }

    private void OnDomainEventEmitted(string type, string source, string dataJson, string id, string specVersion, string dataContentType, string timestampIso)
    {
        if (type == ScoreChanged.EventType)
        {
            try
            {
                using var doc = JsonDocument.Parse(dataJson);
                int v = 0;
                if (doc.RootElement.TryGetProperty("score", out var sc)) v = sc.GetInt32();
                else if (doc.RootElement.TryGetProperty("value", out var val)) v = val.GetInt32();
                _scoreValue = v;
                _score.Text = v.ToString();
            }
            catch (Exception ex)
            {
                if (OS.IsDebugBuild() || string.Equals(OS.GetEnvironment("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal))
                    GD.PrintErr($"[ScorePanel] failed to parse ScoreChanged payload exType={ex.GetType().Name}");
            }
        }
    }
}

