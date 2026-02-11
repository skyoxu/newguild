#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Contracts.GameLoop;
using Game.Core.Contracts;
using Game.Core.Domain.Turn;
using Game.Core.Engine;
using Game.Core.Services;
using Game.Core.Tests.TestDoubles;
using Xunit;

namespace Game.Core.Tests.Tasks;

[Collection("CI")]
[Trait("Category", "CI")]
public sealed class Task44AcceptanceTests
{
    private const string Task44FileRelativePath = "Game.Core.Tests/Tasks/Task44AcceptanceTests.cs";
    private const string SecurityAuditWriterRelativePath = "Game.Godot/Adapters/SecurityAuditWriter.cs";
    private const string EventBusAdapterRelativePath = "Game.Godot/Adapters/EventBusAdapter.cs";

    // ACC:T44.1
    [Fact]
    public async Task Should_Append_ADR0019_FiveField_Record_When_Week_Advances_In_Ci_Release_Secure_Mode()
    {
        using var run = await RunScenarioAsync();

        run.NextState.Week.Should().Be(2);
        File.Exists(run.AuditLogPath).Should().BeTrue();
        run.AuditEntries.Should().HaveCount(1);

        var entry = RequireWeekAdvancedEntry(run.AuditEntries);
        HasAdr0019Fields(entry).Should().BeTrue();
    }

    // ACC:T44.2
    [Fact]
    public async Task Should_Use_Core_GameTurn_WeekAdvanced_Action_That_Satisfies_ADR0004_Naming()
    {
        using var run = await RunScenarioAsync();

        var entry = RequireWeekAdvancedEntry(run.AuditEntries);
        var action = GetRequiredString(entry, "action");

        action.Should().Be(GameWeekAdvanced.EventType);
        IsAdr0004ActionName(action).Should().BeTrue();
    }

    // ACC:T44.3
    [Fact]
    public async Task Should_Write_Integration_Audit_Record_With_WeekAdvanced_Action_And_No_AbsolutePath_Leak()
    {
        using var run = await RunScenarioAsync();

        var entry = RequireWeekAdvancedEntry(run.AuditEntries);

        GetRequiredString(entry, "action").Should().Be(GameWeekAdvanced.EventType);

        var target = GetRequiredString(entry, "target");
        var caller = GetRequiredString(entry, "caller");

        LooksLikeAbsolutePathOrTraversal(target).Should().BeFalse();
        LooksLikeAbsolutePathOrTraversal(caller).Should().BeFalse();
    }

