using System;
using System.Globalization;

namespace Game.Core.Observability;

/// <summary>
/// Builds relative artifact paths under logs/** for CI traceability.
/// </summary>
public static class ArtifactPathBuilder
{
    public static string BuildUnitArtifactPath(DateTimeOffset timestamp, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required.", nameof(fileName));
        if (fileName.Contains("..", StringComparison.Ordinal) ||
            fileName.IndexOfAny(new[] { '/', '\\' }) >= 0)
        {
            throw new ArgumentException("File name must not contain path separators or traversal.", nameof(fileName));
        }

        var dateSegment = FormatUtcDate(timestamp);
        return $"logs/unit/{dateSegment}/{fileName}";
    }

    public static string FormatUtcDate(DateTimeOffset timestamp)
    {
        return timestamp.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
