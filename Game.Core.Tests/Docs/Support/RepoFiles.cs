using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FluentAssertions;

namespace Game.Core.Tests.Docs.Support;

internal static class RepoFiles
{
    public static string ReadAllTextUtf8(string repoRoot, string repoRelativePath)
    {
        var fullPath = ResolveFullPathInsideRepo(repoRoot, repoRelativePath);
        File.Exists(fullPath).Should().BeTrue("Required file must exist: {0}", repoRelativePath);
        return File.ReadAllText(fullPath, Encoding.UTF8);
    }

    public static string ResolveFirstExisting(string repoRoot, IEnumerable<string> repoRelativeCandidates)
    {
        var existing = TryResolveFirstExisting(repoRoot, repoRelativeCandidates);
        existing.Should().NotBeNull("At least one of the candidate files must exist: {0}", string.Join(", ", repoRelativeCandidates));
        return existing!;
    }

    public static string? TryResolveFirstExisting(string repoRoot, IEnumerable<string> repoRelativeCandidates)
    {
        foreach (var rel in repoRelativeCandidates)
        {
            var fullPath = ResolveFullPathInsideRepo(repoRoot, rel);
            if (File.Exists(fullPath))
            {
                return rel;
            }
        }

        return null;
    }

    private static string ResolveFullPathInsideRepo(string repoRoot, string repoRelativePath)
    {
        repoRoot.Should().NotBeNullOrWhiteSpace();
        repoRelativePath.Should().NotBeNullOrWhiteSpace();

        Path.IsPathRooted(repoRelativePath).Should().BeFalse("Repo path must be relative: {0}", repoRelativePath);
        repoRelativePath.Should().NotContain(":", "Repo path must not contain a drive prefix: {0}", repoRelativePath);
        repoRelativePath.Should().NotStartWith("\\\\", "Repo path must not be a UNC path: {0}", repoRelativePath);

        var repoFull = Path.GetFullPath(repoRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        var candidate = Path.GetFullPath(Path.Combine(repoRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        candidate.StartsWith(repoFull, StringComparison.OrdinalIgnoreCase).Should().BeTrue(
            "Repo path must stay within repository root (no traversal). root={0} path={1}",
            repoFull,
            repoRelativePath);

        return candidate;
    }
}

