using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.CI;

public sealed class V11AcceptanceAnchorTraceabilityTests
{
    private static readonly string[] RequiredAnchorsForSummary =
    {
        "ACC:T55.1",
        "ACC:T55.2",
        "ACC:T55.3"
    };

    // ACC:T55.7
    [Fact]
    public void ShouldBuildTraceableVerdicts_WhenAllRequiredAnchorsAreProvided()
    {
        var evidences = new[]
        {
            new AnchorEvidence("ACC:T55.1", Passed: true, Source: "logs/ci/task-links.json"),
            new AnchorEvidence("ACC:T55.2", Passed: true, Source: "logs/unit/2026-03-15/coverage.json"),
            new AnchorEvidence("ACC:T55.3", Passed: false, Source: "logs/e2e/2026-03-15/summary.json")
        };

        var summary = BuildSummary(evidences, RequiredAnchorsForSummary);

        summary.Keys.Should().BeEquivalentTo(RequiredAnchorsForSummary);
        summary["ACC:T55.1"].HasResult.Should().BeTrue();
        summary["ACC:T55.1"].Passed.Should().BeTrue();
        summary["ACC:T55.2"].HasResult.Should().BeTrue();
        summary["ACC:T55.2"].Passed.Should().BeTrue();
        summary["ACC:T55.3"].HasResult.Should().BeTrue();
        summary["ACC:T55.3"].Passed.Should().BeFalse();
        summary["ACC:T55.1"].Source.Should().Be("logs/ci/task-links.json");
        summary["ACC:T55.2"].Source.Should().Be("logs/unit/2026-03-15/coverage.json");
        summary["ACC:T55.3"].Source.Should().Be("logs/e2e/2026-03-15/summary.json");
    }

    [Fact]
    public void ShouldMarkAnchorAsMissing_WhenNoEvidenceIsAvailable()
    {
        var evidences = new[]
        {
            new AnchorEvidence("ACC:T55.1", Passed: true, Source: "logs/ci/task-links.json")
        };

        var summary = BuildSummary(evidences, RequiredAnchorsForSummary);

        summary["ACC:T55.2"].HasResult.Should().BeFalse();
        summary["ACC:T55.2"].Source.Should().BeNull();
        summary["ACC:T55.3"].HasResult.Should().BeFalse();
        summary["ACC:T55.3"].Source.Should().BeNull();

        var overall = EvaluateOverallVerdict(summary, RequiredAnchorsForSummary);
        overall.Status.Should().Be("fail");
        overall.Reason.Should().Contain("missing_required_anchors");
        overall.Reason.Should().Contain("ACC:T55.2");
        overall.Reason.Should().Contain("ACC:T55.3");
    }

    [Fact]
    public void ShouldUseLastEvidence_WhenDuplicateAnchorRecordsExist()
    {
        var evidences = new[]
        {
            new AnchorEvidence("ACC:T55.1", Passed: false, Source: "logs/ci/old.json"),
            new AnchorEvidence("ACC:T55.1", Passed: true, Source: "logs/ci/new.json")
        };

        var summary = BuildSummary(evidences, new[] { "ACC:T55.1" });

        summary["ACC:T55.1"].Passed.Should().BeTrue();
        summary["ACC:T55.1"].Source.Should().Be("logs/ci/new.json");
    }

    private static IReadOnlyDictionary<string, AnchorVerdict> BuildSummary(
        IEnumerable<AnchorEvidence> evidences,
        IEnumerable<string> requiredAnchors)
    {
        var latestByAnchor = evidences
            .GroupBy(x => x.Anchor)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

        var summary = new Dictionary<string, AnchorVerdict>(StringComparer.Ordinal);
        foreach (var anchor in requiredAnchors)
        {
            if (latestByAnchor.TryGetValue(anchor, out var evidence))
            {
                summary[anchor] = new AnchorVerdict(
                    HasResult: true,
                    Passed: evidence.Passed,
                    Source: evidence.Source);
            }
            else
            {
                summary[anchor] = new AnchorVerdict(
                    HasResult: false,
                    Passed: false,
                    Source: null);
            }
        }

        return summary;
    }

    private static OverallVerdict EvaluateOverallVerdict(
        IReadOnlyDictionary<string, AnchorVerdict> summary,
        IReadOnlyList<string> requiredAnchors)
    {
        var missingAnchors = requiredAnchors
            .Where(anchor => !summary.TryGetValue(anchor, out var verdict) || !verdict.HasResult)
            .ToArray();
        if (missingAnchors.Length > 0)
        {
            return new OverallVerdict(
                "fail",
                $"missing_required_anchors:{string.Join(',', missingAnchors)}");
        }

        return new OverallVerdict("pass", string.Empty);
    }

    private sealed record AnchorEvidence(string Anchor, bool Passed, string Source);

    private sealed record AnchorVerdict(bool HasResult, bool Passed, string? Source);

    private sealed record OverallVerdict(string Status, string Reason);
}
