using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Contracts;
using Game.Core.Contracts.Achievements;
using Game.Core.Contracts.Guild;
using Game.Core.Domain.Achievements;
using Game.Core.Services;
using Xunit;

namespace Game.Core.Tests.Tasks;

public sealed class Task50ProcessRestartAcceptanceTests
{
    private const string SaveId = "t50-process-restart";
    private const string EnvStateFile = "TASK50_STATE_FILE";
    private const string EnvSaveId = "TASK50_SAVE_ID";

    // ACC:T50.4
    [Fact]
    public async Task ShouldRestoreUnlockedCount_WhenProcessRestarts()
    {
        var repoRoot = FindRepoRoot();
        var artifactDir = BuildArtifactDirectory(repoRoot);
        var stateFile = Path.Combine(artifactDir, "task50-achievement-state.json");

        Directory.CreateDirectory(artifactDir);
        if (File.Exists(stateFile))
            File.Delete(stateFile);

        var store = new FileAchievementStateStore(stateFile);
        var eventBus = new InMemoryEventBus();

        using (var tracker = new AchievementTracker(eventBus, store, SaveId))
        {
            await eventBus.PublishAsync(BuildEvent(GuildCreated.EventType));
            tracker.UnlockedCount.Should().Be(1);
        }

        var childResult = RunDotnetTest(
            repoRoot,
            "Game.Core.Tests/Game.Core.Tests.csproj",
            "FullyQualifiedName=Game.Core.Tests.Tasks.Task50ProcessRestartProbeTests.ShouldLoadPersistedState_WhenProcessRestarts",
            (EnvStateFile, stateFile),
            (EnvSaveId, SaveId));

        childResult.ExitCode.Should().Be(0, childResult.ToString());

        var persistedJson = File.ReadAllText(stateFile, Encoding.UTF8);
        persistedJson.Should().Contain("\"schemaVersion\":1");
    }

    // ACC:T50.4
    [Fact]
    public void ShouldRestoreUnlockedCount_WhenProcessRestartsWithLegacySchemaPayload()
    {
        var repoRoot = FindRepoRoot();
        var artifactDir = BuildArtifactDirectory(repoRoot);
        var stateFile = Path.Combine(artifactDir, "task50-achievement-state-legacy.json");

        Directory.CreateDirectory(artifactDir);
        var legacyJson = "{\"saveId\":\"t50-process-restart\",\"unlockedCount\":1,\"unlockedTriggerEventTypes\":[\"core.guild.created\"]}";
        File.WriteAllText(stateFile, legacyJson, Encoding.UTF8);

        var childResult = RunDotnetTest(
            repoRoot,
            "Game.Core.Tests/Game.Core.Tests.csproj",
            "FullyQualifiedName=Game.Core.Tests.Tasks.Task50ProcessRestartProbeTests.ShouldLoadPersistedState_WhenProcessRestarts",
            (EnvStateFile, stateFile),
            (EnvSaveId, SaveId));

        childResult.ExitCode.Should().Be(0, childResult.ToString());
    }

