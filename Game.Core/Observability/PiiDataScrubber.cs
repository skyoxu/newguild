using System;
using System.Text.RegularExpressions;

namespace Game.Core.Observability;

public static class PiiDataScrubber
{
    private static readonly Regex GodotPath = new(@"(?i)\b(?:res|user)://[^\s""']+", RegexOptions.Compiled);
    private static readonly Regex WindowsAbsPath = new(@"\b[A-Za-z]:\\[^\s""']+", RegexOptions.Compiled);

    public static string Scrub(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var scrubbed = input;
        scrubbed = GodotPath.Replace(scrubbed, "[path]");
        scrubbed = WindowsAbsPath.Replace(scrubbed, "[path]");
        return scrubbed;
    }
}
