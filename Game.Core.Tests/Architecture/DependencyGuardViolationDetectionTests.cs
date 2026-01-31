using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.Architecture;

// ADR References: ADR-0005, ADR-0007, ADR-0018
public sealed class DependencyGuardViolationDetectionTests
{
    private const string ReportFileName = "dependency-guard.json";
    private const int DefaultTimeoutMs = 60_000;

    // ACC:T43.4
    [Fact]
    public void DependencyGuard_WhenRun_WritesArtifactsToLogsCiDateDirectory()
    {
        using var repo = TempRepo.Create(b =>
        {
            b.WriteProjectGodot();
            b.WriteCsproj("", "GodotGame");
            b.WriteCsproj("Game.Core", "Game.Core");
            b.WriteCsproj("Game.Core.Tests", "Game.Core.Tests");
            b.WriteCsproj("Tests.Godot", "Tests.Godot");
            b.WriteCs("Game.Core", "Ok.cs", "namespace Game.Core; public static class Ok { public static int Value => 1; }");
        });

        var result = RunDependencyGuard(repo.RootPath);

        var reportPath = FindSingleDependencyGuardReport(repo.RootPath);
        reportPath.Should().NotBeNull($"Expected '{ReportFileName}' to be written under logs/ci. Stdout: {result.Stdout} Stderr: {result.Stderr}");

        var reportDir = Path.GetDirectoryName(reportPath!)!;
        var summaryPath = Directory.GetFiles(reportDir, "dependency-guard*.txt", SearchOption.TopDirectoryOnly)
            .SingleOrDefault();

        summaryPath.Should().NotBeNull($"Expected a text summary next to '{ReportFileName}'. Stdout: {result.Stdout} Stderr: {result.Stderr}");
        File.ReadAllText(summaryPath!, Encoding.UTF8).Should().NotBeNullOrWhiteSpace();

        using var json = JsonDocument.Parse(File.ReadAllText(reportPath!, Encoding.UTF8));
        json.RootElement.TryGetProperty("violations", out var violations).Should().BeTrue($"Expected JSON to contain 'violations'. JSON: {json.RootElement.GetRawText()}");
        violations.ValueKind.Should().Be(JsonValueKind.Array);
    }

    // ACC:T43.2
    [Fact]
    public void DependencyGuard_WhenGameCoreUsesGodotNamespace_ReportsViolation()
    {
        using var repo = TempRepo.Create(b =>
        {
            b.WriteProjectGodot();
            b.WriteCsproj("", "GodotGame");
            b.WriteCsproj("Game.Core", "Game.Core");
            b.WriteCsproj("Game.Core.Tests", "Game.Core.Tests");
            b.WriteCsproj("Tests.Godot", "Tests.Godot");
            b.WriteCs("Game.Core", "Bad.cs", "using Godot;\nnamespace Game.Core;\npublic sealed class Bad { public void TouchGodot() => Godot.GD.Print(\"x\"); }");
        });

        var result = RunDependencyGuard(repo.RootPath);

        var reportPath = FindSingleDependencyGuardReport(repo.RootPath);
        reportPath.Should().NotBeNull($"Expected '{ReportFileName}' to be written under logs/ci. Stdout: {result.Stdout} Stderr: {result.Stderr}");

        using var json = JsonDocument.Parse(File.ReadAllText(reportPath!, Encoding.UTF8));
        var violationTexts = ExtractViolationTexts(json.RootElement);

        violationTexts.Should().NotBeEmpty($"Expected at least one violation. JSON: {json.RootElement.GetRawText()}");
        violationTexts.Should().Contain(v => v.Contains("Godot", StringComparison.OrdinalIgnoreCase), "Expected a violation mentioning Godot namespace usage");
        violationTexts.Should().Contain(v => v.Contains("Game.Core", StringComparison.OrdinalIgnoreCase) || v.Contains("Bad.cs", StringComparison.OrdinalIgnoreCase), "Expected the violation to reference the source area");
    }

