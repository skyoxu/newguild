using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.Tasks;

public sealed class Task52GovernanceTests
{
    // ACC:T52.3
    [Fact]
    public void ShouldAlignContractAndArtifactRefs_WhenComparingTask52MasterAndView()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var masterTask = LoadMasterTask52(Path.Combine(repositoryRoot, ".taskmaster", "tasks", "tasks.json"));
        var viewTask = LoadViewTask52(Path.Combine(repositoryRoot, ".taskmaster", "tasks", "tasks_back.json"));

        masterTask.ContractRefs.Should().NotBeEmpty("governance consistency requires contractRefs in tasks.json for Task 52");
        masterTask.ArtifactRefs.Should().NotBeEmpty("governance consistency requires artifactRefs in tasks.json for Task 52");

        masterTask.ContractRefs.Should().Equal(viewTask.ContractRefs, because: "Task/View governance must keep contract references aligned");
        masterTask.ArtifactRefs.Should().Equal(viewTask.ArtifactRefs, because: "Task/View governance must keep gate evidence references aligned");
    }

    // ACC:T52.2
    [Fact]
    public void ShouldValidateRefsIntegrityAndContractBaseline_WhenCheckingT44ToT51AcrossViews()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var masterTasks = LoadMasterTasks(Path.Combine(repositoryRoot, ".taskmaster", "tasks", "tasks.json"))
            .Where(task => task.TaskmasterId >= 44 && task.TaskmasterId <= 51)
            .ToList();
        var backTasks = LoadViewTasks(Path.Combine(repositoryRoot, ".taskmaster", "tasks", "tasks_back.json"))
            .Where(task => task.TaskmasterId >= 44 && task.TaskmasterId <= 51)
            .ToList();
        var gameplayTasks = LoadViewTasks(Path.Combine(repositoryRoot, ".taskmaster", "tasks", "tasks_gameplay.json"))
            .Where(task => task.TaskmasterId >= 44 && task.TaskmasterId <= 51)
            .ToList();
        var viewTasks = backTasks.Concat(gameplayTasks).ToList();

        foreach (var taskId in Enumerable.Range(44, 8))
        {
            masterTasks.Should().ContainSingle(
                task => task.TaskmasterId == taskId,
                $"master task {taskId} must exist for governance range T44-T51");

            var masterTask = masterTasks.Single(task => task.TaskmasterId == taskId);
            AssertMasterRefsAreParsable(masterTask, repositoryRoot);

            var viewCandidates = viewTasks.Where(task => task.TaskmasterId == taskId).ToList();
            viewCandidates.Should().NotBeEmpty(
                $"at least one view task entry must exist for task {taskId} across tasks_back/tasks_gameplay");

            foreach (var viewTask in viewCandidates)
            {
                AssertViewRefsAreParsable(viewTask, repositoryRoot);
            }
        }

        var baselineContractRefs = viewTasks
            .SelectMany(task => task.ContractRefs)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);

        baselineContractRefs.Should().NotBeEmpty("baseline governance set must be derived from existing tasks");

        var task52 = LoadViewTask52(Path.Combine(repositoryRoot, ".taskmaster", "tasks", "tasks_back.json"));
        var unexpectedRefs = task52.ContractRefs
            .Where(contractRef => !baselineContractRefs.Contains(contractRef))
            .ToArray();

        unexpectedRefs.Should().BeEmpty("governance task must not introduce new gameplay contracts");
    }

    // ACC:T52.1
    [Fact]
    public void ShouldMapBackLinkMetadata_WhenTask52MasterAndViewIdsCompared()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var masterTask = LoadMasterTask52(Path.Combine(repositoryRoot, ".taskmaster", "tasks", "tasks.json"));
        var viewTask = LoadViewTask52(Path.Combine(repositoryRoot, ".taskmaster", "tasks", "tasks_back.json"));

        masterTask.Id.Should().Be("52");
        viewTask.TaskmasterId.Should().Be(52);
        masterTask.Title.Should().Be(viewTask.Title);
        masterTask.Description.Should().Be(viewTask.Description);

        masterTask.AdrRefs.Should().Equal(viewTask.AdrRefs);
        masterTask.ChapterRefs.Should().Equal(viewTask.ChapterRefs);
        masterTask.TestRefs.Should().Equal(viewTask.TestRefs);
        viewTask.OverlayRefs.Should().Contain(masterTask.OverlayRef);
    }

    // ACC:T52.4
    [Fact]
    public void ShouldResolveGovernanceEvidenceFiles_WhenTask52ArtifactsAreConfigured()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var masterTask = LoadMasterTask52(Path.Combine(repositoryRoot, ".taskmaster", "tasks", "tasks.json"));
        var viewTask = LoadViewTask52(Path.Combine(repositoryRoot, ".taskmaster", "tasks", "tasks_back.json"));

        masterTask.ArtifactRefs.Should().Contain("logs/ci/<date>/sc-acceptance-check-task-52/summary.json");
        masterTask.ArtifactRefs.Should().Contain("logs/ci/<date>/sc-acceptance-check-task-52/task-links-validate-summary.json");
        viewTask.ArtifactRefs.Should().Equal(masterTask.ArtifactRefs);

        var tempRoot = Directory.CreateTempSubdirectory("task52-evidence-");
        try
        {
            var syntheticDate = "2099-12-31";
            var summaryPath = Path.Combine(
                tempRoot.FullName,
                "logs",
                "ci",
                syntheticDate,
                "sc-acceptance-check-task-52",
                "summary.json");
            var taskLinksPath = Path.Combine(
                tempRoot.FullName,
                "logs",
                "ci",
                syntheticDate,
                "sc-acceptance-check-task-52",
                "task-links-validate-summary.json");

            Directory.CreateDirectory(Path.GetDirectoryName(summaryPath) ?? throw new InvalidDataException("summary parent is null"));
            File.WriteAllText(summaryPath, "{\"status\":\"ok\",\"task_id\":\"52\"}");
            File.WriteAllText(taskLinksPath, "{\"status\":\"ok\"}");

            var resolvedEvidence = ResolveGovernanceEvidenceFiles(tempRoot.FullName, masterTask.ArtifactRefs);
            resolvedEvidence.Should().Contain(summaryPath);
            resolvedEvidence.Should().Contain(taskLinksPath);

            using var summaryDocument = JsonDocument.Parse(File.ReadAllText(summaryPath));
            summaryDocument.RootElement.GetProperty("status").GetString().Should().Be("ok");
            summaryDocument.RootElement.GetProperty("task_id").GetString().Should().Be("52");
        }
        finally
        {
            if (tempRoot.Exists)
            {
                tempRoot.Delete(recursive: true);
            }
        }
    }

    [Fact]
    public void ShouldThrowInvalidDataException_WhenTask52EvidenceFilesAreMissing()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var masterTask = LoadMasterTask52(Path.Combine(repositoryRoot, ".taskmaster", "tasks", "tasks.json"));
        var tempRoot = Directory.CreateTempSubdirectory("task52-evidence-missing-");

        try
        {
            var act = () => ResolveGovernanceEvidenceFiles(tempRoot.FullName, masterTask.ArtifactRefs);
            act.Should().Throw<InvalidDataException>();
        }
        finally
        {
            if (tempRoot.Exists)
            {
                tempRoot.Delete(recursive: true);
            }
        }
    }

    [Fact]
    public void ShouldThrowInvalidDataException_WhenViewTasksShapeUnsupported()
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempPath, "{\"unexpected\":true}");
            var act = () => LoadViewTasks(tempPath);
            act.Should().Throw<InvalidDataException>();
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static MasterTaskRecord LoadMasterTask52(string masterTasksPath)
    {
        return LoadMasterTasks(masterTasksPath).Single(task => task.TaskmasterId == 52);
    }

    private static IReadOnlyList<MasterTaskRecord> LoadMasterTasks(string masterTasksPath)
    {
        using var stream = File.OpenRead(masterTasksPath);
        using var document = JsonDocument.Parse(stream);
        var tasks = new List<MasterTaskRecord>();

        foreach (var taskElement in EnumerateMasterTasks(document.RootElement))
        {
            if (!TryReadMasterId(taskElement, out var id))
            {
                continue;
            }

            if (!int.TryParse(id, out var taskmasterId))
            {
                continue;
            }

            tasks.Add(new MasterTaskRecord(
                TaskmasterId: taskmasterId,
                Id: id,
                Title: ReadString(taskElement, "title"),
                Description: ReadString(taskElement, "description"),
                AdrRefs: ReadStringArray(taskElement, "adrRefs"),
                ChapterRefs: ReadStringArray(taskElement, "archRefs"),
                OverlayRef: ReadString(taskElement, "overlay"),
                TestRefs: ReadStringArray(taskElement, "testRefs"),
                ContractRefs: ReadStringArray(taskElement, "contractRefs"),
                ArtifactRefs: ReadStringArray(taskElement, "artifactRefs")));
        }

        return tasks;
    }

    private static ViewTaskRecord LoadViewTask52(string viewTasksPath)
    {
        return LoadViewTasks(viewTasksPath).Single(task => task.TaskmasterId == 52);
    }

    private static IReadOnlyList<ViewTaskRecord> LoadViewTasks(string viewTasksPath)
    {
        using var stream = File.OpenRead(viewTasksPath);
        using var document = JsonDocument.Parse(stream);

        var tasks = new List<ViewTaskRecord>();

        foreach (var taskElement in EnumerateViewTasks(document.RootElement))
        {
            if (!TryReadTaskmasterId(taskElement, out var taskmasterId))
            {
                continue;
            }

            tasks.Add(new ViewTaskRecord(
                TaskmasterId: taskmasterId,
                Title: ReadString(taskElement, "title"),
                Description: ReadString(taskElement, "description"),
                AdrRefs: ReadStringArray(taskElement, "adr_refs"),
                ChapterRefs: ReadStringArray(taskElement, "chapter_refs"),
                OverlayRefs: ReadStringArray(taskElement, "overlay_refs"),
                TestRefs: ReadStringArray(taskElement, "test_refs"),
                ContractRefs: ReadStringArray(taskElement, "contractRefs"),
                ArtifactRefs: ReadStringArray(taskElement, "artifactRefs")));
        }

        return tasks;
    }

    private static IEnumerable<JsonElement> EnumerateMasterTasks(JsonElement rootElement)
    {
        if (rootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var taskElement in rootElement.EnumerateArray())
            {
                if (taskElement.ValueKind == JsonValueKind.Object)
                {
                    yield return taskElement;
                }
            }

            yield break;
        }

        if (rootElement.ValueKind == JsonValueKind.Object &&
            rootElement.TryGetProperty("master", out var masterElement) &&
            masterElement.ValueKind == JsonValueKind.Object &&
            masterElement.TryGetProperty("tasks", out var tasksElement) &&
            tasksElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var taskElement in tasksElement.EnumerateArray())
            {
                if (taskElement.ValueKind == JsonValueKind.Object)
                {
                    yield return taskElement;
                }
            }

            yield break;
        }

        throw new InvalidDataException("Unsupported master tasks structure.");
    }

    private static IEnumerable<JsonElement> EnumerateViewTasks(JsonElement rootElement)
    {
        if (rootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var taskElement in rootElement.EnumerateArray())
            {
                if (taskElement.ValueKind == JsonValueKind.Object)
                {
                    yield return taskElement;
                }
            }

            yield break;
        }

        if (rootElement.ValueKind == JsonValueKind.Object &&
            rootElement.TryGetProperty("tasks", out var tasksElement) &&
            tasksElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var taskElement in tasksElement.EnumerateArray())
            {
                if (taskElement.ValueKind == JsonValueKind.Object)
                {
                    yield return taskElement;
                }
            }

            yield break;
        }

        throw new InvalidDataException("Unsupported view tasks structure.");
    }

    private static bool TryReadMasterId(JsonElement taskElement, out string id)
    {
        id = string.Empty;
        if (!taskElement.TryGetProperty("id", out var idElement))
        {
            return false;
        }

        if (idElement.ValueKind == JsonValueKind.String)
        {
            id = idElement.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(id);
        }

        if (idElement.ValueKind == JsonValueKind.Number && idElement.TryGetInt32(out var numericId))
        {
            id = numericId.ToString();
            return true;
        }

        return false;
    }

    private static bool TryReadTaskmasterId(JsonElement taskElement, out int taskmasterId)
    {
        taskmasterId = default;
        if (!taskElement.TryGetProperty("taskmaster_id", out var idElement))
        {
            return false;
        }

        if (idElement.ValueKind == JsonValueKind.Number)
        {
            return idElement.TryGetInt32(out taskmasterId);
        }

        if (idElement.ValueKind == JsonValueKind.String)
        {
            return int.TryParse(idElement.GetString(), out taskmasterId);
        }

        return false;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement parentElement, string propertyName)
    {
        if (!parentElement.TryGetProperty(propertyName, out var arrayElement) ||
            arrayElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return arrayElement
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private static string ReadString(JsonElement parentElement, string propertyName)
    {
        if (!parentElement.TryGetProperty(propertyName, out var valueElement) ||
            valueElement.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        return valueElement.GetString() ?? string.Empty;
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

        throw new DirectoryNotFoundException("Cannot locate repository root for Task 52 governance tests.");
    }

    private static IReadOnlyList<string> ResolveGovernanceEvidenceFiles(
        string repositoryRoot,
        IReadOnlyList<string> artifactRefs)
    {
        var resolved = new List<string>();
        foreach (var artifactRef in artifactRefs)
        {
            var resolvedPath = ResolveArtifactPath(repositoryRoot, artifactRef);
            if (!File.Exists(resolvedPath))
            {
                throw new InvalidDataException($"Missing evidence file for artifact ref: {artifactRef}");
            }

            resolved.Add(resolvedPath);
        }

        return resolved;
    }

    private static string ResolveArtifactPath(string repositoryRoot, string artifactRef)
    {
        if (!artifactRef.Contains("<date>", StringComparison.Ordinal))
        {
            return NormalizeAndCombine(repositoryRoot, artifactRef);
        }

        var marker = "<date>";
        var markerIndex = artifactRef.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return NormalizeAndCombine(repositoryRoot, artifactRef);
        }

        var prefix = artifactRef[..markerIndex].TrimEnd('/', '\\');
        var suffix = artifactRef[(markerIndex + marker.Length)..].TrimStart('/', '\\');
        var prefixPath = NormalizeAndCombine(repositoryRoot, prefix);
        if (!Directory.Exists(prefixPath))
        {
            throw new InvalidDataException($"Evidence date root not found: {prefixPath}");
        }

        var dateDirectories = Directory.GetDirectories(prefixPath)
            .OrderByDescending(Path.GetFileName)
            .ToArray();
        foreach (var dateDirectory in dateDirectories)
        {
            var candidate = Path.GetFullPath(Path.Combine(dateDirectory, suffix.Replace('/', Path.DirectorySeparatorChar)));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidDataException($"Cannot resolve dated artifact ref: {artifactRef}");
    }

    private static string NormalizeAndCombine(string repositoryRoot, string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(repositoryRoot, normalized));
    }

    private static void AssertMasterRefsAreParsable(MasterTaskRecord task, string repositoryRoot)
    {
        task.AdrRefs.Should().NotBeEmpty();
        task.AdrRefs.Should().OnlyContain(value => IsAdrRefToken(value));

        task.ChapterRefs.Should().NotBeEmpty();
        task.ChapterRefs.Should().OnlyContain(value => IsChapterRefToken(value));

        task.OverlayRef.Should().NotBeNullOrWhiteSpace();
        AssertRepositoryFileExists(repositoryRoot, task.OverlayRef);

        task.TestRefs.Should().NotBeEmpty();
        task.TestRefs.Should().OnlyContain(value => IsAllowedTestFileRef(value));
        foreach (var testRef in task.TestRefs)
        {
            AssertRepositoryFileExists(repositoryRoot, testRef);
        }
    }

    private static void AssertViewRefsAreParsable(ViewTaskRecord task, string repositoryRoot)
    {
        task.AdrRefs.Should().NotBeEmpty();
        task.AdrRefs.Should().OnlyContain(value => IsAdrRefToken(value));

        task.ChapterRefs.Should().NotBeEmpty();
        task.ChapterRefs.Should().OnlyContain(value => IsChapterRefToken(value));

        task.OverlayRefs.Should().NotBeEmpty();
        foreach (var overlayRef in task.OverlayRefs)
        {
            AssertRepositoryFileExists(repositoryRoot, overlayRef);
        }

        task.TestRefs.Should().NotBeEmpty();
        task.TestRefs.Should().OnlyContain(value => IsAllowedTestFileRef(value));
        foreach (var testRef in task.TestRefs)
        {
            AssertRepositoryFileExists(repositoryRoot, testRef);
        }
    }

    private static bool IsAdrRefToken(string value)
    {
        return value.StartsWith("ADR-", StringComparison.Ordinal) &&
               value.Length == 8 &&
               value.Skip(4).All(char.IsDigit);
    }

    private static bool IsChapterRefToken(string value)
    {
        return value.StartsWith("CH", StringComparison.Ordinal) &&
               value.Length == 4 &&
               value.Skip(2).All(char.IsDigit);
    }

    private static bool IsAllowedTestFileRef(string value)
    {
        return value.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
               value.EndsWith(".gd", StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertRepositoryFileExists(string repositoryRoot, string relativePath)
    {
        relativePath.Should().NotBeNullOrWhiteSpace();
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(repositoryRoot, normalized));
        File.Exists(fullPath).Should().BeTrue($"expected referenced file to exist: {relativePath}");
    }

    private sealed record MasterTaskRecord(
        int TaskmasterId,
        string Id,
        string Title,
        string Description,
        IReadOnlyList<string> AdrRefs,
        IReadOnlyList<string> ChapterRefs,
        string OverlayRef,
        IReadOnlyList<string> TestRefs,
        IReadOnlyList<string> ContractRefs,
        IReadOnlyList<string> ArtifactRefs);

    private sealed record ViewTaskRecord(
        int TaskmasterId,
        string Title,
        string Description,
        IReadOnlyList<string> AdrRefs,
        IReadOnlyList<string> ChapterRefs,
        IReadOnlyList<string> OverlayRefs,
        IReadOnlyList<string> TestRefs,
        IReadOnlyList<string> ContractRefs,
        IReadOnlyList<string> ArtifactRefs);
}
