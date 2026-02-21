using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Game.Core.Contracts.Engine;
using Game.Core.Contracts.Guild;
using Game.Core.Contracts.Media;
using Game.Core.Contracts.Persistence;
using Game.Core.Contracts.Progression;
using Game.Core.Contracts.Raid;
using Game.Core.Contracts.Recruitment;
using Game.Core.Contracts.Security;
using Xunit;

namespace Game.Core.Tests.Tasks;

public sealed class Task49GovernanceTests
{
    // ACC:T49.6
    [Fact]
    public void Should_Validate_Task49_TestRefs_Are_Consistent_And_Executable()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(repositoryRoot));

        var refsByView = GetTaskViewFiles(repositoryRoot)
            .ToDictionary(
                viewFile => Path.GetFileName(viewFile),
                viewFile => LoadTask49FieldValues(viewFile, "test_refs"));

        refsByView.Values.Should().OnlyContain(values => values.Count >= 3);
        refsByView.Values.Should().OnlyContain(values => values.Distinct(StringComparer.Ordinal).Count() == values.Count);

        var referenceList = refsByView.First().Value;
        foreach (var entry in refsByView)
        {
            entry.Value.Should().Equal(referenceList, because: $"test_refs drift in {entry.Key}");
        }

        foreach (var testRef in referenceList)
        {
            Path.IsPathRooted(testRef).Should().BeFalse($"test ref must be relative: {testRef}");

            var normalizedRefPath = Path.GetFullPath(Path.Combine(repositoryRoot, testRef));
            normalizedRefPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                .Should()
                .BeTrue($"test ref must stay under repository root: {testRef}");

            File.Exists(normalizedRefPath).Should().BeTrue($"test ref must exist on disk: {testRef}");

            var content = File.ReadAllText(normalizedRefPath);
            var hasCsTestDeclaration = Regex.IsMatch(content, @"(?m)^\s*\[(Fact|Theory)(?:\([^\]]*\))?\]\s*$");
            var hasGdTestDeclaration = Regex.IsMatch(content, @"(?m)^\s*func\s+test_[A-Za-z0-9_]+\s*\(");
            (hasCsTestDeclaration || hasGdTestDeclaration).Should().BeTrue($"test ref must point to executable tests: {testRef}");
        }
    }

    // ACC:T49.4 ACC:T49.7
    [Fact]
    public void Should_Keep_Task49_ContractRefs_Equivalent_To_Domain_Event_Contracts()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var expectedEventTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            ExperienceChanged.EventType,
            LevelChanged.EventType,
            RaidResolved.EventType,
            GuildCreated.EventType,
            MediaBeatTriggered.EventType,
            RecruitmentOfferResolved.EventType,
            ReputationChanged.EventType,
            ScoreChanged.EventType,
            SaveRequested.EventType,
            LoadRequested.EventType,
            SecuritySnapshotGateDecision.EventType
        };

        foreach (var viewFile in GetTaskViewFiles(repositoryRoot))
        {
            var contractRefs = LoadTask49FieldValues(viewFile, "contractRefs");
            contractRefs.Should().BeEquivalentTo(expectedEventTypes, because: $"contractRefs drift in {Path.GetFileName(viewFile)}");
        }
    }

    // ACC:T49.10
    [Fact]
    public void Should_Create_And_Read_Route14_Artifact_Json_Using_Task_ArtifactRefs_Template()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(repositoryRoot));

        var artifactRefsByView = GetTaskViewFiles(repositoryRoot)
            .ToDictionary(
                viewFile => Path.GetFileName(viewFile),
                viewFile => LoadTask49FieldValues(viewFile, "artifactRefs"));

        artifactRefsByView.Values.Should().OnlyContain(values => values.Count >= 1);
        var baselineArtifactRefs = artifactRefsByView.First().Value;
        foreach (var entry in artifactRefsByView)
        {
            entry.Value.Should().Equal(baselineArtifactRefs, because: $"artifactRefs drift in {entry.Key}");
        }

        baselineArtifactRefs.Count.Should().Be(1, "artifactRefs must stay single-source for Task49 route14");
        var artifactTemplate = baselineArtifactRefs[0];
        artifactTemplate.Should().Contain("<date>", "artifactRefs must be date-parameterized");

        var dateToken = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var relativeArtifactPath = artifactTemplate.Replace("<date>", dateToken, StringComparison.Ordinal);
        Path.IsPathRooted(relativeArtifactPath).Should().BeFalse();
        var normalizedRelativeArtifactPath = relativeArtifactPath.Replace('\\', '/').TrimStart('/');
        Regex.IsMatch(normalizedRelativeArtifactPath, @"^logs/e2e/[0-9]{4}-[0-9]{2}-[0-9]{2}/playability-route14-summary\.json$").Should().BeTrue();

        var artifactPath = Path.GetFullPath(Path.Combine(repositoryRoot, relativeArtifactPath));
        artifactPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase).Should().BeTrue();

        var artifactDirectory = Path.GetDirectoryName(artifactPath);
        artifactDirectory.Should().NotBeNullOrWhiteSpace();

        var fileName = Path.GetFileName(artifactPath);
        fileName.Should().Be("playability-route14-summary.json");

        Path.GetExtension(artifactPath).Should().Be(".json");

        var normalizedDirectory = artifactDirectory!.Replace('\\', '/').TrimStart('/');
        Regex.IsMatch(normalizedDirectory, @"^.+/logs/e2e/[0-9]{4}-[0-9]{2}-[0-9]{2}$").Should().BeTrue();

        var payload = new
        {
            route = 14,
            status = "ok",
            xpTotal = 120,
            level = 2,
            eventTypes = new[]
            {
                ExperienceChanged.EventType,
                LevelChanged.EventType,
                RaidResolved.EventType
            }
        };

        var isolatedRoot = Path.Combine(Path.GetTempPath(), "newguild-task49-governance", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolatedRoot);
        var isolatedArtifactPath = Path.GetFullPath(Path.Combine(isolatedRoot, relativeArtifactPath));
        var isolatedRootNormalized = EnsureTrailingSeparator(Path.GetFullPath(isolatedRoot));
        isolatedArtifactPath.StartsWith(isolatedRootNormalized, StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        var isolatedArtifactDirectory = Path.GetDirectoryName(isolatedArtifactPath) ?? throw new InvalidOperationException("isolated artifact directory missing");
        Directory.CreateDirectory(isolatedArtifactDirectory);

        try
        {
            var serialized = JsonSerializer.Serialize(payload);
            using (var stream = new FileStream(isolatedArtifactPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.Write(serialized);
            }

            var savedContent = File.ReadAllText(isolatedArtifactPath);
            using var document = JsonDocument.Parse(savedContent);

            document.RootElement.GetProperty("route").GetInt32().Should().Be(14);
            document.RootElement.GetProperty("status").GetString().Should().Be("ok");
            document.RootElement.GetProperty("xpTotal").GetInt32().Should().Be(120);
            document.RootElement.GetProperty("level").GetInt32().Should().Be(2);

            var eventTypes = document.RootElement
                .GetProperty("eventTypes")
                .EnumerateArray()
                .Select(item => item.GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.Ordinal);

            eventTypes.Should().BeEquivalentTo(new[]
            {
                ExperienceChanged.EventType,
                LevelChanged.EventType,
                RaidResolved.EventType
            });
        }
        finally
        {
            if (Directory.Exists(isolatedRoot))
            {
                Directory.Delete(isolatedRoot, recursive: true);
            }
        }
    }

    private static IReadOnlyList<string> GetTaskViewFiles(string repositoryRoot)
    {
        return new[]
        {
            Path.Combine(repositoryRoot, ".taskmaster", "tasks", "tasks.json"),
            Path.Combine(repositoryRoot, ".taskmaster", "tasks", "tasks_back.json"),
            Path.Combine(repositoryRoot, ".taskmaster", "tasks", "tasks_gameplay.json")
        };
    }

    private static IReadOnlyList<string> LoadTask49FieldValues(string viewFile, string fieldName)
    {
        using var stream = File.OpenRead(viewFile);
        using var document = JsonDocument.Parse(stream);

        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!item.TryGetProperty("taskmaster_id", out var taskmasterId) ||
                    taskmasterId.ValueKind != JsonValueKind.Number ||
                    taskmasterId.GetInt32() != 49)
                {
                    continue;
                }

                if (!item.TryGetProperty(fieldName, out var valuesElement) || valuesElement.ValueKind != JsonValueKind.Array)
                {
                    return Array.Empty<string>();
                }

                return valuesElement
                    .EnumerateArray()
                    .Where(value => value.ValueKind == JsonValueKind.String)
                    .Select(value => value.GetString() ?? string.Empty)
                    .ToList();
            }

            throw new InvalidDataException($"Taskmaster task 49 not found in view file: {viewFile}");
        }

        if (document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty("master", out var masterElement) &&
            masterElement.ValueKind == JsonValueKind.Object &&
            masterElement.TryGetProperty("tasks", out var tasksElement) &&
            tasksElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var task in tasksElement.EnumerateArray())
            {
                if (task.ValueKind != JsonValueKind.Object ||
                    !task.TryGetProperty("id", out var idElement))
                {
                    continue;
                }

                var isTask49 = idElement.ValueKind == JsonValueKind.String
                    ? string.Equals(idElement.GetString(), "49", StringComparison.Ordinal)
                    : idElement.ValueKind == JsonValueKind.Number && idElement.GetInt32() == 49;
                if (!isTask49)
                {
                    continue;
                }

                var normalizedFieldName = fieldName == "test_refs"
                    ? "testRefs"
                    : fieldName;
                if (!task.TryGetProperty(normalizedFieldName, out var valuesElement) || valuesElement.ValueKind != JsonValueKind.Array)
                {
                    return Array.Empty<string>();
                }

                return valuesElement
                    .EnumerateArray()
                    .Where(value => value.ValueKind == JsonValueKind.String)
                    .Select(value => value.GetString() ?? string.Empty)
                    .ToList();
            }

            throw new InvalidDataException($"Taskmaster task 49 not found in master tasks file: {viewFile}");
        }

        throw new InvalidDataException($"Unsupported task file structure: {viewFile}");
    }

    private static string ResolveRepositoryRoot()
    {
        var environmentRoot = Environment.GetEnvironmentVariable("REPO_ROOT");
        if (!string.IsNullOrWhiteSpace(environmentRoot))
        {
            var normalizedEnvironmentRoot = Path.GetFullPath(environmentRoot);
            if (File.Exists(Path.Combine(normalizedEnvironmentRoot, "Game.sln")) &&
                Directory.Exists(Path.Combine(normalizedEnvironmentRoot, ".taskmaster", "tasks")))
            {
                return normalizedEnvironmentRoot;
            }
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Game.sln")) &&
                Directory.Exists(Path.Combine(current.FullName, ".taskmaster", "tasks")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Cannot locate repository root for Task49 governance tests.");
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