    // ACC:T43.5
    [Fact]
    public void DependencyGuard_WhenScriptsCoreUsesGodotApi_ReportsViolation()
    {
        using var repo = TempRepo.Create(b =>
        {
            b.WriteProjectGodot();
            b.WriteCsproj("", "GodotGame");
            b.WriteCsproj("Game.Core", "Game.Core");
            b.WriteCsproj("Game.Core.Tests", "Game.Core.Tests");
            b.WriteCsproj("Tests.Godot", "Tests.Godot");
            b.WriteCs("Scripts/Core", "BadAdapterLeak.cs", "using Godot;\nnamespace Scripts.Core;\npublic sealed class BadAdapterLeak { public void TouchGodot() => Godot.GD.Print(\"x\"); }");
        });

        var result = RunDependencyGuard(repo.RootPath);

        var reportPath = FindSingleDependencyGuardReport(repo.RootPath);
        reportPath.Should().NotBeNull($"Expected '{ReportFileName}' to be written under logs/ci. Stdout: {result.Stdout} Stderr: {result.Stderr}");

        using var json = JsonDocument.Parse(File.ReadAllText(reportPath!, Encoding.UTF8));
        var violationTexts = ExtractViolationTexts(json.RootElement);

        violationTexts.Should().Contain(v =>
                v.Contains("Scripts", StringComparison.OrdinalIgnoreCase) &&
                v.Contains("Core", StringComparison.OrdinalIgnoreCase) &&
                v.Contains("Godot", StringComparison.OrdinalIgnoreCase),
            "Expected a dependency-matrix violation when Scripts/Core uses Godot API");
    }

    [Fact]
    public void DependencyGuard_WhenRepoIsClean_ReportsNoViolations()
    {
        using var repo = TempRepo.Create(b =>
        {
            b.WriteProjectGodot();
            b.WriteCsproj("", "GodotGame");
            b.WriteCsproj("Game.Core", "Game.Core");
            b.WriteCsproj("Game.Core.Tests", "Game.Core.Tests");
            b.WriteCsproj("Tests.Godot", "Tests.Godot");
            b.WriteCs("Game.Core", "Ok.cs", "namespace Game.Core; public static class Ok { public static int Value => 1; }");
        });

        var result = RunDependencyGuard(repo.RootPath);

        var reportPath = FindSingleDependencyGuardReport(repo.RootPath);
        reportPath.Should().NotBeNull($"Expected '{ReportFileName}' to be written under logs/ci. Stdout: {result.Stdout} Stderr: {result.Stderr}");

        using var json = JsonDocument.Parse(File.ReadAllText(reportPath!, Encoding.UTF8));
        var violationTexts = ExtractViolationTexts(json.RootElement);

        result.ExitCode.Should().Be(0, $"Expected exit code 0 for a clean repo. Stdout: {result.Stdout} Stderr: {result.Stderr}");
        violationTexts.Should().BeEmpty($"Expected no violations. JSON: {json.RootElement.GetRawText()}");
    }

    private static IReadOnlyList<string> ExtractViolationTexts(JsonElement root)
    {
        if (!root.TryGetProperty("violations", out var violations) || violations.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var list = new List<string>();
        foreach (var violation in violations.EnumerateArray())
        {
            list.Add(FlattenViolation(violation));
        }

        return list;
    }

    private static string FlattenViolation(JsonElement v)
    {
        static string ReadString(JsonElement obj, string name)
        {
            if (obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? string.Empty;
            }
            return string.Empty;
        }

        var parts = new[]
        {
            ReadString(v, "rule"),
            ReadString(v, "ruleId"),
            ReadString(v, "message"),
            ReadString(v, "file"),
            ReadString(v, "path"),
            ReadString(v, "from"),
            ReadString(v, "to"),
            ReadString(v, "source"),
            ReadString(v, "target"),
        };

        var combined = string.Join(" | ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        return string.IsNullOrWhiteSpace(combined) ? v.GetRawText() : combined;
    }

    private static string? FindSingleDependencyGuardReport(string repoRoot)
    {
        var ciRoot = Path.Combine(repoRoot, "logs", "ci");
        if (!Directory.Exists(ciRoot))
        {
            return null;
        }

        var matches = Directory.GetFiles(ciRoot, ReportFileName, SearchOption.AllDirectories);
        return matches.Length == 1 ? matches[0] : null;
    }

    private static DependencyGuardProcessResult RunDependencyGuard(string repoRoot)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "py",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.ArgumentList.Add("-3");
        psi.ArgumentList.Add(Path.Combine("scripts", "python", "dependency_guard.py"));

        using var process = Process.Start(psi);
        process.Should().NotBeNull("Expected to start Python via 'py -3'");

        var stdoutTask = process!.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(DefaultTimeoutMs))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best-effort.
            }

