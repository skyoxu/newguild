using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.CI;

[Collection("CI")]
[Trait("Category", "CI")]
public sealed class V11AcceptanceAnchorBindingTests
{
    private const string RequiredAnchor = "ACC:T55.9";
    private const int TaskmasterId = 55;

    // ACC:T55.9
    [Fact]
    public void ShouldFailBinding_WhenAnchorIsUndefinedUnboundOrUnconsumed()
    {
        var task = LoadTask55FromBackView();
        var acceptanceByAnchor = ParseAcceptance(task.Acceptance);
        var referencedFiles = task.TestRefs.ToHashSet(StringComparer.Ordinal);

        var missingDefinition = EvaluateBindingDecision(
            acceptanceByAnchor,
            "ACC:T55.999",
            referencedFiles,
            consumedAnchors: new HashSet<string>(StringComparer.Ordinal));
        missingDefinition.Status.Should().Be("fail");
        missingDefinition.Reason.Should().Be("anchor_undefined");

        var withoutRefs = new Dictionary<string, AcceptanceEntry>(acceptanceByAnchor, StringComparer.Ordinal)
        {
            [RequiredAnchor] = new AcceptanceEntry(RequiredAnchor, Array.Empty<string>())
        };
        var unbound = EvaluateBindingDecision(
            withoutRefs,
            RequiredAnchor,
            referencedFiles,
            consumedAnchors: new HashSet<string>(StringComparer.Ordinal) { RequiredAnchor });
        unbound.Status.Should().Be("fail");
        unbound.Reason.Should().Be("anchor_unbound");

        var unconsumed = EvaluateBindingDecision(
            acceptanceByAnchor,
            RequiredAnchor,
            referencedFiles,
            consumedAnchors: new HashSet<string>(StringComparer.Ordinal));
        unconsumed.Status.Should().Be("fail");
        unconsumed.Reason.Should().Be("anchor_unconsumed");
    }

    [Fact]
    public void ShouldPassBinding_WhenAnchorIsDefinedBoundAndConsumed()
    {
        var task = LoadTask55FromBackView();
        var acceptanceByAnchor = ParseAcceptance(task.Acceptance);
        var referencedFiles = task.TestRefs.ToHashSet(StringComparer.Ordinal);
        var consumed = new HashSet<string>(StringComparer.Ordinal) { RequiredAnchor };

        var result = EvaluateBindingDecision(acceptanceByAnchor, RequiredAnchor, referencedFiles, consumed);

        result.Status.Should().Be("pass");
        result.Reason.Should().BeEmpty();
    }

    private static BindingDecision EvaluateBindingDecision(
        IReadOnlyDictionary<string, AcceptanceEntry> acceptanceByAnchor,
        string requiredAnchor,
        IReadOnlySet<string> testRefs,
        IReadOnlySet<string> consumedAnchors)
    {
        if (!acceptanceByAnchor.TryGetValue(requiredAnchor, out var entry))
        {
            return new BindingDecision("fail", "anchor_undefined");
        }

        if (entry.Refs.Count == 0 || entry.Refs.Any(path => !testRefs.Contains(path)))
        {
            return new BindingDecision("fail", "anchor_unbound");
        }

        if (!consumedAnchors.Contains(requiredAnchor))
        {
            return new BindingDecision("fail", "anchor_unconsumed");
        }

        return new BindingDecision("pass", string.Empty);
    }

    private static IReadOnlyDictionary<string, AcceptanceEntry> ParseAcceptance(IReadOnlyList<string> acceptanceLines)
    {
        var result = new Dictionary<string, AcceptanceEntry>(StringComparer.Ordinal);
        foreach (var line in acceptanceLines)
        {
            if (!line.StartsWith("ACC:T55.", StringComparison.Ordinal))
            {
                continue;
            }

            var firstSpace = line.IndexOf(' ');
            if (firstSpace <= 0)
            {
                continue;
            }

            var anchor = line[..firstSpace];
            var refsIndex = line.IndexOf("Refs:", StringComparison.Ordinal);
            if (refsIndex < 0)
            {
                result[anchor] = new AcceptanceEntry(anchor, Array.Empty<string>());
                continue;
            }

            var refsText = line[(refsIndex + "Refs:".Length)..];
            var refs = refsText
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();

            result[anchor] = new AcceptanceEntry(anchor, refs);
        }

        return result;
    }

    private static BackTask55 LoadTask55FromBackView()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, ".taskmaster", "tasks", "tasks_back.json");
        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);

        var tasksElement = document.RootElement.ValueKind switch
        {
            JsonValueKind.Array => document.RootElement,
            JsonValueKind.Object when document.RootElement.TryGetProperty("tasks", out var nestedTasks) && nestedTasks.ValueKind == JsonValueKind.Array => nestedTasks,
            _ => default
        };

        if (tasksElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("tasks_back.json has unexpected shape.");
        }

        foreach (var taskElement in tasksElement.EnumerateArray())
        {
            if (!taskElement.TryGetProperty("taskmaster_id", out var idElement))
            {
                continue;
            }

            var id = idElement.ValueKind == JsonValueKind.Number
                ? idElement.GetInt32()
                : int.TryParse(idElement.GetString(), out var parsed) ? parsed : -1;
            if (id != TaskmasterId)
            {
                continue;
            }

            var acceptance = ReadStringArray(taskElement, "acceptance");
            var testRefs = ReadStringArray(taskElement, "test_refs");
            return new BackTask55(acceptance, testRefs);
        }

        throw new InvalidDataException("Task 55 not found in tasks_back.json.");
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Game.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed record BackTask55(IReadOnlyList<string> Acceptance, IReadOnlyList<string> TestRefs);

    private sealed record AcceptanceEntry(string Anchor, IReadOnlyList<string> Refs);

    private sealed record BindingDecision(string Status, string Reason);
}
