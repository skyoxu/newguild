#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.Tasks;

public sealed class Task42EventTypePrefixMigrationTests
{
    // ACC:T42.1
    [Fact]
    public void Should_Not_Use_Legacy_EventType_Prefixes_For_DomainEvent_Type_Literals()
    {
        var repoRoot = RepoRootFinder.FindRepoRootOrThrow();
        var targetDirs = new[]
        {
            Path.Combine(repoRoot, "Game.Core"),
            Path.Combine(repoRoot, "Game.Core.Tests"),
        };

        var csFiles = CsFileScanner.EnumerateCsFiles(targetDirs).ToArray();

        var totalViolations = 0;
        var samples = new List<string>(capacity: 25);

        foreach (var file in csFiles)
        {
            var text = File.ReadAllText(file);
            foreach (var hit in DomainEventTypeLiteralExtractor.ExtractTypeStringLiterals(text))
            {
                var value = hit.Value;
                var isValid = LegacyEventTypeRules.IsValidDomainEventTypeLiteral(value, filePath: file);

                if (isValid)
                {
                    continue;
                }

                totalViolations++;
                if (samples.Count < 25)
                {
                    var rel = Path.GetRelativePath(repoRoot, file);
                    samples.Add($"{rel}:{hit.Line} Type=\"{value}\"");
                }
            }
        }

        totalViolations.Should().Be(0,
            "Task 42 requires eliminating legacy event type prefixes (game.* / game/*) and slash-delimited types in DomainEvent.Type literals.\nSample violations:\n{0}",
            string.Join(Environment.NewLine, samples));
    }

    // ACC:T42.4
    [Fact]
    public void Should_Not_Contain_Any_Legacy_Game_Prefix_In_EventTypes()
    {
        var repoRoot = RepoRootFinder.FindRepoRootOrThrow();
        var targetDirs = new[]
        {
            Path.Combine(repoRoot, "Game.Core"),
            Path.Combine(repoRoot, "Game.Core.Tests"),
        };

        var csFiles = CsFileScanner.EnumerateCsFiles(targetDirs).ToArray();

        var totalHits = 0;
        var samples = new List<string>(capacity: 25);

        foreach (var file in csFiles)
        {
            var text = File.ReadAllText(file);
            foreach (var hit in DomainEventTypeLiteralExtractor.ExtractTypeStringLiterals(text))
            {
                if (!LegacyEventTypeRules.IsLegacyGamePrefix(hit.Value))
                    continue;

                totalHits++;
                if (samples.Count < 25)
                {
                    var rel = Path.GetRelativePath(repoRoot, file);
                    samples.Add($"{rel}:{hit.Line} {hit.Preview}");
                }
            }

            foreach (var hit in EventTypeConstantExtractor.ExtractEventTypeConstants(text))
            {
                if (!LegacyEventTypeRules.IsLegacyGamePrefix(hit.Value))
                    continue;

                totalHits++;
                if (samples.Count < 25)
                {
                    var rel = Path.GetRelativePath(repoRoot, file);
                    samples.Add($"{rel}:{hit.Line} {hit.Preview}");
                }
            }
        }

        totalHits.Should().Be(0,
            "A self-check must not find legacy event types using game.* or game/* in DomainEvent.Type literals or EventType constants.\nSample hits:\n{0}",
            string.Join(Environment.NewLine, samples));
    }

    private static class RepoRootFinder
    {
        public static string FindRepoRootOrThrow()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);