    private static DomainEvent BuildEvent(string eventType) =>
        new(
            eventType,
            "task50.acceptance",
            "{}",
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"));

    private static string BuildArtifactDirectory(string repoRoot)
    {
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        return Path.Combine(repoRoot, "logs", "unit", date, "task50-process-restart");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var sentinel = Path.Combine(dir.FullName, "project.godot");
            if (File.Exists(sentinel))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private static ProcessResult RunDotnetTest(
        string repoRoot,
        string projectPath,
        string filter,
        params (string Key, string Value)[] environment)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.ArgumentList.Add("test");
        psi.ArgumentList.Add(projectPath);
        psi.ArgumentList.Add("--no-build");
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("Debug");
        psi.ArgumentList.Add("--filter");
        psi.ArgumentList.Add(filter);
        psi.ArgumentList.Add("-v");
        psi.ArgumentList.Add("minimal");

        foreach (var (key, value) in environment)
            psi.Environment[key] = value;

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(180_000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            throw new TimeoutException($"dotnet test timed out (filter={filter}).");
        }

        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();

        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    private readonly record struct ProcessResult(int ExitCode, string StdOut, string StdErr)
    {
        public override string ToString()
        {
            static string Trim(string value)
            {
                const int maxChars = 4000;
                if (value.Length <= maxChars)
                    return value;

                return value[..maxChars] + "\n...<truncated>";
            }

            return $"ExitCode={ExitCode}\nSTDOUT:\n{Trim(StdOut)}\nSTDERR:\n{Trim(StdErr)}";
        }
    }
}

public sealed class Task50ProcessRestartProbeTests
{
    // ACC:T50.4
    [Fact]
    public void ShouldLoadPersistedState_WhenProcessRestarts()
    {
        var stateFile = Environment.GetEnvironmentVariable("TASK50_STATE_FILE");
        var saveId = Environment.GetEnvironmentVariable("TASK50_SAVE_ID");

        if (string.IsNullOrWhiteSpace(stateFile) || string.IsNullOrWhiteSpace(saveId))
            return;

        var store = new FileAchievementStateStore(stateFile);
        var eventBus = new InMemoryEventBus();
        using var tracker = new AchievementTracker(eventBus, store, saveId);

        tracker.UnlockedCount.Should().Be(1);
    }
}

internal sealed class FileAchievementStateStore : IAchievementStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly string _filePath;
    private readonly object _gate = new();

    public FileAchievementStateStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("filePath cannot be null or whitespace.", nameof(filePath));

        _filePath = filePath;
    }

    public Task<AchievementStateSnapshot?> LoadAsync(string saveId)
    {
        lock (_gate)
        {
            if (!File.Exists(_filePath))
                return Task.FromResult<AchievementStateSnapshot?>(null);

            var json = File.ReadAllText(_filePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
                return Task.FromResult<AchievementStateSnapshot?>(null);

            var payload = JsonSerializer.Deserialize<PersistedState>(json, JsonOptions);
            if (payload == null || !string.Equals(payload.SaveId, saveId, StringComparison.Ordinal))
                return Task.FromResult<AchievementStateSnapshot?>(null);

            if (!AchievementStateSnapshotMigration.TryMigrateToCurrent(
                    payload.SchemaVersion,
                    payload.UnlockedTriggerEventTypes,
                    out var snapshot))
            {
                return Task.FromResult<AchievementStateSnapshot?>(null);
            }

            return Task.FromResult<AchievementStateSnapshot?>(snapshot);
        }
    }

    public Task SaveAsync(string saveId, AchievementStateSnapshot snapshot)
    {
        lock (_gate)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            if (!AchievementStateSnapshotMigration.TryMigrateToCurrent(
                    snapshot.SchemaVersion,
                    snapshot.UnlockedTriggerEventTypes,
                    out var normalizedSnapshot))
            {
                throw new InvalidOperationException($"Unsupported achievement snapshot schemaVersion={snapshot.SchemaVersion}.");
            }

            var payload = new PersistedState
            {
                SchemaVersion = AchievementStateSnapshot.CurrentSchemaVersion,
                SaveId = saveId,
                UnlockedCount = normalizedSnapshot.UnlockedCount,
                UnlockedTriggerEventTypes = normalizedSnapshot.UnlockedTriggerEventTypes,
            };

            var json = JsonSerializer.Serialize(payload, JsonOptions);
            File.WriteAllText(_filePath, json, Encoding.UTF8);
        }

        return Task.CompletedTask;
    }

    private sealed class PersistedState
    {
        public int SchemaVersion { get; set; }

        public string SaveId { get; set; } = string.Empty;

        public int UnlockedCount { get; set; }

        public System.Collections.Generic.IReadOnlyList<string> UnlockedTriggerEventTypes { get; set; } =
            Array.Empty<string>();
    }
}
