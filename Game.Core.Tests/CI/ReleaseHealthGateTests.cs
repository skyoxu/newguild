using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.CI;

public sealed class ReleaseHealthGateTests
{
    // ACC:T24.3
    [Fact]
    public void Should_Return_Zero_And_Write_Output_When_CrashFreeSessions_Meets_Threshold()
    {
        var repoRoot = FindRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "python", "release_health_gate.py");
        File.Exists(scriptPath).Should().BeTrue("the release health gate script must exist at scripts/python/release_health_gate.py");

        var startedAtUtc = DateTime.UtcNow;
        var outputCandidates = GetCandidateOutputPaths(repoRoot, startedAtUtc);

        var result = RunReleaseHealthGate(
            scriptPath,
            repoRoot,
            new Dictionary<string, string?>
            {
                ["PYTHONUTF8"] = "1",
                ["GD_OFFLINE_MODE"] = "1",
                ["SENTRY_AUTH_TOKEN"] = "dummy",
                ["SENTRY_ORG"] = "dummy",
                ["SENTRY_PROJECT"] = "dummy",
                ["RELEASE_HEALTH_THRESHOLD_JSON"] = "{\"crash_free_sessions_threshold\":99.5,\"window_hours\":24}",
                ["RELEASE_HEALTH_METRICS_JSON"] = "{\"crash_free_sessions\":99.6}"
            });

        result.ExitCode.Should().Be(0, "the gate must return exit code 0 when crash-free sessions meets the configured threshold");

        var outputPath = PickLatestExistingPath(outputCandidates);
        outputPath.Should().NotBeNull("the gate must write logs/ci/<date>/release-health.json");

        File.GetLastWriteTimeUtc(outputPath!).Should().BeOnOrAfter(startedAtUtc.AddSeconds(-5));

