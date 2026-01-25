using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.CI;

[Collection("CI")]
[Trait("Category", "CI")]
public sealed class Task28ArtifactRefsAcceptanceTests
{
    // ACC:T28.4
    [Fact]
    public void Should_Produce_RefactorGate_Artifacts_For_T28()
    {
        var repoRoot = FindRepoRoot();
        var startUtc = DateTime.UtcNow;

        var result = RunPy(
            repoRoot,
            "scripts/sc/build.py",
            "tdd",
            "--task-id",
            "28",
            "--stage",
            "refactor"
        );

        result.ExitCode.Should().Be(0, result.ToString());

        var ciDir = Path.Combine(repoRoot, "logs", "ci");
        Directory.Exists(ciDir).Should().BeTrue("refactor gate should write logs under logs/ci");

        var summaryPath = FindLatestFile(ciDir, Path.Combine("sc-build-tdd", "summary.json"), startUtc);
        File.Exists(summaryPath).Should().BeTrue("refactor gate must produce sc-build-tdd/summary.json");

        using var doc = JsonDocument.Parse(File.ReadAllText(summaryPath, Encoding.UTF8));
        doc.RootElement.GetProperty("status").GetString().Should().Be("ok");
        doc.RootElement.GetProperty("stage").GetString().Should().Be("refactor");
        doc.RootElement.GetProperty("task").GetProperty("task_id").GetString().Should().Be("28");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var projectFile = Path.Combine(dir.FullName, "project.godot");
            var buildScript = Path.Combine(dir.FullName, "scripts", "sc", "build.py");
            if (File.Exists(projectFile) && File.Exists(buildScript))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found (expected project.godot and scripts/sc/build.py).");
    }

    private static string FindLatestFile(string logsCiDir, string relativeSuffix, DateTime startUtc)
    {
        string? latest = null;
        DateTime latestUtc = DateTime.MinValue;

        foreach (var file in Directory.EnumerateFiles(logsCiDir, "summary.json", SearchOption.AllDirectories))
        {
            var normalized = file.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            var suffix = Path.DirectorySeparatorChar + relativeSuffix.Replace('/', Path.DirectorySeparatorChar);
            if (!normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                continue;

            var lastWrite = File.GetLastWriteTimeUtc(file);
            if (lastWrite < startUtc)
                continue;
            if (lastWrite <= latestUtc)
                continue;

            latestUtc = lastWrite;
            latest = file;
        }

        if (latest is null)
            throw new FileNotFoundException($"No file found matching suffix: {relativeSuffix}");

        return latest;
    }

    private static ProcessResult RunPy(string repoRoot, params string[] args)
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

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();

        if (!proc.WaitForExit(milliseconds: 180_000))
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
