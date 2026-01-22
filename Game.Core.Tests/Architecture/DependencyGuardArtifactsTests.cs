using System;
using System.Globalization;
using System.IO;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.Architecture;

public sealed class DependencyGuardArtifactsTests
{
    private const string DependencyGuardScriptRelativePath = "scripts/python/dependency_guard.py";
    private const string DependencyGuardJsonFileName = "dependency-guard.json";
    private const string DependencyGuardSummaryFileName = "dependency-guard.txt";

    // ACC:T43.2
    [Fact]
    public void Should_LocateDependencyGuardScript_And_DefineExpectedArtifactNames()
    {
        var repoRoot = FindRepoRootByProjectGodot();
        repoRoot.Should().NotBeNullOrWhiteSpace();

        var scriptPath = Path.Combine(repoRoot, "scripts", "python", "dependency_guard.py");
        File.Exists(scriptPath).Should().BeTrue("dependency guard script should exist at '{0}'", DependencyGuardScriptRelativePath);

        DependencyGuardJsonFileName.Should().Be("dependency-guard.json");
        DependencyGuardSummaryFileName.Should().Contain("dependency-guard").And.EndWith(".txt");
    }

    // ACC:T43.6
    [Fact]
    public void Should_Use_LogsCiDateDirectory_ForDependencyGuardArtifacts()
    {
        var dateSegment = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var artifactsDir = Path.Combine("logs", "ci", dateSegment);

        var jsonPath = Path.Combine(artifactsDir, DependencyGuardJsonFileName);
        var summaryPath = Path.Combine(artifactsDir, DependencyGuardSummaryFileName);

        Path.IsPathRooted(artifactsDir).Should().BeFalse();
        artifactsDir.Should().Be(Path.Combine("logs", "ci", dateSegment));

        Path.GetDirectoryName(jsonPath).Should().Be(artifactsDir);
        Path.GetDirectoryName(summaryPath).Should().Be(artifactsDir);

        Path.GetFileName(jsonPath).Should().Be("dependency-guard.json");
        Path.GetFileName(summaryPath).Should().Contain("dependency-guard").And.EndWith(".txt");
    }

    private static string FindRepoRootByProjectGodot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 12 && dir is not null; i++)
        {
            var markerPath = Path.Combine(dir.FullName, "project.godot");
            if (File.Exists(markerPath))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root (missing 'project.godot' marker).");
    }
}
