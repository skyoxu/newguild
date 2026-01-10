using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Game.Core.Tests.Docs.Support;

internal sealed class MarkdownFrontMatter
{
    private readonly Dictionary<string, List<string>> _lists;

    private MarkdownFrontMatter(Dictionary<string, List<string>> lists)
    {
        _lists = lists;
    }

    public static MarkdownFrontMatter TryParse(string markdown)
    {
        var lines = SplitLines(markdown).ToArray();
        if (lines.Length < 3)
        {
            return new MarkdownFrontMatter(new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase));
        }

        if (!string.Equals(lines[0].Trim(), "---", StringComparison.Ordinal))
        {
            return new MarkdownFrontMatter(new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase));
        }

        var endIndex = Array.FindIndex(lines, 1, static l => string.Equals(l.Trim(), "---", StringComparison.Ordinal));
        if (endIndex <= 1)
        {
            return new MarkdownFrontMatter(new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase));
        }

        var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        string? currentKey = null;

        for (var i = 1; i < endIndex; i++)
        {
            var raw = lines[i];
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var keyMatch = Regex.Match(line, @"^(?<key>[A-Za-z0-9_\-]+)\s*:\s*(?<value>.*)$", RegexOptions.CultureInvariant);
            if (keyMatch.Success)
            {
                currentKey = keyMatch.Groups["key"].Value.Trim();
                var value = keyMatch.Groups["value"].Value.Trim();

                if (!dict.TryGetValue(currentKey, out var list))
                {
                    list = new List<string>();
                    dict[currentKey] = list;
                }

                if (value.Length > 0)
                {
                    list.Add(value);
                }

                continue;
            }

            if (currentKey is null)
            {
                continue;
            }

            var itemMatch = Regex.Match(line, @"^\-\s+(?<item>.+)$", RegexOptions.CultureInvariant);
            if (!itemMatch.Success)
            {
                continue;
            }

            var item = itemMatch.Groups["item"].Value.Trim();
            var hashIndex = item.IndexOf('#');
            if (hashIndex >= 0)
            {
                item = item[..hashIndex].Trim();
            }

            if (item.Length > 0)
            {
                dict[currentKey].Add(item);
            }
        }

        return new MarkdownFrontMatter(dict);
    }

    public IReadOnlyList<string> GetList(params string[] possibleKeys)
    {
        foreach (var key in possibleKeys)
        {
            if (_lists.TryGetValue(key, out var list))
            {
                return list;
            }
        }

        return Array.Empty<string>();
    }

    public string? GetScalar(params string[] possibleKeys)
        => GetList(possibleKeys).FirstOrDefault();

    private static IEnumerable<string> SplitLines(string text)
        => text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
}

