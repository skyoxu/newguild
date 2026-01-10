using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Game.Core.Tests.Docs.Support;

internal static class MarkdownRefs
{
    private static readonly Regex AdrRegex = new Regex(@"\bADR\-\d{4}\b", RegexOptions.CultureInvariant);

    public static HashSet<string> CollectAdrRefs(string markdown, MarkdownFrontMatter frontMatter)
    {
        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in frontMatter.GetList("ADR-Refs", "ADR_Refs", "Adr-Refs", "adrRefs", "adr_refs"))
        {
            foreach (Match match in AdrRegex.Matches(item))
            {
                refs.Add(match.Value.ToUpperInvariant());
            }
        }

        foreach (Match match in AdrRegex.Matches(markdown))
        {
            refs.Add(match.Value.ToUpperInvariant());
        }

        return refs;
    }
}

