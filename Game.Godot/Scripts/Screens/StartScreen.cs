using Godot;
using System;
using Game.Core.Ports;
using Game.Core.Services;
using Game.Godot.Adapters;
using System.Collections.Generic;

namespace Game.Godot.Scripts.Screens;

public partial class StartScreen : Control
{
    private const string DemoGuildId = "npc-guild-01";
    private const int MaxLogLines = 200;

    private Button _btnOpenGuild = default!;
    private Button _btnSaveLoad = default!;
    private Button _btnAiLog = default!;
    private Button _btnDemoRaid = default!;
    private Button _btnDemoMedia = default!;
    private Button _btnDemoReputation = default!;
    private Button _btnBack = default!;
    private Label _output = default!;
    private PopupPanel _eventLogPopup = default!;
    private RichTextLabel _eventLog = default!;
    private Button _btnCloseLog = default!;

    private EventBusAdapter? _bus;
    private ITime? _time;
    private readonly IIdGenerator _ids = new GuidIdGenerator();
    private ReputationSystem? _reputation;
    private MediaBeatSystem? _mediaBeats;

    private Node? _hud;
    private Callable _raidCompletedCallable;
    private Callable _domainEventCallable;
    private readonly List<string> _aiLogLines = new();

    public override void _Ready()
    {
        GD.Print("[StartScreen] _Ready");

        _btnOpenGuild = GetNode<Button>("Center/VBox/BtnOpenGuild");
        _btnSaveLoad = GetNode<Button>("Center/VBox/BtnSaveLoad");
        _btnAiLog = GetNode<Button>("Center/VBox/BtnAiLog");
        _btnDemoRaid = GetNode<Button>("Center/VBox/BtnDemoRaid");
        _btnDemoMedia = GetNode<Button>("Center/VBox/BtnDemoMedia");
        _btnDemoReputation = GetNode<Button>("Center/VBox/BtnDemoReputation");
        _btnBack = GetNode<Button>("Center/VBox/BtnBack");
        _output = GetNode<Label>("Center/VBox/Output");

        _eventLogPopup = GetNode<PopupPanel>("EventLogPopup");
        _eventLog = GetNode<RichTextLabel>("EventLogPopup/Panel/Margin/VBox/Log");
        _btnCloseLog = GetNode<Button>("EventLogPopup/Panel/Margin/VBox/BtnClose");

        _btnOpenGuild.Pressed += OnOpenGuildPressed;
        _btnSaveLoad.Pressed += OnSaveLoadPressed;
        _btnAiLog.Pressed += OnShowAiLogPressed;
        _btnDemoRaid.Pressed += OnDemoRaidPressed;
        _btnDemoMedia.Pressed += OnDemoMediaPressed;
        _btnDemoReputation.Pressed += OnDemoReputationPressed;
        _btnBack.Pressed += OnBackPressed;
        _btnCloseLog.Pressed += OnCloseAiLogPressed;

        _bus = GetNodeOrNull<EventBusAdapter>("/root/EventBus");
        _time = GetNodeOrNull<TimeAdapter>("/root/Time");

        if (_bus != null)
        {
            _reputation = new ReputationSystem(_bus, _time, _ids);
            _mediaBeats = new MediaBeatSystem(_bus, _time, _ids);

            _domainEventCallable = new Callable(this, nameof(OnDomainEventEmitted));
            _bus.Connect(EventBusAdapter.SignalName.DomainEventEmitted, _domainEventCallable);
        }

        _hud = GetNodeOrNull<Node>("/root/Main/HUD");
        if (_hud != null && _hud.HasSignal("RaidEncounterDemoCompleted"))
        {
            _raidCompletedCallable = new Callable(this, nameof(OnRaidEncounterDemoCompleted));
            _hud.Connect("RaidEncounterDemoCompleted", _raidCompletedCallable);
        }

        var demosEnabled = AreDemosEnabled();
        _btnDemoRaid.Visible = demosEnabled;
        _btnDemoMedia.Visible = demosEnabled;
        _btnDemoReputation.Visible = demosEnabled;
        _btnAiLog.Visible = demosEnabled;

        _output.Text = demosEnabled
            ? "Demos enabled."
            : "Demos disabled. Set GD_ENABLE_PLAYABLE=1 to enable.";
    }

