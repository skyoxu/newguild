#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.CI
{
    // References: ADR-0005-quality-gates (Accepted)
    public sealed class ArtifactsEvidenceTests
    {
        // ACC:T29.6
        // Evidence: produce a traceable artifact reference under logs/**.
        [Fact]
        public void Should_Write_ArtifactsEvidence_To_Logs()
        {
            var repoRoot = TryFindRepoRoot(out var root)
                ? root
                : Directory.GetCurrentDirectory();

            var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var artifactFileName = $"task-29--artifacts-evidence--{Environment.ProcessId}.json";
            var artifactRelativePath = Path.Combine("logs", "unit", date, artifactFileName);
            var artifactFullPath = Path.Combine(repoRoot, artifactRelativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(artifactFullPath)!);

            var evidence = new EvidenceArtifact(
                TaskId: 29,
                AcceptanceAnchor: "ACC:T29.6",
                ArtifactRefs: new[] { NormalizeToForwardSlashes(artifactRelativePath) },
                CreatedUtc: DateTimeOffset.UtcNow.ToString("O"),
                Runner: Environment.GetCommandLineArgs().FirstOrDefault() ?? "unknown",
                Framework: ".NET",
                Note: "CI evidence artifact for reproducibility and traceability."
            );

            evidence.TaskId.Should().Be(29);
            evidence.AcceptanceAnchor.Should().Be("ACC:T29.6");

            var json = JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true });
            json.Should().Contain("ACC:T29.6");

            File.WriteAllText(artifactFullPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            File.Exists(artifactFullPath).Should().BeTrue();
            new FileInfo(artifactFullPath).Length.Should().BeGreaterThan(0);

            var roundTrip = File.ReadAllText(artifactFullPath, Encoding.UTF8);
            roundTrip.Should().Contain("ACC:T29.6");
            roundTrip.Should().Contain("task-29--artifacts-evidence");
        }

        [Fact]
        public void Should_Have_Stable_ArtifactRef_Shape()
        {
            var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var artifactFileName = $"task-29--artifacts-evidence--{Environment.ProcessId}.json";
            var artifactRelativePath = Path.Combine("logs", "unit", date, artifactFileName);

            artifactRelativePath.Should().StartWith(Path.Combine("logs", "unit") + Path.DirectorySeparatorChar);
            artifactRelativePath.Should().Contain("task-29--artifacts-evidence--");
            artifactRelativePath.Should().EndWith(".json");
        }

        private static bool TryFindRepoRoot(out string repoRoot)
        {
            static bool LooksLikeRepoRoot(string dir)
            {
                return File.Exists(Path.Combine(dir, "project.godot"))
                    || File.Exists(Path.Combine(dir, "tasks", "tasks.json"))
                    || File.Exists(Path.Combine(dir, "architecture_base.index"))
                    || Directory.Exists(Path.Combine(dir, "Game.Core.Tests"));
            }

            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var current = new DirectoryInfo(start);
                for (var i = 0; i < 12 && current != null; i++, current = current.Parent)
                {
                    if (LooksLikeRepoRoot(current.FullName))
                    {
                        repoRoot = current.FullName;
                        return true;
                    }
                }
            }

            repoRoot = string.Empty;
            return false;
        }

        private static string NormalizeToForwardSlashes(string path)
        {
            return path.Replace('\\', '/');
        }

        private sealed record EvidenceArtifact(
            int TaskId,
            string AcceptanceAnchor,
            string[] ArtifactRefs,
            string CreatedUtc,
            string Runner,
            string Framework,
            string Note
        );
    }
}
