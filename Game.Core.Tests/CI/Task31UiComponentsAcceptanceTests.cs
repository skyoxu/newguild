using System;
using System.Globalization;
using System.IO;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.CI;

public sealed class Task31UiComponentsAcceptanceTests
{
    // ACC:T31.5
    [Fact]
    public void Should_EmitTraceableEvidence_AndHaveUiComponentScenesPresent()
    {
        var repoRoot = FindRepoRoot();

        var requiredScenes = new[]
        {
            "Game.Godot/Scenes/UI/Components/StatusPanel.tscn",
            "Game.Godot/Scenes/UI/Components/ErrorPanel.tscn",
            "Game.Godot/Scenes/UI/Components/ListPanel.tscn",
            "Game.Godot/Scenes/UI/Components/ConfirmDialog.tscn",
        };

        foreach (var relPath in requiredScenes)
        {
            var path = Path.Combine(repoRoot, relPath.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(path).Should().BeTrue($"required UI component scene should exist: {relPath}");
        }

        var outDir = Path.Combine(
            repoRoot,
            "logs",
            "ci",
            GetCiDateUtc(),
            "task-31-ui-components"
        );
        Directory.CreateDirectory(outDir);

        var runIdRaw = Environment.GetEnvironmentVariable("GITHUB_RUN_ID")
            ?? Environment.GetEnvironmentVariable("CI_RUN_ID")
            ?? $"local-{Environment.ProcessId}";
        var runId = SanitizeFileName(runIdRaw);

        var evidencePath = Path.Combine(outDir, $"evidence-{runId}.txt");
        File.WriteAllText(
            evidencePath,
            $"Task31 UI component scenes verified. runId={runIdRaw}\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        );
        File.Exists(evidencePath).Should().BeTrue("evidence file must be written for traceability");
    }

    private static string FindRepoRoot()
    {
        var envRoot = Environment.GetEnvironmentVariable("REPO_ROOT");
        if (!string.IsNullOrWhiteSpace(envRoot))
        {
            var full = Path.GetFullPath(envRoot);
            var project = Path.Combine(full, "project.godot");
            var gameGodotDir = Path.Combine(full, "Game.Godot");
            if (File.Exists(project) && Directory.Exists(gameGodotDir))
                return full;
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var godotProject = Path.Combine(dir.FullName, "project.godot");
            var gameGodotDir = Path.Combine(dir.FullName, "Game.Godot");
            if (File.Exists(godotProject) && Directory.Exists(gameGodotDir))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Failed to locate repo root by searching for 'project.godot' and 'Game.Godot/'");
    }

    private static string GetCiDateUtc()
    {
        var raw = Environment.GetEnvironmentVariable("CI_DATE_UTC")
            ?? Environment.GetEnvironmentVariable("CI_DATE");

        if (raw is not null && DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return raw;

        return DateTime.UtcNow.ToString("yyyy-MM-dd");
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";

        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.')
                sb.Append(ch);
        }

        return sb.Length == 0 ? "unknown" : sb.ToString();
    }
}