        var json = File.ReadAllText(outputPath!);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
        doc.RootElement.GetProperty("passed").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("reason").GetString().Should().NotBeNullOrWhiteSpace();
        doc.RootElement.GetProperty("metrics").GetProperty("crash_free_sessions").GetDouble().Should().BeApproximately(99.6, 0.0001);
    }

    [Fact]
    public void Should_Return_NonZero_When_CrashFreeSessions_Below_Threshold()
    {
        var repoRoot = FindRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "python", "release_health_gate.py");
        File.Exists(scriptPath).Should().BeTrue("the release health gate script must exist at scripts/python/release_health_gate.py");

        var startedAtUtc = DateTime.UtcNow;
        var outputCandidates = GetCandidateOutputPaths(repoRoot, startedAtUtc);

        var result = RunReleaseHealthGate(
            scriptPath,
            repoRoot,
            new Dictionary<string, string?>
            {
                ["PYTHONUTF8"] = "1",
                ["GD_OFFLINE_MODE"] = "1",
                ["SENTRY_AUTH_TOKEN"] = "dummy",
                ["SENTRY_ORG"] = "dummy",
                ["SENTRY_PROJECT"] = "dummy",
                ["RELEASE_HEALTH_THRESHOLD_JSON"] = "{\"crash_free_sessions_threshold\":99.5,\"window_hours\":24}",
                ["RELEASE_HEALTH_METRICS_JSON"] = "{\"crash_free_sessions\":99.4}"
            });

        result.ExitCode.Should().NotBe(0, "the gate must return a non-zero exit code when crash-free sessions is below the configured threshold");

        var outputPath = PickLatestExistingPath(outputCandidates);
        outputPath.Should().NotBeNull("the gate must write logs/ci/<date>/release-health.json even on failure");

        File.GetLastWriteTimeUtc(outputPath!).Should().BeOnOrAfter(startedAtUtc.AddSeconds(-5));

        var json = File.ReadAllText(outputPath!);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
        doc.RootElement.GetProperty("passed").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("reason").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Should_Return_NonZero_When_ThresholdJson_Is_Invalid()
    {
        var repoRoot = FindRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "python", "release_health_gate.py");
        File.Exists(scriptPath).Should().BeTrue("the release health gate script must exist at scripts/python/release_health_gate.py");

        var startedAtUtc = DateTime.UtcNow;
        var outputCandidates = GetCandidateOutputPaths(repoRoot, startedAtUtc);

        var result = RunReleaseHealthGate(
            scriptPath,
            repoRoot,
            new Dictionary<string, string?>
            {
                ["PYTHONUTF8"] = "1",
                ["GD_OFFLINE_MODE"] = "1",
                ["SENTRY_AUTH_TOKEN"] = "dummy",
                ["SENTRY_ORG"] = "dummy",
                ["SENTRY_PROJECT"] = "dummy",
                ["RELEASE_HEALTH_THRESHOLD_JSON"] = "not-json",
                ["RELEASE_HEALTH_METRICS_JSON"] = "{\"crash_free_sessions\":99.6}"
            });

        result.ExitCode.Should().NotBe(0, "the gate must return a non-zero exit code when the threshold JSON is invalid");

        var outputPath = PickLatestExistingPath(outputCandidates);
        outputPath.Should().NotBeNull("the gate must write logs/ci/<date>/release-health.json even on configuration errors");

        File.GetLastWriteTimeUtc(outputPath!).Should().BeOnOrAfter(startedAtUtc.AddSeconds(-5));

        var json = File.ReadAllText(outputPath!);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
        doc.RootElement.GetProperty("passed").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("reason").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Should_Return_NonZero_When_SentryBaseUrlHost_Is_Not_Allowlisted()
    {
        var repoRoot = FindRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "python", "release_health_gate.py");
        File.Exists(scriptPath).Should().BeTrue("the release health gate script must exist at scripts/python/release_health_gate.py");

        var startedAtUtc = DateTime.UtcNow;
        var outputCandidates = GetCandidateOutputPaths(repoRoot, startedAtUtc);

        var result = RunReleaseHealthGate(
            scriptPath,
            repoRoot,
            new Dictionary<string, string?>
            {
                ["PYTHONUTF8"] = "1",
                ["GD_OFFLINE_MODE"] = "0",
                ["SENTRY_BASE_URL"] = "https://evil.example",
                ["SENTRY_ALLOWED_HOSTS"] = "sentry.io",
                ["SENTRY_AUTH_TOKEN"] = "dummy",
                ["SENTRY_ORG"] = "dummy",
                ["SENTRY_PROJECT"] = "dummy",
                ["RELEASE_HEALTH_THRESHOLD_JSON"] = "{\"crash_free_sessions_threshold\":99.5,\"window_hours\":24}",
                ["RELEASE_HEALTH_METRICS_JSON"] = "{\"crash_free_sessions\":99.6}"
            });

        result.ExitCode.Should().NotBe(0);

        var outputPath = PickLatestExistingPath(outputCandidates);
        outputPath.Should().NotBeNull("the gate must write logs/ci/<date>/release-health.json even on allowlist failures");

        var json = File.ReadAllText(outputPath!);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("passed").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("reason").GetString().Should().Contain("allowlist");
    }

    private static string FindRepoRoot()
    {
        var start = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 12 && start != null; i++)
        {
            var projectGodot = Path.Combine(start.FullName, "project.godot");
            var scriptsDir = Path.Combine(start.FullName, "scripts");
            if (File.Exists(projectGodot) && Directory.Exists(scriptsDir))
            {
                return start.FullName;
            }

            start = start.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root (expected to find project.godot and scripts/).");
    }

    private static IReadOnlyList<string> GetCandidateOutputPaths(string repoRoot, DateTime startedAtUtc)
    {
        var day0 = startedAtUtc.ToString("yyyy-MM-dd");
        var day1 = startedAtUtc.AddDays(1).ToString("yyyy-MM-dd");

        return new[]
        {
            Path.Combine(repoRoot, "logs", "ci", day0, "release-health.json"),
            Path.Combine(repoRoot, "logs", "ci", day1, "release-health.json")
        };
    }

    private static string? PickLatestExistingPath(IEnumerable<string> candidates)
    {
        return candidates
            .Where(File.Exists)
            .Select(path => new { path, ts = File.GetLastWriteTimeUtc(path) })
            .OrderByDescending(x => x.ts)
            .Select(x => x.path)
            .FirstOrDefault();
    }

    private static ProcessResult RunReleaseHealthGate(string scriptPath, string workingDirectory, IReadOnlyDictionary<string, string?> env)
    {
        ProcessResult? result = null;

        Action act = () =>
        {
            var psi = new ProcessStartInfo
            {
                FileName = "py",
                Arguments = $"-3 \"{scriptPath}\"",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            foreach (var kvp in env)
            {
                if (kvp.Value is null)
                {
                    psi.Environment.Remove(kvp.Key);
                }
                else
                {
                    psi.Environment[kvp.Key] = kvp.Value;
                }
            }

            using var process = new Process { StartInfo = psi };
            process.Start().Should().BeTrue("the release health gate must be runnable via 'py -3'");

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();

            var exited = process.WaitForExit(60_000);
            if (!exited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch { }
            }

            exited.Should().BeTrue("the release health gate must exit within the timeout");
            result = new ProcessResult(process.ExitCode, stdout, stderr);
        };

        act.Should().NotThrow("the release health gate must be executable");
        result.Should().NotBeNull();
        return result!;
    }

    private sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);
}
