using System;

namespace Game.Core.Tests.Docs.Support;

internal static class DocRef
{
    public static string Normalize(string value)
        => value.Trim().Replace('\\', '/');

    public static bool EqualsNormalized(string left, string right)
        => string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
}

