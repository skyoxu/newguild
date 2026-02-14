using Godot;
using System;
using Game.Core.Contracts.Persistence;
using Game.Core.Contracts.UI;
using Game.Core.Ports;
using Game.Core.Services;
using Game.Core.Contracts.Security;
using Game.Godot.Adapters;
using System.Collections.Generic;

namespace Game.Godot.Scripts.Screens;

public partial class StartScreen : Control
{
    private const string DemoGuildId = "npc-guild-01";
    private const int MaxLogLines = 200;
    private const string CoreAiPrefix = "core.ai.";
    private const string DemosDisabledHint = "Demos disabled. GD_ENABLE_PLAYABLE=0 always disables. Use GD_ENABLE_PLAYABLE=1 or unset it and set SECURITY_TEST_MODE=1 for test mode.";
    private const string DemoGateTarget = "ai-log-popup";
    private const string DemoGateCaller = "StartScreen";
    private const string FallbackScreenNavigatorPath = "/root/Main/ScreenNavigator";
    private const string FallbackMainMenuPath = "/root/Main/MainMenu";
    private const string FallbackHudPath = "/root/Main/HUD";
    private const string DragPayloadSourceKey = "source";
    private const string DragPayloadTargetKey = "target";
    private const string DragPayloadInteractionKey = "interaction";
    private const string DragPayloadExpectedSource = DemoGateCaller;
    private const string DragPayloadExpectedTarget = DemoGateTarget;
    private const string DragPayloadExpectedInteraction = "dragdrop";

    [Export]
    public NodePath ScreenNavigatorPath { get; set; } = new NodePath("../../ScreenNavigator");

    [Export]
    public NodePath MainMenuPath { get; set; } = new NodePath("../../MainMenu");

    [Export]
    public NodePath HudPath { get; set; } = new NodePath("../../HUD");

    private Button _btnOpenGuild = default!;
    private Button _btnSaveLoad = default!;
    private Button _btnActivityFeed = default!;
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
    private SecurityGateDecisionPublisher? _gateDecisionPublisher;

    private Node? _hud;
    private Callable _raidCompletedCallable;
    private Callable _domainEventCallable;
    private readonly List<string> _aiLogLines = new();
    private bool _areDemosEnabled;

    public override void _Ready()
    {
        GD.Print("[StartScreen] _Ready");

        _btnOpenGuild = GetNode<Button>("Center/VBox/BtnOpenGuild");
        _btnSaveLoad = GetNode<Button>("Center/VBox/BtnSaveLoad");
        _btnActivityFeed = GetNode<Button>("Center/VBox/BtnActivityFeed");
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
        _btnActivityFeed.Pressed += OnActivityFeedPressed;
        _btnAiLog.Pressed += OnShowAiLogPressed;
        _btnDemoRaid.Pressed += OnDemoRaidPressed;
        _btnDemoMedia.Pressed += OnDemoMediaPressed;
        _btnDemoReputation.Pressed += OnDemoReputationPressed;
        _btnBack.Pressed += OnBackPressed;
        _btnCloseLog.Pressed += OnCloseAiLogPressed;

        _bus = GetNodeOrNull<EventBusAdapter>("/root/EventBus");
        _gateDecisionPublisher = _bus != null ? new SecurityGateDecisionPublisher(_bus) : null;
        _time = GetNodeOrNull<TimeAdapter>("/root/Time");

        if (_bus != null)
        {
            _reputation = new ReputationSystem(_bus, _time, _ids);
            _mediaBeats = new MediaBeatSystem(_bus, _time, _ids);

            _domainEventCallable = new Callable(this, nameof(OnDomainEventEmitted));
            _bus.Connect(EventBusAdapter.SignalName.DomainEventEmitted, _domainEventCallable);
        }

        _hud = ResolveHud();
        if (_hud != null && _hud.HasSignal("RaidEncounterDemoCompleted"))
        {
            _raidCompletedCallable = new Callable(this, nameof(OnRaidEncounterDemoCompleted));
            _hud.Connect("RaidEncounterDemoCompleted", _raidCompletedCallable);
        }

        var demosAllowedByPolicy = AreDemosEnabled();
        _areDemosEnabled = demosAllowedByPolicy && _gateDecisionPublisher != null;

        _btnDemoRaid.Visible = _areDemosEnabled;
        _btnDemoMedia.Visible = _areDemosEnabled;
        _btnDemoReputation.Visible = _areDemosEnabled;
        _btnAiLog.Visible = _areDemosEnabled;
        if (!_areDemosEnabled)
            HideAiLogPopup();

        if (demosAllowedByPolicy && _gateDecisionPublisher == null)
            _output.Text = "Demos disabled. EventBus unavailable.";
        else
            _output.Text = _areDemosEnabled
                ? "Demos enabled."
                : DemosDisabledHint;
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
        var playableRaw = OS.GetEnvironment("GD_ENABLE_PLAYABLE");
        if (string.IsNullOrWhiteSpace(playableRaw))
            playableRaw = System.Environment.GetEnvironmentVariable("GD_ENABLE_PLAYABLE") ?? string.Empty;

        bool? playableOverride = playableRaw == "1" ? true : playableRaw == "0" ? false : null;

        var securityTestModeRaw = OS.GetEnvironment("SECURITY_TEST_MODE");
        if (string.IsNullOrWhiteSpace(securityTestModeRaw))
            securityTestModeRaw = System.Environment.GetEnvironmentVariable("SECURITY_TEST_MODE") ?? string.Empty;

        var securityTestModeEnabled = string.Equals(securityTestModeRaw, "1", StringComparison.Ordinal);

        return DemoGatePolicy.AreDemosEnabled(
            playableOverride: playableOverride,
            securityTestModeEnabled: securityTestModeEnabled,
            isDebugBuild: OS.IsDebugBuild());
    }