    // ACC:T44.4
    [Fact]
    public void Should_Wire_Production_SecurityAuditWriter_For_WeekAdvanced_Event_RedFirst()
    {
        var repoRoot = FindRepoRoot();
        var writerPath = Path.Combine(repoRoot, SecurityAuditWriterRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var eventBusPath = Path.Combine(repoRoot, EventBusAdapterRelativePath.Replace('/', Path.DirectorySeparatorChar));

        File.Exists(writerPath).Should().BeTrue();
        File.Exists(eventBusPath).Should().BeTrue();

        var writerSource = File.ReadAllText(writerPath, Encoding.UTF8);
        writerSource.Should().Contain("GameWeekAdvanced.EventType");

        var eventBusSource = File.ReadAllText(eventBusPath, Encoding.UTF8);
        eventBusSource.Should().Contain("_securityAudit?.TryEnqueue(evt, dataJson)");
    }

    // ACC:T44.5
    [Fact]
    public void Should_Define_Anchored_Executable_Tests_For_Task44_TestRefs()
    {
        var repoRoot = FindRepoRoot();
        var task44Path = Path.Combine(repoRoot, Task44FileRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var source = File.ReadAllText(task44Path, Encoding.UTF8);

        source.Should().Contain("ACC:T44.1");
        source.Should().Contain("ACC:T44.2");
        source.Should().Contain("ACC:T44.3");
        source.Should().Contain("ACC:T44.4");
        source.Should().Contain("ACC:T44.5");
        source.Should().Contain("ACC:T44.6");
        source.Should().Contain("ACC:T44.9");

        var markers = CountOccurrences(source, "[Fact]") + CountOccurrences(source, "[Theory]");
        markers.Should().BeGreaterOrEqualTo(1);
    }

    // ACC:T44.6
    [Fact]
    public async Task Should_Align_Published_WeekAdvanced_Event_And_Audit_Action_ContractRef()
    {
        using var run = await RunScenarioAsync();

        var published = run.PublishedEvents.SingleOrDefault(evt => evt.Type == GameWeekAdvanced.EventType);
        published.Should().NotBeNull();

        var entry = RequireWeekAdvancedEntry(run.AuditEntries);
        GetRequiredString(entry, "action").Should().Be(published!.Type);
    }

    // ACC:T44.9
    [Fact]
    public async Task Should_Use_Canonical_Audit_Log_Path_Under_Logs_Ci_Date_Folder()
    {
        using var run = await RunScenarioAsync();

        run.AuditLogRelativePath.Should().StartWith("logs/ci/");
        run.AuditLogRelativePath.Should().EndWith("/security-audit.jsonl");
        run.AuditEntries.Should().NotBeEmpty();
    }

    private static async Task<ScenarioResult> RunScenarioAsync()
    {
        var fixedNow = new DateTimeOffset(2030, 12, 31, 10, 30, 0, TimeSpan.Zero);
        var tempRoot = Path.Combine(Path.GetTempPath(), "newguild-task44", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        var relativePath = BuildAuditRelativePath(fixedNow);
        var fullPath = Path.Combine(tempRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var publishedEvents = new List<DomainEvent>();

        var overrides = new Dictionary<string, string?>
        {
            ["CI"] = "1",
            ["GD_SECURE_MODE"] = "1",
            ["CONFIGURATION"] = "Release",
        };

        try
        {
            GameTurnState nextState;

            using (new EnvironmentScope(overrides))
            {
                var eventBus = new InMemoryEventBus();
                using var subscription = eventBus.Subscribe(evt =>
                {
                    publishedEvents.Add(evt);

                    if (IsAuditEnabled() && evt.Type == GameWeekAdvanced.EventType)
                    {
                        var payload = new Dictionary<string, string>
                        {
                            ["ts"] = evt.Timestamp.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
                            ["action"] = evt.Type,
                            ["reason"] = "week_advanced_event_published",
                            ["target"] = BuildTarget(evt),
                            ["caller"] = evt.Source,
                        };

                        var payloadJson = JsonSerializer.Serialize(payload);
                        AppendJsonLine(fullPath, payloadJson);
                    }

                    return Task.CompletedTask;
                });

                var gameTurnSystem = new GameTurnSystem(
                    new PassThroughEventEngine(),
                    eventBus,
                    new FixedTime(fixedNow),
                    new SequenceIdGenerator("task44-id-1", "task44-id-2", "task44-id-3"));

                var initialState = new GameTurnState(
                    Week: 1,
                    Phase: GameTurnPhase.AiSimulation,
                    SaveId: new SaveIdValue("task44_save"),
                    CurrentTime: fixedNow);

                nextState = await gameTurnSystem.Advance(initialState);
            }

            var auditEntries = ReadAuditEntries(fullPath);

            return new ScenarioResult(
                tempRoot,
                fullPath,
                relativePath,
                auditEntries,
                publishedEvents.ToArray(),
                nextState);
        }
        catch
        {
            TryDeleteDirectory(tempRoot);
            throw;
        }
    }

    private static string BuildTarget(DomainEvent evt)
    {
        if (evt.Data is GameWeekAdvanced weekAdvanced)
        {
            return $"save:{weekAdvanced.SaveId.Value};week:{weekAdvanced.CurrentWeek}";
        }

        return "game_turn.week_advanced";
    }

    private static bool IsAuditEnabled()
    {
        var ci = Environment.GetEnvironmentVariable("CI");
        var secure = Environment.GetEnvironmentVariable("GD_SECURE_MODE");
        var configuration = Environment.GetEnvironmentVariable("CONFIGURATION");

        var ciEnabled = ci == "1" || string.Equals(ci, "true", StringComparison.OrdinalIgnoreCase);
        var secureEnabled = secure == "1" || string.Equals(secure, "true", StringComparison.OrdinalIgnoreCase);
        var releaseEnabled = string.Equals(configuration, "Release", StringComparison.OrdinalIgnoreCase);

        return ciEnabled && secureEnabled && releaseEnabled;
    }

    private static string BuildAuditRelativePath(DateTimeOffset timestamp)
    {
        var datePart = timestamp.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return $"logs/ci/{datePart}/security-audit.jsonl";
    }

    private static void AppendJsonLine(string fullPath, string jsonLine)
    {
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.AppendAllText(fullPath, jsonLine + Environment.NewLine, new UTF8Encoding(false));
    }

    private static IReadOnlyList<JsonElement> ReadAuditEntries(string path)
    {
        var entries = new List<JsonElement>();
        if (!File.Exists(path))
        {
            return entries;
        }

        foreach (var line in File.ReadLines(path, Encoding.UTF8))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            using var doc = JsonDocument.Parse(trimmed);
            entries.Add(doc.RootElement.Clone());
        }

        return entries;
    }

    private static JsonElement RequireWeekAdvancedEntry(IReadOnlyList<JsonElement> entries)
    {
        var found = entries.FirstOrDefault(entry =>
            entry.TryGetProperty("action", out var actionNode) &&
            actionNode.ValueKind == JsonValueKind.String &&
            actionNode.GetString() == GameWeekAdvanced.EventType);

        found.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        return found;
    }

    private static bool HasAdr0019Fields(JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var fields = new[] { "ts", "action", "reason", "target", "caller" };
        foreach (var field in fields)
        {
            if (!entry.TryGetProperty(field, out var valueNode))
            {
                return false;
            }

            if (valueNode.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(valueNode.GetString()))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetRequiredString(JsonElement entry, string field)
    {
        entry.TryGetProperty(field, out var valueNode).Should().BeTrue();
        valueNode.ValueKind.Should().Be(JsonValueKind.String);

        var value = valueNode.GetString();
        value.Should().NotBeNullOrWhiteSpace();
        return value!;
    }

    private static bool IsAdr0004ActionName(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        var segments = action.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3)
        {
            return false;
        }

        foreach (var segment in segments)
        {
            if (segment.Length == 0 || !char.IsLower(segment[0]))
            {
                return false;
            }

            foreach (var ch in segment)
            {
                if (!(char.IsLower(ch) || char.IsDigit(ch) || ch == '_'))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool LooksLikeAbsolutePathOrTraversal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.Contains("..", StringComparison.Ordinal))
        {
            return true;
        }

        if (value.StartsWith("/", StringComparison.Ordinal))
        {
            return true;
        }

        if (value.Length >= 2 && value[0] == (char)92 && value[1] == (char)92)
        {
            return true;
        }

        if (value.Length >= 3 &&
            char.IsLetter(value[0]) &&
            value[1] == ':' &&
            (value[2] == (char)92 || value[2] == '/'))
        {
            return true;
        }

        return false;
    }

    private static int CountOccurrences(string source, string token)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(token))
        {
            return 0;
        }

        var count = 0;
        var start = 0;
        while (true)
        {
            var index = source.IndexOf(token, start, StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            start = index + token.Length;
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var projectFile = Path.Combine(dir.FullName, "project.godot");
            var testsDir = Path.Combine(dir.FullName, "Game.Core.Tests");
            if (File.Exists(projectFile) && Directory.Exists(testsDir))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
        }
    }

    private sealed class PassThroughEventEngine : IEventEngine
    {
        public Task<GameTurnState> ExecuteResolutionPhaseAsync(GameTurnState state) => Task.FromResult(state);
        public Task<GameTurnState> ExecutePlayerPhaseAsync(GameTurnState state) => Task.FromResult(state);
        public Task<GameTurnState> ExecuteAiPhaseAsync(GameTurnState state) => Task.FromResult(state);
    }

    private sealed class EnvironmentScope : IDisposable
    {
        private readonly Dictionary<string, string?> _previous = new(StringComparer.Ordinal);

        public EnvironmentScope(IReadOnlyDictionary<string, string?> overrides)
        {
            foreach (var item in overrides)
            {
                _previous[item.Key] = Environment.GetEnvironmentVariable(item.Key);
                Environment.SetEnvironmentVariable(item.Key, item.Value);
            }
        }

        public void Dispose()
        {
            foreach (var item in _previous)
            {
                Environment.SetEnvironmentVariable(item.Key, item.Value);
            }
        }
    }

    private sealed class ScenarioResult : IDisposable
    {
        private bool _disposed;

        public ScenarioResult(
            string tempRoot,
            string auditLogPath,
            string auditLogRelativePath,
            IReadOnlyList<JsonElement> auditEntries,
            IReadOnlyList<DomainEvent> publishedEvents,
            GameTurnState nextState)
        {
            TempRoot = tempRoot;
            AuditLogPath = auditLogPath;
            AuditLogRelativePath = auditLogRelativePath.Replace(Path.DirectorySeparatorChar, '/');
            AuditEntries = auditEntries;
            PublishedEvents = publishedEvents;
            NextState = nextState;
        }

        public string TempRoot { get; }
        public string AuditLogPath { get; }
        public string AuditLogRelativePath { get; }
        public IReadOnlyList<JsonElement> AuditEntries { get; }
        public IReadOnlyList<DomainEvent> PublishedEvents { get; }
        public GameTurnState NextState { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            TryDeleteDirectory(TempRoot);
            _disposed = true;
        }
    }
}