            throw new TimeoutException($"dependency_guard.py did not exit within {DefaultTimeoutMs}ms");
        }

        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();

        return new DependencyGuardProcessResult(process.ExitCode, stdout, stderr);
    }

    private readonly record struct DependencyGuardProcessResult(int ExitCode, string Stdout, string Stderr);

    private sealed class TempRepo : IDisposable
    {
        public string RootPath { get; }

        private TempRepo(string rootPath)
        {
            RootPath = rootPath;
        }

        public static TempRepo Create(Action<TempRepoBuilder> build)
        {
            var root = Path.Combine(Path.GetTempPath(), "dependency-guard-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            var builder = new TempRepoBuilder(root);
            builder.CopyDependencyGuardScriptFromRealRepo();
            build(builder);

            return new TempRepo(root);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(RootPath))
                {
                    Directory.Delete(RootPath, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    private sealed class TempRepoBuilder
    {
        private readonly string _root;

        public TempRepoBuilder(string root)
        {
            _root = root;
        }

        public void CopyDependencyGuardScriptFromRealRepo()
        {
            var realRepoRoot = FindRealRepoRootContainingDependencyGuard();
            var source = Path.Combine(realRepoRoot, "scripts", "python", "dependency_guard.py");
            File.Exists(source).Should().BeTrue($"Expected dependency guard script at '{source}'");

            var destDir = Path.Combine(_root, "scripts", "python");
            Directory.CreateDirectory(destDir);

            var dest = Path.Combine(destDir, "dependency_guard.py");
            File.WriteAllText(dest, File.ReadAllText(source, Encoding.UTF8), Encoding.UTF8);
        }

        public void WriteProjectGodot()
        {
            File.WriteAllText(Path.Combine(_root, "project.godot"), "", Encoding.UTF8);
        }

        public void WriteCsproj(string projectDirName, string projectName)
        {
            var projectDir = Path.Combine(_root, projectDirName);
            Directory.CreateDirectory(projectDir);

            var csprojPath = Path.Combine(projectDir, projectName + ".csproj");
            var csproj = "" +
                "<Project Sdk=\"Microsoft.NET.Sdk\">\n" +
                "  <PropertyGroup>\n" +
                "    <TargetFramework>net8.0</TargetFramework>\n" +
                "    <ImplicitUsings>enable</ImplicitUsings>\n" +
                "    <Nullable>enable</Nullable>\n" +
                "  </PropertyGroup>\n" +
                "</Project>\n";

            File.WriteAllText(csprojPath, csproj, Encoding.UTF8);
        }

        public void WriteCs(string relativeDir, string fileName, string content)
        {
            var dir = Path.Combine(_root, relativeDir);
            Directory.CreateDirectory(dir);

            var path = Path.Combine(dir, fileName);
            File.WriteAllText(path, content + "\n", Encoding.UTF8);
        }

        private static string FindRealRepoRootContainingDependencyGuard()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "scripts", "python", "dependency_guard.py");
                if (File.Exists(candidate))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Could not find repo root containing scripts/python/dependency_guard.py");
        }
    }
}