    public override void _ExitTree()
    {
        if (_bus != null)
        {
            try
            {
                if (_bus.IsConnected(EventBusAdapter.SignalName.DomainEventEmitted, _domainEventCallable))
                    _bus.Disconnect(EventBusAdapter.SignalName.DomainEventEmitted, _domainEventCallable);
            }
            catch
            {
                // Best-effort only.
            }
        }

        if (_hud == null)
            return;
        if (_hud.IsConnected("RaidEncounterDemoCompleted", _raidCompletedCallable))
            _hud.Disconnect("RaidEncounterDemoCompleted", _raidCompletedCallable);
    }

    // Optional lifecycle hooks recognized by ScreenNavigator
    public void Enter()
    {
        GD.Print("[StartScreen] Enter");
    }

    public void Exit()
    {
        GD.Print("[StartScreen] Exit");
    }

    private static bool AreDemosEnabled()
    {
        if (OS.IsDebugBuild())
            return true;

        return string.Equals(OS.GetEnvironment("GD_ENABLE_PLAYABLE"), "1", StringComparison.Ordinal) ||
               string.Equals(OS.GetEnvironment("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal);
    }

    private void SetOutput(string message)
    {
        _output.Text = message;
        GD.Print($"[StartScreen] {message}");
    }

    private static bool IsAiEventType(string type)
    {
        return type.StartsWith("core.ai.", StringComparison.Ordinal);
    }

    private void AppendAiLogLine(string line)
    {
        _aiLogLines.Add(line);
        if (_aiLogLines.Count > MaxLogLines)
            _aiLogLines.RemoveAt(0);

        if (_eventLogPopup != null && _eventLogPopup.Visible)
            RefreshAiLogUi();
    }

    private void RefreshAiLogUi()
    {
        if (_eventLog == null)
            return;
        _eventLog.Clear();
        _eventLog.AppendText("[b]AI events[/b]\\n");
        foreach (var ln in _aiLogLines)
            _eventLog.AppendText(ln + "\\n");
    }

    private void OnDomainEventEmitted(string type, string source, string dataJson, string id, string specVersion, string dataContentType, string timestampIso)
    {
        if (!IsAiEventType(type))
            return;

        var msg = $"{timestampIso} {type} source={source} id={id}";
        AppendAiLogLine(msg);
    }

    private void OnOpenGuildPressed()
    {
        var nav = GetNodeOrNull<Node>("/root/Main/ScreenNavigator");
        if (nav == null || !nav.HasMethod("SwitchTo"))
        {
            SetOutput("ScreenNavigator not found.");
            return;
        }

        var ok = (bool)nav.Call("SwitchTo", "res://Game.Godot/Scenes/Screens/GuildScreen.tscn");
        SetOutput(ok ? "Opened GuildScreen." : "Failed to open GuildScreen.");
    }

    private void OnSaveLoadPressed()
    {
        var ds = GetNodeOrNull<Node>("/root/DataStore");
        if (ds == null)
        {
            SetOutput("DataStore not found.");
            return;
        }

        var key = "ui_save_entry";
        var json = "{\"ts\":" + Time.GetUnixTimeFromSystem() + "}";
        var payload = "{\"saveId\":\"" + key.Replace("\"", "\\\"") + "\"}";

        try
        {
            if (_bus != null && _bus.HasMethod("PublishSimple"))
                _bus.Call("PublishSimple", "core.save.requested", "ui", payload);

            var savedOk = false;
            if (ds.HasMethod("TrySaveSync"))
                savedOk = (bool)ds.Call("TrySaveSync", key, json);
            else if (ds.HasMethod("SaveSync"))
            {
                ds.Call("SaveSync", key, json);
                savedOk = true;
            }

            if (_bus != null && _bus.HasMethod("PublishSimple"))
                _bus.Call("PublishSimple", savedOk ? "core.save.completed" : "core.save.failed", "ui", payload);

            if (_bus != null && _bus.HasMethod("PublishSimple"))
                _bus.Call("PublishSimple", "core.load.requested", "ui", payload);

            Variant loaded = default;
            if (ds.HasMethod("TryLoadSync"))
                loaded = (Variant)ds.Call("TryLoadSync", key);
            else if (ds.HasMethod("LoadSync"))
                loaded = (Variant)ds.Call("LoadSync", key);

            var loadedOk = loaded.VariantType != Variant.Type.Nil;
            if (_bus != null && _bus.HasMethod("PublishSimple"))
                _bus.Call("PublishSimple", loadedOk ? "core.load.completed" : "core.load.failed", "ui", payload);

            SetOutput("Save+Load: " + (loadedOk ? "OK" : "FAILED"));
        }
        catch (Exception ex)
        {
            SetOutput($"Save+Load failed exType={ex.GetType().Name}");
        }
    }

    private void OnShowAiLogPressed()
    {
        if (_eventLogPopup == null)
            return;
        RefreshAiLogUi();
        _eventLogPopup.PopupCentered();
    }

    private void OnCloseAiLogPressed()
    {
        _eventLogPopup?.Hide();
    }

    private void OnBackPressed()
    {
        var nav = GetNodeOrNull<Node>("/root/Main/ScreenNavigator");
        if (nav != null && nav.HasMethod("Clear"))
            nav.Call("Clear");

        var menu = GetNodeOrNull<Node>("/root/Main/MainMenu");
        if (menu != null && menu.HasMethod("ShowMenu"))
            menu.Call("ShowMenu");
    }

    private void OnDemoRaidPressed()
    {
        if (!AreDemosEnabled())
        {
            SetOutput("Demos disabled (GD_ENABLE_PLAYABLE=1).");
            return;
        }

        var hud = GetNodeOrNull<Node>("/root/Main/HUD");
        if (hud == null || !hud.HasMethod("TriggerRaidEncounterDemo"))
        {
            SetOutput("HUD raid demo is not available.");
            return;
        }

        SetOutput("Triggered raid encounter demo.");
        hud.Call("TriggerRaidEncounterDemo");
    }

    private void OnRaidEncounterDemoCompleted(string result)
    {
        SetOutput($"Raid demo completed result={result}");
    }

    private async void OnDemoMediaPressed()
    {
        if (!AreDemosEnabled())
        {
            SetOutput("Demos disabled (GD_ENABLE_PLAYABLE=1).");
            return;
        }
        if (_mediaBeats == null)
        {
            SetOutput("MediaBeatSystem is not available (missing EventBus).");
            return;
        }

        try
        {
            var beatId = _ids.NewId();
            await _mediaBeats.TriggerBeatAsync(
                beatId: beatId,
                guildId: DemoGuildId,
                sourceEventType: "demo.startscreen.media",
                headline: "StartScreen demo media beat");

            SetOutput($"Triggered media beat beatId={beatId} (check HUD).");
        }
        catch (Exception ex)
        {
            SetOutput($"Media beat demo failed exType={ex.GetType().Name}");
        }
    }

    private async void OnDemoReputationPressed()
    {
        if (!AreDemosEnabled())
        {
            SetOutput("Demos disabled (GD_ENABLE_PLAYABLE=1).");
            return;
        }
        if (_reputation == null)
        {
            SetOutput("ReputationSystem is not available (missing EventBus).");
            return;
        }

        try
        {
            var v = await _reputation.ApplyDeltaAsync(
                guildId: DemoGuildId,
                delta: +10,
                reason: "StartScreen demo",
                sourceId: "demo.startscreen.reputation");

            SetOutput($"Reputation updated: {DemoGuildId} -> {v} (check HUD).");
        }
        catch (Exception ex)
        {
            SetOutput($"Reputation demo failed exType={ex.GetType().Name}");
        }
    }
}
