using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.CI;

public sealed class Task23QualityGatesAcceptanceTests
{
    // ACC:T37.5
    // ACC:T42.8
    // ACC:T23.1
    [Fact]
    public void Should_ValidateAuditJsonl_AndEmitReport_AndExitNonZeroOnInvalidEntries()
    {
        var repoRoot = FindRepoRoot();
        var tempDir = CreateTempDirectory();
        try
        {
            var logPath = Path.Combine(tempDir, "security-audit.jsonl");
            var reportPath = Path.Combine(tempDir, "audit-validation-report.json");

            var validLine = "{\"ts\":\"2026-01-10T00:00:00Z\",\"action\":\"core.guild.member.joined\",\"reason\":\"test\",\"target\":\"user:u1\",\"caller\":\"Task23QualityGatesAcceptanceTests\"}";
            var invalidJsonLine = "{\"ts\":\"2026-01-10T00:00:01Z\",\"action\":\"core.guild.member.joined\"";
            var missingFieldsLine = "{\"ts\":\"2026-01-10T00:00:02Z\",\"action\":\"core.guild.member.joined\",\"caller\":\"Task23QualityGatesAcceptanceTests\"}";

            File.WriteAllText(
                logPath,
                string.Join("\n", new[] { validLine, invalidJsonLine, missingFieldsLine }) + "\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            );

            var result = RunPy(
                repoRoot,
                new Dictionary<string, string?>(),
                "scripts/python/validate_audit_logs.py",
                "--log-path",
                logPath,
                "--strict",
                "--check-sensitive",
                "--report",
                reportPath
            );

            result.ExitCode.Should().Be(1, result.ToString());
            File.Exists(reportPath).Should().BeTrue("the validator must produce a JSON report file");

            using var doc = JsonDocument.Parse(File.ReadAllText(reportPath, Encoding.UTF8));
            var summary = doc.RootElement.GetProperty("summary");
            summary.GetProperty("total_files").GetInt32().Should().Be(1);
            summary.GetProperty("failed_files").GetInt32().Should().Be(1);
            summary.GetProperty("total_errors").GetInt32().Should().BeGreaterThan(0);

            result.StdOut.Should().Contain("VALIDATION FAILED");
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    // ACC:T23.2
    [Fact]
    public void Should_ValidatePerfP95_AndEmitReport_AndExitNonZeroWhenOverBudget()
    {
        var repoRoot = FindRepoRoot();
        var validatePerfScript = Path.Combine(repoRoot, "scripts", "python", "validate_perf.py");
        File.Exists(validatePerfScript).Should().BeTrue("Task 23 requires scripts/python/validate_perf.py to exist");

        var tempDir = CreateTempDirectory();
        try
        {
            var summaryPath = Path.Combine(tempDir, "db-perf-summary.json");
            var reportPath = Path.Combine(tempDir, "quality-gates-perf.json");

            var summary = new
            {
                timestamp = "2026-01-10T00:00:00Z",
                DB_QUERY_P95 = new
                {
                    samples = 120,
                    p50_ms = 1.0,
                    p95_ms = 25.0,
                    mean_ms = 2.0,
                    max_ms = 30.0
                }
            };

            File.WriteAllText(
                summaryPath,
                JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }) + "\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            );

            var result = RunPy(
                repoRoot,
                new Dictionary<string, string?>(),
                "scripts/python/validate_perf.py",
                "--summary-path",
                summaryPath,
                "--metric",
                "DB_QUERY_P95",
                "--threshold-ms",
                "16.6",
                "--report",
                reportPath,
                "--strict"
            );

            result.ExitCode.Should().Be(1, result.ToString());
            File.Exists(reportPath).Should().BeTrue("the perf validator must produce a JSON report file");

            using var doc = JsonDocument.Parse(File.ReadAllText(reportPath, Encoding.UTF8));
            doc.RootElement.TryGetProperty("metric", out _).Should().BeTrue("report should include the metric name");
            doc.RootElement.TryGetProperty("threshold_ms", out _).Should().BeTrue("report should include the configured threshold");
            doc.RootElement.TryGetProperty("p95_ms", out _).Should().BeTrue("report should include the measured p95 value");
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    // ACC:T23.3
    [Fact]
    public void Should_TreatPerfAndAuditValidation_AsHardGates_InQualityGatesAllMode()
    {
        var repoRoot = FindRepoRoot();
        var qualityGatesPath = Path.Combine(repoRoot, "scripts", "python", "quality_gates.py");
        File.Exists(qualityGatesPath).Should().BeTrue();

        var text = File.ReadAllText(qualityGatesPath, Encoding.UTF8);

        text.Should().Contain("--validate-audit");
        text.Should().Contain("validate_audit_logs.py");

        text.Should().Contain("--validate-perf", "Task 23 requires a perf validation gate in all mode");
        text.Should().Contain("validate_perf.py", "Task 23 requires quality_gates.py to delegate to validate_perf.py");

        var auditHardGateRegex = new Regex(
            @"if\s+args\.validate_audit\s*:\s*[\s\S]*?audit_rc\s*=\s*validate_security_audit_logs\(\)\s*[\s\S]*?if\s+audit_rc\s*!=\s*0\s*:\s*[\s\S]*?hard_failed\s*=\s*True",
            RegexOptions.Multiline
        );
        auditHardGateRegex.IsMatch(text).Should().BeTrue("audit validation failure must set hard_failed = True");

        var perfHardGateRegex = new Regex(
            @"if\s+args\.validate_perf\s*:\s*[\s\S]*?perf_rc\s*=\s*validate_perf_logs\(\)\s*[\s\S]*?if\s+perf_rc\s*!=\s*0\s*:\s*[\s\S]*?hard_failed\s*=\s*True",
            RegexOptions.Multiline
        );
        perfHardGateRegex.IsMatch(text).Should().BeTrue("perf validation failure must set hard_failed = True");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var projectFile = Path.Combine(dir.FullName, "project.godot");
            var scriptsDir = Path.Combine(dir.FullName, "scripts", "python");
            if (File.Exists(projectFile) && Directory.Exists(scriptsDir))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found (expected project.godot and scripts/python).");
    }

    private static string CreateTempDirectory()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "newguild-task23", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDir);
        return baseDir;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private static ProcessResult RunPy(string repoRoot, IReadOnlyDictionary<string, string?> environment, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "py",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("-3");
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        foreach (var kvp in environment)
            psi.Environment[kvp.Key] = kvp.Value;

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();

        if (!proc.WaitForExit(milliseconds: 120_000))
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"Process timed out: py -3 {string.Join(" ", args)}");
        }

        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();

        return new ProcessResult(proc.ExitCode, stdout, stderr);
    }

    private readonly record struct ProcessResult(int ExitCode, string StdOut, string StdErr)
    {
        public override string ToString()
        {
            static string Trim(string s)
            {
                const int max = 4000;
                if (s.Length <= max) return s;
                return s.Substring(0, max) + "\n...<truncated>";
            }

            return $"ExitCode={ExitCode}\nSTDOUT:\n{Trim(StdOut)}\nSTDERR:\n{Trim(StdErr)}";
        }
    }
}