    private Node? ResolveNode(NodePath preferredPath, string fallbackAbsolutePath)
    {
        var byPreferredPath = GetNodeOrNull<Node>(preferredPath);
        if (byPreferredPath != null)
            return byPreferredPath;

        return GetNodeOrNull<Node>(fallbackAbsolutePath);
    }

    private Node? ResolveScreenNavigator()
    {
        return ResolveNode(ScreenNavigatorPath, FallbackScreenNavigatorPath);
    }

    private Node? ResolveMainMenu()
    {
        return ResolveNode(MainMenuPath, FallbackMainMenuPath);
    }

    private Node? ResolveHud()
    {
        return ResolveNode(HudPath, FallbackHudPath);
    }

    private void SetOutput(string message)
    {
        _output.Text = message;
        GD.Print($"[StartScreen] {message}");
    }

    private static bool IsAiEventType(string type)
    {
        return type.StartsWith(CoreAiPrefix, StringComparison.Ordinal);
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
        var nav = ResolveScreenNavigator();
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
                _bus.Call("PublishSimple", SaveRequested.EventType, "ui", payload);

            var savedOk = false;
            if (ds.HasMethod("TrySaveSync"))
                savedOk = (bool)ds.Call("TrySaveSync", key, json);
            else if (ds.HasMethod("SaveSync"))
            {
                ds.Call("SaveSync", key, json);
                savedOk = true;
            }

            if (_bus != null && _bus.HasMethod("PublishSimple"))
                _bus.Call("PublishSimple", savedOk ? SaveCompleted.EventType : SaveFailed.EventType, "ui", payload);

            if (_bus != null && _bus.HasMethod("PublishSimple"))
                _bus.Call("PublishSimple", LoadRequested.EventType, "ui", payload);

            Variant loaded = default;
            if (ds.HasMethod("TryLoadSync"))
                loaded = (Variant)ds.Call("TryLoadSync", key);
            else if (ds.HasMethod("LoadSync"))
                loaded = (Variant)ds.Call("LoadSync", key);

            var loadedOk = loaded.VariantType != Variant.Type.Nil;
            if (_bus != null && _bus.HasMethod("PublishSimple"))
                _bus.Call("PublishSimple", loadedOk ? LoadCompleted.EventType : LoadFailed.EventType, "ui", payload);

            SetOutput("Save+Load: " + (loadedOk ? "OK" : "FAILED"));
        }
        catch (Exception ex)
        {
            SetOutput($"Save+Load failed exType={ex.GetType().Name}");
        }
    }

    private void OnActivityFeedPressed()
    {
        if (_bus == null || !_bus.HasMethod("PublishSimple"))
        {
            SetOutput("EventBus not found.");
            return;
        }

        _bus.Call("PublishSimple", UiMenuEventTypes.Activity, "ui", "{}");
        SetOutput("Requested Activity Feed.");
    }

    private void OnShowAiLogPressed()
    {
        if (!_areDemosEnabled)
        {
            PublishDemoGateDecision(SecurityAiLogPopupGateDecision.DecisionDeny, SecurityAiLogPopupGateDecision.ReasonDemosDisabled);
            SetOutput(DemosDisabledHint);
            return;
        }

        if (_eventLogPopup == null)
        {
            PublishDemoGateDecision(SecurityAiLogPopupGateDecision.DecisionError, SecurityAiLogPopupGateDecision.ReasonPopupNotAvailable);
            SetOutput("AI log popup is not available.");
            return;
        }

        ShowAiLogPopup();
        PublishDemoGateDecision(SecurityAiLogPopupGateDecision.DecisionAllow, SecurityAiLogPopupGateDecision.ReasonPopupOpened);
    }

    private void OnCloseAiLogPressed()
    {
        HideAiLogPopup();
    }

    private void OnRightClickInteraction()
    {
        if (!_areDemosEnabled)
        {
            PublishDemoGateDecision(SecurityAiLogPopupGateDecision.DecisionDeny, SecurityAiLogPopupGateDecision.ReasonDemosDisabled);
            SetOutput(DemosDisabledHint);
            return;
        }

        if (ToggleAiLogPopup())
        {
            PublishDemoGateDecision(SecurityAiLogPopupGateDecision.DecisionAllow, SecurityAiLogPopupGateDecision.ReasonPopupToggled);
            SetOutput(_eventLogPopup != null && _eventLogPopup.Visible
                ? "AI log popup opened from interaction."
                : "AI log popup closed from interaction.");
            GetViewport()?.SetInputAsHandled();
        }
        else
        {
            PublishDemoGateDecision(SecurityAiLogPopupGateDecision.DecisionError, SecurityAiLogPopupGateDecision.ReasonPopupNotAvailable);
            SetOutput("AI log popup is not available.");
        }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (!_areDemosEnabled)
        {
            PublishDemoGateDecision(SecurityAiLogPopupGateDecision.DecisionDeny, SecurityAiLogPopupGateDecision.ReasonDemosDisabled);
            return default;
        }

        _ = atPosition;
        SetOutput("Drag-drop payload prepared.");
        return new global::Godot.Collections.Dictionary
        {
            { DragPayloadSourceKey, DragPayloadExpectedSource },
            { DragPayloadTargetKey, DragPayloadExpectedTarget },
            { DragPayloadInteractionKey, DragPayloadExpectedInteraction },
        };
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        _ = atPosition;
        if (!_areDemosEnabled)
        {
            PublishDemoGateDecision(SecurityAiLogPopupGateDecision.DecisionDeny, SecurityAiLogPopupGateDecision.ReasonDemosDisabled);
            return false;
        }

        if (data.VariantType != Variant.Type.Dictionary)
        {
            PublishDemoGateDecision(SecurityAiLogPopupGateDecision.DecisionDeny, SecurityAiLogPopupGateDecision.ReasonInvalidPayload);
            return false;
        }

        var payload = data.AsGodotDictionary();
        var isExpectedPayload = HasExpectedPayloadValue(payload, DragPayloadSourceKey, DragPayloadExpectedSource)
            && HasExpectedPayloadValue(payload, DragPayloadTargetKey, DragPayloadExpectedTarget)
            && HasExpectedPayloadValue(payload, DragPayloadInteractionKey, DragPayloadExpectedInteraction);

        if (!isExpectedPayload)
            PublishDemoGateDecision(SecurityAiLogPopupGateDecision.DecisionDeny, SecurityAiLogPopupGateDecision.ReasonInvalidPayload);

        return isExpectedPayload;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (!_CanDropData(atPosition, data))
        {
            SetOutput(_areDemosEnabled ? "Drag-drop rejected." : DemosDisabledHint);
            return;
        }

        OnRightClickInteraction();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent &&
            keyEvent.Pressed &&
            keyEvent.Keycode == Key.Escape)
        {
            if (_eventLogPopup != null && _eventLogPopup.Visible)
            {
                HideAiLogPopup();
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        if (@event is InputEventKey shortcutEvent &&
            shortcutEvent.Pressed &&
            !shortcutEvent.Echo &&
            (shortcutEvent.Keycode == Key.F1 || shortcutEvent.PhysicalKeycode == Key.F1))
        {
            OnRightClickInteraction();
            return;
        }

        if (@event is InputEventMouseButton mouseEvent &&
            mouseEvent.Pressed &&
            mouseEvent.ButtonIndex == MouseButton.Right)
        {
            OnRightClickInteraction();
        }
    }

    private static bool HasExpectedPayloadValue(global::Godot.Collections.Dictionary payload, string key, string expectedValue)
    {
        if (!payload.ContainsKey(key))
            return false;

        var value = payload[key];
        if (value.VariantType != Variant.Type.String)
            return false;

        var actual = value.AsString();
        return string.Equals(actual, expectedValue, StringComparison.Ordinal);
    }

    private void PublishDemoGateDecision(string decision, string reason)
    {
        if (_gateDecisionPublisher == null)
        {
            GD.PushWarning($"[StartScreen] SecurityGateDecisionPublisher unavailable decision={decision} reason={reason}");
            return;
        }

        var published = _gateDecisionPublisher.TryPublishAiLogPopupDecision(
            decision: decision,
            reason: reason,
            source: DemoGateCaller,
            target: DemoGateTarget,
            caller: DemoGateCaller);

        if (!published)
            SetOutput("Security gate decision publish failed.");
    }

    private bool ToggleAiLogPopup()
    {
        if (!_areDemosEnabled)
            return false;

        if (_eventLogPopup == null)
            return false;

        if (_eventLogPopup.Visible)
        {
            HideAiLogPopup();
            return true;
        }

        ShowAiLogPopup();
        return true;
    }

    private void ShowAiLogPopup()
    {
        if (!_areDemosEnabled)
        {
            HideAiLogPopup();
            return;
        }

        if (_eventLogPopup == null)
            return;

        RefreshAiLogUi();
        _eventLogPopup.PopupCentered();
    }

    private void HideAiLogPopup()
    {
        _eventLogPopup?.Hide();
    }

    private void OnBackPressed()
    {
        var nav = ResolveScreenNavigator();
        if (nav != null && nav.HasMethod("Clear"))
            nav.Call("Clear");

        var menu = ResolveMainMenu();
        if (menu != null && menu.HasMethod("ShowMenu"))
            menu.Call("ShowMenu");
    }

    private void OnDemoRaidPressed()
    {
        if (!_areDemosEnabled)
        {
            SetOutput(DemosDisabledHint);
            return;
        }

        var hud = ResolveHud();
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
        if (!_areDemosEnabled)
        {
            SetOutput(DemosDisabledHint);
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
        if (!_areDemosEnabled)
        {
            SetOutput(DemosDisabledHint);
            return;
        }
        if (_reputation == null)
        {
            SetOutput("ReputationSystem is not available (missing EventBus).");
            return;
        }

        try
        {
            var newReputation = await _reputation.ApplyDeltaAsync(
                guildId: DemoGuildId,
                delta: +10,
                reason: "StartScreen demo",
                sourceId: "demo.startscreen.reputation");

            SetOutput($"Reputation updated: {DemoGuildId} -> {newReputation} (check HUD).");
        }
        catch (Exception ex)
        {
            SetOutput($"Reputation demo failed exType={ex.GetType().Name}");
        }
    }
}
