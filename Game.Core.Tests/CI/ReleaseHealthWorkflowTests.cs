using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Game.Core.Contracts;
using Xunit;

namespace Game.Core.Tests.CI;

public sealed class ReleaseHealthWorkflowTests
{
    [Fact]
    public void Should_HaveStableDomainEventDefaults()
    {
        var evt = new DomainEvent(
            Type: "core.test.event",
            Source: "tests",
            Data: null,
            Timestamp: DateTime.UnixEpoch,
            Id: "id-1"
        );

        evt.SpecVersion.Should().Be("1.0");
        evt.DataContentType.Should().Be("application/json");
    }

    // ACC:T24.4
    [Fact]
    public void Should_ParseReleaseHealthArtifactJson_AndExposeClearReason()
    {
        const string json = "{\"passed\":true,\"reason\":\"crash_free_sessions 99.6 >= threshold 99.5\",\"metrics\":{\"crash_free_sessions\":99.6},\"threshold\":{\"crash_free_sessions\":99.5,\"window_hours\":24}}";

        var result = ParseReleaseHealthArtifact(json);

        result.Passed.Should().BeTrue();
        result.Reason.Should().NotBeNullOrWhiteSpace();
        result.Reason.Should().Contain("threshold");
    }

    [Fact]
    public void Should_HaveAtLeastOneWorkflowSettingPythonUtf8Environment()
    {
        var repoRoot = FindRepoRoot();
        var workflowsDir = Path.Combine(repoRoot, ".github", "workflows");
        Directory.Exists(workflowsDir).Should().BeTrue("workflows must exist under .github/workflows");

        var workflowFiles = Directory.EnumerateFiles(workflowsDir, "*.yml", SearchOption.TopDirectoryOnly).ToArray();
        workflowFiles.Length.Should().BeGreaterThan(0);

        var combined = string.Join("\n", workflowFiles.Select(File.ReadAllText));
        combined.Should().Contain("PYTHONUTF8", "CI should enforce UTF-8 behavior for Python");
        combined.Should().Contain("PYTHONIOENCODING", "CI should enforce UTF-8 behavior for Python");
    }

    [Fact]
    public void Should_Include_OptionalReleaseHealthGate_And_DisableSecretsOnPullRequest()
    {
        var repoRoot = FindRepoRoot();
        var workflow = Path.Combine(repoRoot, ".github", "workflows", "windows-quality-gate.yml");
        File.Exists(workflow).Should().BeTrue("windows-quality-gate.yml must exist");

        var yml = File.ReadAllText(workflow);
        yml.Should().Contain("Release health gate (optional)");
        yml.Should().Contain("scripts/python/release_health_gate.py");
        yml.Should().Contain("github.event_name != 'pull_request'");
    }

    private static string FindRepoRoot()
    {
        var start = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 12 && start != null; i++)
        {
            var projectGodot = Path.Combine(start.FullName, "project.godot");
            var dotGitHub = Path.Combine(start.FullName, ".github");
            if (File.Exists(projectGodot) && Directory.Exists(dotGitHub))
                return start.FullName;

            start = start.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root (expected project.godot and .github/).");
    }

    private static ReleaseHealthArtifact ParseReleaseHealthArtifact(string json)
    {
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Object);

        doc.RootElement.TryGetProperty("passed", out var passedEl).Should().BeTrue("artifact must include a 'passed' boolean");
        passedEl.ValueKind.Should().BeOneOf(JsonValueKind.True, JsonValueKind.False);

        doc.RootElement.TryGetProperty("reason", out var reasonEl).Should().BeTrue("artifact must include a 'reason' string");
        reasonEl.ValueKind.Should().Be(JsonValueKind.String);

        var reason = reasonEl.GetString();
        reason.Should().NotBeNullOrWhiteSpace();

        return new ReleaseHealthArtifact(passedEl.GetBoolean(), reason!);
    }

    private sealed record ReleaseHealthArtifact(bool Passed, string Reason);
}
