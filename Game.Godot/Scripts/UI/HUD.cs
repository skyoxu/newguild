using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using System.Text.Json;
using Game.Godot.Adapters;
using Game.Godot.Autoloads;
using Game.Core.Contracts;
using Game.Core.Contracts.Security;
using Game.Core.Domain.Turn;
using Game.Core.Engine;
using Game.Godot.Scripts.Demo;
using Game.Core.Services;
using Game.Core.Ports;
using Game.Core.Ports.AI;

namespace Game.Godot.Scripts.UI;

public partial class HUD : Control
{
    public const string DemoResultDenied = "denied";
    public const string DemoResultError = "error";
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        MaxDepth = 32,
    };
    private const string DemoAuditSource = "game.godot/hud";
    private const string DemoAuditTarget = "raid-encounter-demo";

    private Label _score = default!;
    private Label _health = default!;
    private Label _week = default!;
    private Label _phase = default!;
    private Button _nextTurnButton = default!;

    private EventBusAdapter? _eventBus;
    private Callable _domainEventCallable;

    private IGameTurnSystem? _turnSystem;
    private GameTurnState? _currentTurn;

    private IEventBus? _coreEventBus;
    private ITime? _coreTime;
    private IIdGenerator? _coreIdGenerator;

    public string RaidEncounterDemoLastResult { get; private set; } = "";
    private int _raidEncounterDemoInFlight;

    [Signal]
    public delegate void RaidEncounterDemoCompletedEventHandler(string result);

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

        _coreEventBus = eventBus;
        _coreTime = timePort;
        _coreIdGenerator = new GuidIdGenerator();

        IEventCatalog catalog = new EmptyEventCatalog();
        var saveId = new SaveIdValue("t2-demo");

        var world = new InMemoryAiWorldStatePort();
        world.Seed(saveId, week: 1, CreateDemoWorldSnapshot());

        IAICoordinator aiCoordinator = new AICoordinator(world, new GuidIdGenerator());
        _turnSystem = new GameTurnSystem(new EventEngine(catalog, eventBus, timePort, aiCoordinator: aiCoordinator), eventBus, timePort);
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

    // Public debug entry for T17 demo: trigger one encounter and expose the result to UI/tests.
    public void TriggerRaidEncounterDemo()
    {
        if (Interlocked.Exchange(ref _raidEncounterDemoInFlight, 1) == 1)
            return;
        _ = TriggerRaidEncounterDemoAsync();
    }

    public async Task<string> TriggerRaidEncounterDemoAsync()
    {
        var caller = $"{nameof(HUD)}.{nameof(TriggerRaidEncounterDemoAsync)}";
        try
        {
            var (allowed, allowReason) = CheckRaidEncounterDemoAllowed();
            if (!allowed)
            {
                RaidEncounterDemoLastResult = DemoResultDenied;
                await TryPublishDemoGateDecisionAsync(
                    decision: SecurityDemoGateDecision.DecisionDeny,
                    reason: allowReason,
                    target: DemoAuditTarget,
                    caller: caller);
                return RaidEncounterDemoLastResult;
            }

            if (_coreEventBus == null || _coreTime == null || _coreIdGenerator == null)
            {
                RaidEncounterDemoLastResult = DemoResultError;
                GD.PrintErr("[RaidEncounterDemo] missing core dependencies (eventBus/time/idGenerator).");

                if (_coreEventBus != null)
                {
                    try
                    {
                        var data = new SecurityDemoGateDecision(
                            Target: DemoAuditTarget,
                            Decision: SecurityDemoGateDecision.DecisionError,
                            Reason: "missing_core_dependencies",
                            OccurredAt: DateTimeOffset.UtcNow,
                            Caller: caller);

                        var evt = new DomainEvent(
                            Type: SecurityDemoGateDecision.EventType,
                            Source: DemoAuditSource,
                            Data: data,
                            Timestamp: DateTime.UtcNow,
                            Id: Guid.NewGuid().ToString("N"));

                        await _coreEventBus.PublishAsync(evt);
                    }
                    catch (Exception ex)
                    {
                        GD.PushWarning($"[HUD] demo gate audit fallback publish failed exType={ex.GetType().Name}");
                    }
                }
                return RaidEncounterDemoLastResult;
            }

            var week = _currentTurn?.Week ?? 1;
            if (week < 1)
                week = 1;
            var runner = new RaidEncounterDemoRunner(_coreEventBus, _coreTime, _coreIdGenerator);
            RaidEncounterDemoLastResult = await runner.RunAsync(week);
            GD.Print($"[RaidEncounterDemo] week={week} result={RaidEncounterDemoLastResult}");
            var decision = RaidEncounterDemoLastResult == DemoResultError
                ? SecurityDemoGateDecision.DecisionError
                : SecurityDemoGateDecision.DecisionAllow;
            var auditReason = decision == SecurityDemoGateDecision.DecisionError
                ? "runner_error"
                : allowReason;
            await TryPublishDemoGateDecisionAsync(
                decision: decision,
                reason: auditReason,
                target: $"{DemoAuditTarget}:week={week}",
                caller: caller);
            return RaidEncounterDemoLastResult;
        }
        catch (Exception ex)
        {
            RaidEncounterDemoLastResult = DemoResultError;
            GD.PrintErr($"[RaidEncounterDemo] failed exType={ex.GetType().Name}");
            if (OS.IsDebugBuild() && string.Equals(System.Environment.GetEnvironmentVariable("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal))
                GD.PrintErr(ex.ToString());
            await TryPublishDemoGateDecisionAsync(
                decision: SecurityDemoGateDecision.DecisionError,
                reason: ex.GetType().Name,
                target: DemoAuditTarget,
                caller: caller);
            return RaidEncounterDemoLastResult;
        }
        finally
        {
            Interlocked.Exchange(ref _raidEncounterDemoInFlight, 0);
            CallDeferred(nameof(EmitRaidEncounterDemoCompleted), RaidEncounterDemoLastResult);
        }
    }

    private static (bool Allowed, string Reason) CheckRaidEncounterDemoAllowed()
    {
        var enabled = System.Environment.GetEnvironmentVariable("GD_ENABLE_PLAYABLE");
        return (string.Equals(enabled, "1", StringComparison.Ordinal), $"GD_ENABLE_PLAYABLE={enabled ?? "(null)"}");
    }

    private void EmitRaidEncounterDemoCompleted(string result)
    {
        EmitSignal(SignalName.RaidEncounterDemoCompleted, result);
    }

    private async Task<bool> TryPublishDemoGateDecisionAsync(string decision, string reason, string target, string caller)
    {
        try
        {
            if (_coreEventBus == null || _coreTime == null || _coreIdGenerator == null)
            {
                GD.PushWarning("[HUD] demo gate audit publish skipped: missing core dependencies.");
                GD.PrintErr($"[HUD] demo_gate_audit_skipped decision={decision} target={target} caller={caller} reason={reason}");
                return false;
            }

            var data = new SecurityDemoGateDecision(
                Target: target,
                Decision: decision,
                Reason: reason,
                OccurredAt: _coreTime.UtcNowOffset,
                Caller: caller);

            var evt = new DomainEvent(
                Type: SecurityDemoGateDecision.EventType,
                Source: DemoAuditSource,
                Data: data,
                Timestamp: _coreTime.UtcNowOffset.UtcDateTime,
                Id: _coreIdGenerator.NewId());

            await _coreEventBus.PublishAsync(evt);
            return true;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[HUD] demo gate audit publish failed exType={ex.GetType().Name}");
            GD.PrintErr($"[HUD] demo_gate_audit_publish_failed decision={decision} target={target} caller={caller} reason={reason} exType={ex.GetType().Name}");

            if (OS.IsDebugBuild() && string.Equals(System.Environment.GetEnvironmentVariable("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal))
                GD.PrintErr(ex.ToString());

            return false;
        }
    }

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
