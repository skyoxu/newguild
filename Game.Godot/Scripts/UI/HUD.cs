using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using System.Text.Json;
using Game.Godot.Adapters;
using Game.Godot.Autoloads;
using Game.Core.Contracts;
using Game.Core.Contracts.Achievements;
using Game.Core.Contracts.Events;
using Game.Core.Contracts.Engine;
using Game.Core.Contracts.Media;
using Game.Core.Contracts.Progression;
using Game.Core.Contracts.Raid;
using Game.Core.Contracts.Security;
using Game.Core.Domain;
using Game.Core.Domain.Achievements;
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
    private const string ReputationLabelPrefix = "Reputation";
    private const string ExperienceLabelPrefix = "XP";
    private const string MediaBeatLabelPrefix = "MediaBeat";
    private const string AchievementsLabelPrefix = "Achievements";
    private const string EventCatalogPath = "res://Game.Godot/Assets/Data/content/base/event_catalog.json";
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
    private Label _achievements = default!;
    private Label _reputation = default!;
    private Label _experience = default!;
    private Label _mediaBeatLabel = default!;
    private Button _mediaBeatButton = default!;
    private Button _nextTurnButton = default!;
    private int _achievementsUnlockedCount;
    private AchievementTracker? _achievementTracker;

    private EventBusAdapter? _eventBus;
    private Callable _domainEventCallable;

    private IGameTurnSystem? _turnSystem;
    private GameTurnState? _currentTurn;

    private IEventBus? _coreEventBus;
    private ITime? _coreTime;
    private IIdGenerator? _coreIdGenerator;
    private int _demoScore;
    private MediaBeatSystem? _mediaBeatSystem;

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
        _achievements = GetNodeOrNull<Label>("TopBar/HBox/AchievementsLabel");
        _reputation = GetNodeOrNull<Label>("TopBar/HBox/ReputationLabel");
        _experience = GetNodeOrNull<Label>("TopBar/HBox/ExperienceLabel");
        _mediaBeatLabel = GetNodeOrNull<Label>("TopBar/HBox/MediaBeatLabel");
        _mediaBeatButton = GetNodeOrNull<Button>("TopBar/HBox/MediaBeatButton");
        _nextTurnButton = GetNodeOrNull<Button>("TopBar/HBox/NextTurnButton");

        if (_week != null)
            _week.Text = "Week: -";
        if (_phase != null)
            _phase.Text = "Phase: -";
        if (_achievements != null && string.IsNullOrWhiteSpace(_achievements.Text))
            _achievements.Text = FormatAchievementsText(0);
        if (_reputation != null && string.IsNullOrWhiteSpace(_reputation.Text))
            _reputation.Text = FormatReputationText(0);
        if (_experience != null && string.IsNullOrWhiteSpace(_experience.Text))
            _experience.Text = FormatExperienceText(0, 1);

        var allowMediaBeatDemo = IsMediaBeatDemoAllowed();
        if (_mediaBeatLabel != null)
        {
            _mediaBeatLabel.Visible = allowMediaBeatDemo;
            if (string.IsNullOrWhiteSpace(_mediaBeatLabel.Text))
                _mediaBeatLabel.Text = FormatMediaBeatText("-", "-");
        }
        if (_mediaBeatButton != null)
        {
            _mediaBeatButton.Visible = allowMediaBeatDemo;
            _mediaBeatButton.Pressed += TriggerMediaBeatDemo;
        }
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
            // Prefer the actual /root/EventBus node if present to avoid stale references
            // when tests temporarily rename/disable the original bus Node.
            var busNode = (IEventBus?)_eventBus ?? root.EventBus;
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
        _demoScore = 0;
        _mediaBeatSystem = new MediaBeatSystem(eventBus, timePort, _coreIdGenerator);
        _achievementsUnlockedCount = 0;
        _achievementTracker = new AchievementTracker(eventBus);
        _achievementTracker.UnlockedCountChanged += OnAchievementCountChanged;
        SetAchievementsUnlockedCount(_achievementTracker.UnlockedCount);

        var catalog = LoadEventCatalogOrThrow(eventBus, timePort);
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

        if (_achievementTracker != null)
        {
            _achievementTracker.UnlockedCountChanged -= OnAchievementCountChanged;
            _achievementTracker.Dispose();
            _achievementTracker = null;
        }
    }

    private void OnDomainEventEmitted(string type, string source, string dataJson, string id, string specVersion, string dataContentType, string timestampIso)
    {
        if (type == ScoreChanged.EventType)
        {
            try
            {
                using var doc = JsonDocument.Parse(dataJson, JsonOptions);
                int scoreValue;
                if (doc.RootElement.TryGetProperty("score", out var sc) && sc.TryGetInt32(out scoreValue)) { }
                else if (doc.RootElement.TryGetProperty("value", out var val) && val.TryGetInt32(out scoreValue)) { }
                else
                {
                    GD.PushWarning($"[HUD] invalid payload for {type} (expected int score/value).");
                    return;
                }
                _demoScore = scoreValue;
                _score.Text = $"Score: {scoreValue}";
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[HUD] failed to parse event payload type={type} exType={ex.GetType().Name}");
            }
        }
        else if (type == "core.health.updated" || type == "player.health.changed")
        {
            try
            {
                using var doc = JsonDocument.Parse(dataJson, JsonOptions);
                int healthValue;
                if (doc.RootElement.TryGetProperty("value", out var val) && val.TryGetInt32(out healthValue)) { }
                else if (doc.RootElement.TryGetProperty("health", out var hp) && hp.TryGetInt32(out healthValue)) { }
                else
                {
                    GD.PushWarning($"[HUD] invalid payload for {type} (expected int value/health).");
                    return;
                }
                _health.Text = $"HP: {healthValue}";
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[HUD] failed to parse event payload type={type} exType={ex.GetType().Name}");
            }
        }
        else if (type == ReputationChanged.EventType)
        {
            if (_reputation == null)
                return;

            try
            {
                using var doc = JsonDocument.Parse(dataJson, JsonOptions);
                int reputationValue;
                if (doc.RootElement.TryGetProperty("newValue", out var newValue) && newValue.TryGetInt32(out reputationValue)) { }
                else if (doc.RootElement.TryGetProperty("value", out var value) && value.TryGetInt32(out reputationValue)) { }
                else
                {
                    GD.PushWarning($"[HUD] invalid payload for {type} (expected int newValue/value).");
                    return;
                }

                _reputation.Text = FormatReputationText(reputationValue);
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[HUD] failed to parse event payload type={type} exType={ex.GetType().Name}");
            }
        }
        else if (type == ExperienceChanged.EventType || type == LevelChanged.EventType)
        {
            if (_experience == null)
                return;

            try
            {
                using var doc = JsonDocument.Parse(dataJson, JsonOptions);
                int total;
                int level;

                if (doc.RootElement.TryGetProperty("totalExperience", out var totalExperience) && totalExperience.TryGetInt32(out total)) { }
                else if (doc.RootElement.TryGetProperty("total", out var totalValue) && totalValue.TryGetInt32(out total)) { }
                else
                {
                    GD.PushWarning($"[HUD] invalid payload for {type} (expected int totalExperience/total).");
                    return;
                }

                if (doc.RootElement.TryGetProperty("level", out var levelValue) && levelValue.TryGetInt32(out level)) { }
                else if (doc.RootElement.TryGetProperty("newLevel", out var newLevel) && newLevel.TryGetInt32(out level)) { }
                else
                {
                    GD.PushWarning($"[HUD] invalid payload for {type} (expected int level/newLevel).");
                    return;
                }

                _experience.Text = FormatExperienceText(total, level);
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[HUD] failed to parse event payload type={type} exType={ex.GetType().Name}");
            }
        }
        else if (type == MediaBeatTriggered.EventType)
        {
            if (_mediaBeatLabel == null)
                return;

            try
            {
                using var doc = JsonDocument.Parse(dataJson, JsonOptions);
                var beatId = doc.RootElement.TryGetProperty("beatId", out var beat) ? beat.GetString() : null;
                var headline = doc.RootElement.TryGetProperty("headline", out var hl) ? hl.GetString() : null;

                if (string.IsNullOrWhiteSpace(beatId) && string.IsNullOrWhiteSpace(headline))
                {
                    GD.PushWarning($"[HUD] invalid payload for {type} (expected string beatId/headline).");
                    return;
                }

                _mediaBeatLabel.Text = FormatMediaBeatText(beatId ?? "-", headline ?? "-");
                GD.Print($"[HUD] observed media beat beatId={beatId ?? "?"}");
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[HUD] failed to parse event payload type={type} exType={ex.GetType().Name}");
            }
        }
    }

    private static string FormatReputationText(int value) => $"{ReputationLabelPrefix}: {value}";

    private static string FormatAchievementsText(int count) => $"{AchievementsLabelPrefix}: {count}";
    private static string FormatExperienceText(int totalExperience, int level) =>
        $"{ExperienceLabelPrefix}: {totalExperience} Lv: {level}";

    private static string FormatMediaBeatText(string beatId, string headline) =>
        $"{MediaBeatLabelPrefix}: {beatId} {headline}".Trim();

    private static bool IsMediaBeatDemoAllowed()
    {
        if (OS.IsDebugBuild())
            return true;

        return string.Equals(OS.GetEnvironment("GD_ENABLE_PLAYABLE"), "1", StringComparison.Ordinal) ||
               string.Equals(OS.GetEnvironment("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal) ||
               string.Equals(System.Environment.GetEnvironmentVariable("GD_ENABLE_PLAYABLE"), "1", StringComparison.Ordinal) ||
               string.Equals(System.Environment.GetEnvironmentVariable("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal);
    }

    public void SetScore(int v) => _score.Text = $"Score: {v}";
    public void SetHealth(int v) => _health.Text = $"HP: {v}";
    public void SetAchievementsUnlockedCount(int count)
    {
        _achievementsUnlockedCount = Math.Max(0, count);
        if (_achievements == null)
            return;
        _achievements.Text = FormatAchievementsText(_achievementsUnlockedCount);
    }
    private void OnAchievementCountChanged(object? sender, AchievementCountChanged args)
    {
        SetAchievementsUnlockedCount(args.UnlockedCount);
    }

    // Public entry for GDScript debug button to advance turn once.
    public void AdvanceTurnFromGd() => OnNextTurnPressed();

    // Public debug entry for T19 demo: trigger one media beat and expose the result to UI/tests.
    public void TriggerMediaBeatDemo()
    {
        if (!IsMediaBeatDemoAllowed())
            return;
        if (_mediaBeatSystem == null)
            return;
        if (_coreIdGenerator == null)
            return;

        var beatId = _coreIdGenerator.NewId();
        const string guildId = "npc-guild-01";
        const string sourceEventType = "demo.hud.mediaBeatButton.pressed";
        var headline = $"Demo media beat {DateTimeOffset.UtcNow:O}";

        ObserveFireAndForget(_mediaBeatSystem.TriggerBeatAsync(beatId, guildId, sourceEventType, headline), "media_beat_demo");
    }

    // Public debug entry for T17 demo: trigger one encounter and expose the result to UI/tests.
    public void TriggerRaidEncounterDemo()
    {
        if (Interlocked.Exchange(ref _raidEncounterDemoInFlight, 1) == 1)
            return;

        try
        {
            // NOTE: This is a demo/test entrypoint and must be deterministic under headless CI.
            // Keep all Godot API usage on the main thread; avoid async continuations that may resume on a worker thread.
            var caller = $"{nameof(HUD)}.{nameof(TriggerRaidEncounterDemo)}";

            var (allowed, allowReason) = CheckRaidEncounterDemoAllowed();
            if (!allowed)
            {
                RaidEncounterDemoLastResult = DemoResultDenied;
                _ = TryPublishDemoGateDecisionMainThread(
                    decision: SecurityDemoGateDecision.DecisionDeny,
                    reason: allowReason,
                    target: DemoAuditTarget,
                    caller: caller);
                return;
            }

            if (_coreEventBus == null || _coreTime == null || _coreIdGenerator == null)
            {
                RaidEncounterDemoLastResult = DemoResultError;
                GD.PrintErr("[RaidEncounterDemo] missing core dependencies (eventBus/time/idGenerator).");

                if (_coreEventBus != null)
                {
                    _ = TryPublishDemoGateDecisionMainThread(
                        decision: SecurityDemoGateDecision.DecisionError,
                        reason: "missing_core_dependencies",
                        target: DemoAuditTarget,
                        caller: caller);
                }

                return;
            }

            var week = _currentTurn?.Week ?? 1;
            if (week < 1)
                week = 1;

            var runner = new RaidEncounterDemoRunner(_coreEventBus, _coreTime, _coreIdGenerator);
            var outcome = runner.Run(week);
            RaidEncounterDemoLastResult = outcome.Result;
            GD.Print($"[RaidEncounterDemo] week={week} result={RaidEncounterDemoLastResult}");

            if (RaidEncounterDemoLastResult == RaidResolved.ResultSuccess && outcome.RewardPoints > 0)
            {
                _demoScore += outcome.RewardPoints;
                _score.Text = $"Score: {_demoScore}";
                ObserveFireAndForget(PublishScoreChangedAsync(score: _demoScore, added: outcome.RewardPoints), "score_changed");
            }

            var decision = RaidEncounterDemoLastResult == DemoResultError
                ? SecurityDemoGateDecision.DecisionError
                : SecurityDemoGateDecision.DecisionAllow;
            var auditReason = decision == SecurityDemoGateDecision.DecisionError
                ? "runner_error"
                : allowReason;

            _ = TryPublishDemoGateDecisionMainThread(
                decision: decision,
                reason: auditReason,
                target: $"{DemoAuditTarget}:week={week}",
                caller: caller);
        }
        catch (Exception ex)
        {
            RaidEncounterDemoLastResult = DemoResultError;
            GD.PrintErr($"[RaidEncounterDemo] failed exType={ex.GetType().Name}");
            if (OS.IsDebugBuild() && string.Equals(OS.GetEnvironment("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal))
                GD.PrintErr(ex.ToString());

            _ = TryPublishDemoGateDecisionMainThread(
                decision: SecurityDemoGateDecision.DecisionError,
                reason: ex.GetType().Name,
                target: DemoAuditTarget,
                caller: $"{nameof(HUD)}.{nameof(TriggerRaidEncounterDemo)}");
        }
        finally
        {
            Interlocked.Exchange(ref _raidEncounterDemoInFlight, 0);
            EmitSignal(SignalName.RaidEncounterDemoCompleted, RaidEncounterDemoLastResult);
        }
    }

    private Task PublishScoreChangedAsync(int score, int added)
    {
        try
        {
            if (_coreEventBus == null)
                return Task.CompletedTask;

            var ts = _coreTime?.UtcNowOffset.UtcDateTime ?? DateTime.UtcNow;
            var id = _coreIdGenerator?.NewId() ?? Guid.NewGuid().ToString("N");
            var evt = new DomainEvent(
                Type: ScoreChanged.EventType,
                Source: DemoAuditSource,
                Data: new ScoreChanged(Score: score, Added: added),
                Timestamp: ts,
                Id: id);

            return _coreEventBus.PublishAsync(evt);
        }
        catch
        {
            return Task.CompletedTask;
        }
    }

    private static (bool Allowed, string Reason) CheckRaidEncounterDemoAllowed()
    {
        var enabled = OS.GetEnvironment("GD_ENABLE_PLAYABLE");
        return (string.Equals(enabled, "1", StringComparison.Ordinal), $"GD_ENABLE_PLAYABLE={enabled ?? "(null)"}");
    }

    private bool TryPublishDemoGateDecisionMainThread(string decision, string reason, string target, string caller)
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

            ObserveFireAndForget(_coreEventBus.PublishAsync(evt), "demo_gate_decision");
            return true;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[HUD] demo gate audit publish failed exType={ex.GetType().Name}");
            GD.PrintErr($"[HUD] demo_gate_audit_publish_failed decision={decision} target={target} caller={caller} reason={reason} exType={ex.GetType().Name}");

            if (OS.IsDebugBuild() && string.Equals(OS.GetEnvironment("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal))
                GD.PrintErr(ex.ToString());

            return false;
        }
    }

    private static void ObserveFireAndForget(Task task, string label)
    {
        try
        {
            _ = task.ContinueWith(
                t =>
                {
                    var ex = t.Exception?.GetBaseException();
                    if (IsFireAndForgetObservationEnabled())
                        Console.Error.WriteLine($"[HUD] publish failed label={label} exType={ex?.GetType().Name ?? "unknown"}");
                },
                TaskContinuationOptions.OnlyOnFaulted);
        }
        catch
        {
            // Best-effort only.
        }
    }

    private static bool IsFireAndForgetObservationEnabled()
    {
        if (OS.IsDebugBuild())
            return true;

        return string.Equals(OS.GetEnvironment("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal) ||
               string.Equals(System.Environment.GetEnvironmentVariable("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal) ||
               string.Equals(System.Environment.GetEnvironmentVariable("CI"), "1", StringComparison.Ordinal) ||
               string.Equals(System.Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);
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

    private static IEventCatalog LoadEventCatalogOrThrow(IEventBus eventBus, ITime timePort)
    {
        if (eventBus is null)
            throw new ArgumentNullException(nameof(eventBus));
        if (timePort is null)
            throw new ArgumentNullException(nameof(timePort));

        var safePath = SafeResourcePath.FromString(EventCatalogPath);
        if (safePath is null || safePath.Type != PathType.ReadOnly)
            throw new InvalidOperationException($"Event catalog path must be a res:// path. path='{EventCatalogPath}'");

        using var file = FileAccess.Open(safePath.Value, FileAccess.ModeFlags.Read);
        var json = file?.GetAsText();
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException($"Event catalog is missing or empty. path='{EventCatalogPath}'");

        var catalogId = "base";
        var schemaVersion = "1";
        try
        {
            using var doc = JsonDocument.Parse(json, JsonOptions);
            if (doc.RootElement.TryGetProperty("catalogId", out var cid) && cid.ValueKind == JsonValueKind.String)
                catalogId = cid.GetString() ?? catalogId;
            if (doc.RootElement.TryGetProperty("schemaVersion", out var sv) && sv.ValueKind == JsonValueKind.String)
                schemaVersion = sv.GetString() ?? schemaVersion;
        }
        catch (JsonException)
        {
            // Metadata parsing is best-effort; the catalog itself must still validate via EventCatalog.FromJson().
        }

        var catalog = EventCatalog.FromJson(json);

        var loadedAt = timePort.UtcNowOffset;
        var evt = new EventCatalogLoaded(
            CatalogId: catalogId,
            SchemaVersion: schemaVersion,
            EventDefinitionCount: catalog.GetEnabledEventTypes().Count,
            EventChainCount: 0,
            LoadedAt: loadedAt);

        _ = eventBus.PublishAsync(new DomainEvent(
            Type: EventCatalogLoaded.EventType,
            Source: nameof(HUD),
            Data: evt,
            Timestamp: loadedAt.UtcDateTime,
            Id: Guid.NewGuid().ToString("N")));

        if (string.Equals(OS.GetEnvironment("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal) ||
            string.Equals(System.Environment.GetEnvironmentVariable("SECURITY_TEST_MODE"), "1", StringComparison.Ordinal))
        {
            GD.Print($"[HUD] EventCatalog loaded catalogId={catalogId} schemaVersion={schemaVersion} enabled={evt.EventDefinitionCount}");
        }

        return catalog;
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
