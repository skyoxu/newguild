using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.CI
{
    public sealed class InteractionArtifactsLoggingTests
    {
        private const int TaskId = 32;
        private const string AcceptanceAnchor = "ACC:T32.5";

        // ACC:T32.5 - Evidence: writes a reproducible artifact under logs/** for CI traceability.
        [Fact]
        public void Should_WriteInteractionArtifact_ToLogsDirectory()
        {
            var repoRoot = RepoRootLocator.MustFindRepoRoot();
            var dateFolder = ResolveCiDateFolder();
            var outputDir = EnsureSafeLogsCiDirectory(repoRoot, dateFolder);

            var runId = Environment.GetEnvironmentVariable("GITHUB_RUN_ID") ?? Environment.GetEnvironmentVariable("BUILD_BUILDID");
            var runAttempt = Environment.GetEnvironmentVariable("GITHUB_RUN_ATTEMPT");
            var suffix = !string.IsNullOrWhiteSpace(runId)
                ? $"--run{runId}--a{runAttempt ?? "1"}"
                : $"--pid{Environment.ProcessId}--{Guid.NewGuid():N}";

            var artifactFile = Path.Combine(
                outputDir,
                $"interaction-artifact--task{TaskId}--{nameof(Should_WriteInteractionArtifact_ToLogsDirectory)}{suffix}.json"
            );

            var artifact = new InteractionArtifact(
                TaskId: TaskId,
                Anchor: AcceptanceAnchor,
                TestName: nameof(Should_WriteInteractionArtifact_ToLogsDirectory),
                CreatedUtc: DateTimeOffset.UtcNow,
                Notes: "Scaffold artifact for interaction-mode CI traceability."
            );

            ArtifactWriter.WriteJson(artifactFile, artifact);

            File.Exists(artifactFile).Should().BeTrue("the test must leave a tangible artifact under logs/**");

            var json = File.ReadAllText(artifactFile, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            json.Should().Contain(AcceptanceAnchor);

            var parsed = JsonSerializer.Deserialize<InteractionArtifact>(json);
            parsed.Should().NotBeNull();
            parsed!.TaskId.Should().Be(TaskId);
            parsed.Anchor.Should().Be(AcceptanceAnchor);
            parsed.TestName.Should().Be(nameof(Should_WriteInteractionArtifact_ToLogsDirectory));

            if (!ShouldPersistArtifacts())
            {
                try
                {
                    File.Delete(artifactFile);
                }
                catch (Exception ex)
                {
                    try
                    {
                        var cleanupLog = Path.Combine(outputDir, "interaction-artifact-cleanup.log");
                        File.AppendAllText(cleanupLog, ex.GetType().Name + ": " + ex.Message + Environment.NewLine);
                    }
                    catch
                    {
                        // Best-effort only.
                    }

                    throw;
                }
            }
        }

        [Fact]
        public void Should_LocateRepoRoot_AndContainProjectSentinel()
        {
            var root = RepoRootLocator.MustFindRepoRoot();

            File.Exists(Path.Combine(root, "project.godot")).Should().BeTrue("repo root must contain project.godot sentinel");
        }

        private sealed record InteractionArtifact(
            int TaskId,
            string Anchor,
            string TestName,
            DateTimeOffset CreatedUtc,
            string Notes
        );

        private static class ArtifactWriter
        {
            public static void WriteJson<T>(string filePath, T value)
            {
                if (filePath is null)
                    throw new ArgumentNullException(nameof(filePath));

                var directory = Path.GetDirectoryName(filePath);
                if (string.IsNullOrWhiteSpace(directory))
                    throw new ArgumentException("File path must contain a directory.", nameof(filePath));

                Directory.CreateDirectory(directory);

                var json = JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }

        private static class RepoRootLocator
        {
            public static string MustFindRepoRoot()
            {
                var root = TryFindRepoRoot();
                if (string.IsNullOrWhiteSpace(root))
                    throw new InvalidOperationException("Failed to locate repo root from AppContext.BaseDirectory.");
                return root;
            }

            public static string? TryFindRepoRoot()
            {
                var current = new DirectoryInfo(AppContext.BaseDirectory);

                for (var i = 0; i < 50 && current is not null; i++)
                {
                    if (ContainsSentinel(current.FullName))
                        return current.FullName;

                    current = current.Parent;
                }

                return null;
            }

            private static bool ContainsSentinel(string directory)
            {
                if (File.Exists(Path.Combine(directory, "project.godot")))
                    return true;

                return false;
            }
        }

        private static string ResolveCiDateFolder()
        {
            var env = Environment.GetEnvironmentVariable("CI_DATE_UTC")
                ?? Environment.GetEnvironmentVariable("CI_DATE");
            if (!string.IsNullOrWhiteSpace(env))
            {
                if (Regex.IsMatch(env, @"^\d{4}-\d{2}-\d{2}$"))
                    return env;
            }

            return DateTime.UtcNow.ToString("yyyy-MM-dd");
        }

        private static string EnsureSafeLogsCiDirectory(string repoRoot, string dateFolder)
        {
            var logsCiRoot = Path.GetFullPath(Path.Combine(repoRoot, "logs", "ci"));
            var outputDir = Path.GetFullPath(Path.Combine(logsCiRoot, dateFolder));

            var rel = Path.GetRelativePath(logsCiRoot, outputDir);
            rel.Should().NotStartWith("..", "artifacts must be written under repo-root logs/ci/<date>/");

            Directory.CreateDirectory(outputDir);
            return outputDir;
        }

        private static bool ShouldPersistArtifacts()
        {
            var explicitPersist = Environment.GetEnvironmentVariable("CI_ARTIFACTS_PERSIST");
            if (string.Equals(explicitPersist, "1", StringComparison.Ordinal))
                return true;

            var gha = Environment.GetEnvironmentVariable("GITHUB_ACTIONS");
            return string.Equals(gha, "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
