using System;
using System.IO;

namespace Game.Core.Tests.Docs.Support;

internal static class RepoRootLocator
{
    public static string FindRepoRoot()
    {
        var candidates = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
        };

        foreach (var candidate in candidates)
        {
            var root = TryFindFrom(candidate);
            if (root is not null)
            {
                return root;
            }
        }

        throw new InvalidOperationException(
            "Unable to locate repository root. Expected to find a directory containing 'docs' and either 'project.godot' or '.taskmaster'.");
    }

    private static string? TryFindFrom(string start)
    {
        var dir = new DirectoryInfo(start);
        for (var i = 0; i < 20 && dir is not null; i++)
        {
            var docsDir = Path.Combine(dir.FullName, "docs");
            var hasDocs = Directory.Exists(docsDir);

            var hasProjectGodot = File.Exists(Path.Combine(dir.FullName, "project.godot"));
            var hasTaskmaster = Directory.Exists(Path.Combine(dir.FullName, ".taskmaster"));

            if (hasDocs && (hasProjectGodot || hasTaskmaster))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}