            while (current != null)
            {
                var coreDir = Path.Combine(current.FullName, "Game.Core");
                var testsDir = Path.Combine(current.FullName, "Game.Core.Tests");

                if (Directory.Exists(coreDir) && Directory.Exists(testsDir))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new InvalidOperationException(
                "Unable to locate repo root. Expected to find both 'Game.Core' and 'Game.Core.Tests' directories above the test output folder.");
        }
    }

    private static class CsFileScanner
    {
        public static IEnumerable<string> EnumerateCsFiles(IEnumerable<string> roots)
        {
            foreach (var root in roots)
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    if (IsInBuildOutput(file))
                    {
                        continue;
                    }

                    yield return file;
                }
            }
        }

        private static bool IsInBuildOutput(string path)
        {
            var normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            return normalized.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                   || normalized.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static class DomainEventTypeLiteralExtractor
    {
        public static IEnumerable<StringLiteralHit> ExtractTypeStringLiterals(string text)
        {
            const string marker = "Type:";
            var searchStart = 0;

            while (true)
            {
                var idx = text.IndexOf(marker, searchStart, StringComparison.Ordinal);
                if (idx < 0)
                {
                    yield break;
                }

                // Avoid false positives such as "DataContentType:" (contains "Type:" as a suffix).
                if (idx > 0)
                {
                    var prev = text[idx - 1];
                    if (char.IsLetterOrDigit(prev) || prev == '_')
                    {
                        searchStart = idx + marker.Length;
                        continue;
                    }
                }

                if (!LooksLikeDomainEventContext(text, idx))
                {
                    searchStart = idx + marker.Length;
                    continue;
                }

                var i = idx + marker.Length;
                while (i < text.Length && char.IsWhiteSpace(text[i]))
                {
                    i++;
                }

                if (i < text.Length && text[i] == '@')
                {
                    i++;
                }

                if (i >= text.Length || text[i] != '"')
                {
                    searchStart = idx + marker.Length;
                    continue;
                }

                var startQuote = i;
                i++;

                var valueStart = i;
                while (i < text.Length && text[i] != '"' && text[i] != '\r' && text[i] != '\n')
                {
                    i++;
                }

                if (i < text.Length && text[i] == '"')
                {
                    var value = text.Substring(valueStart, i - valueStart);
                    yield return new StringLiteralHit(
                        Index: startQuote,
                        Line: LineNumberHelper.Get1BasedLineNumber(text, startQuote),
                        Value: value,
                        Preview: "\"" + value + "\"");
                }

                searchStart = idx + marker.Length;
            }
        }

        private static bool LooksLikeDomainEventContext(string text, int typeMarkerIndex)
        {
            var windowStart = Math.Max(0, typeMarkerIndex - 256);
            var windowLength = typeMarkerIndex - windowStart;
            var window = text.AsSpan(windowStart, windowLength);

            return window.IndexOf("DomainEvent", StringComparison.Ordinal) >= 0;
        }
    }

    private static class EventTypeConstantExtractor
    {
        private static readonly Regex EventTypeRegex = new(
            @"\bEventType\s*=\s*@?""([^""]+)""",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static IEnumerable<StringLiteralHit> ExtractEventTypeConstants(string text)
        {
            foreach (Match match in EventTypeRegex.Matches(text))
            {
                var value = match.Groups[1].Value;
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                var quoteIndex = match.Index + match.Value.IndexOf('"');
                yield return new StringLiteralHit(
                    Index: quoteIndex,
                    Line: LineNumberHelper.Get1BasedLineNumber(text, quoteIndex),
                    Value: value,
                    Preview: "\"" + value + "\"");
            }
        }
    }

    private static class LegacyEventTypeRules
    {
        public static bool IsLegacyGamePrefix(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            return value.StartsWith("game.", StringComparison.Ordinal) || value.StartsWith("game/", StringComparison.Ordinal);
        }

        public static bool IsValidDomainEventTypeLiteral(string value, string filePath)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (IsLegacyGamePrefix(value))
                return false;

            if (value.Contains('/'))
                return false;

            if (!IsCoreProjectFile(filePath))
                return true;

            // In Game.Core (non-test), require known bounded-context prefixes per ADR-0004.
            return value.StartsWith("core.", StringComparison.Ordinal) ||
                   value.StartsWith("security.", StringComparison.Ordinal) ||
                   value.StartsWith("ui.menu.", StringComparison.Ordinal) ||
                   value.StartsWith("screen.", StringComparison.Ordinal) ||
                   value.StartsWith("demo.", StringComparison.Ordinal);
        }

        private static bool IsCoreProjectFile(string filePath)
        {
            var norm = (filePath ?? string.Empty).Replace('\\', '/');
            if (norm.Contains("/Game.Core.Tests/", StringComparison.Ordinal))
                return false;
            return norm.Contains("/Game.Core/", StringComparison.Ordinal);
        }
    }

    private static class LineNumberHelper
    {
        public static int Get1BasedLineNumber(string text, int index)
        {
            var line = 1;
            var end = Math.Min(index, text.Length);

            for (var i = 0; i < end; i++)
            {
                if (text[i] == '\n')
                {
                    line++;
                }
            }

            return line;
        }
    }

    private readonly record struct StringLiteralHit(int Index, int Line, string Value, string Preview);
}
