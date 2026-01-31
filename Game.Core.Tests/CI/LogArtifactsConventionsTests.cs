using System;
using System.Globalization;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.CI;

// References: ADR-0003-observability-release-health, ADR-0005-quality-gates
public sealed class LogArtifactsConventionsTests
{
    private static bool IsValidLogArtifactRef(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return false;

        // Must be a repo-relative forward-slash path.
        if (relativePath.Contains('\\'))
            return false;
        if (relativePath.StartsWith("/", StringComparison.Ordinal))
            return false;
        if (Regex.IsMatch(relativePath, "^[a-zA-Z]:/"))
            return false;

        // Must live under logs/.
        if (!relativePath.StartsWith("logs/", StringComparison.Ordinal))
            return false;

        // Expect logs/<suite>/<YYYY-MM-DD>/<file>
        var parts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4)
            return false;

        var suite = parts[1];
        if (string.IsNullOrWhiteSpace(suite))
            return false;

        var datePart = parts[2];
        if (!DateTime.TryParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return false;

        var filePart = parts[^1];
        if (string.IsNullOrWhiteSpace(filePart))
            return false;

        return true;
    }

    private static string BuildLogArtifactPath(string suite, DateTime dateUtc, string fileName)
    {
        suite.Should().NotBeNullOrWhiteSpace();
        fileName.Should().NotBeNullOrWhiteSpace();
        fileName.Should().NotContain("/");
        fileName.Should().NotContain("\\");

        return $"logs/{suite}/{dateUtc:yyyy-MM-dd}/{fileName}";
    }

    // ACC:T40.5
    [Fact]
    public void Should_Build_Log_Artifact_Paths_With_Stable_Date_Format()
    {
        var dateUtc = new DateTime(2030, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var path = BuildLogArtifactPath("ci", dateUtc, "security-audit.jsonl");

        path.Should().Be("logs/ci/2030-12-31/security-audit.jsonl");
        IsValidLogArtifactRef(path).Should().BeTrue();
    }

    // ACC:T40.5
    [Theory]
    [InlineData("logs/unit/2026-01-31/coverage.json", true)]
    [InlineData("logs/e2e/2026-01-31/results.xml", true)]
    [InlineData("logs/ci/2026-01-31/task-links.json", true)]
    [InlineData("logs/perf/2026-01-31/summary.json", true)]
    [InlineData("logs/ci/2026-13-01/bad-date.json", false)]
    [InlineData("logs//2026-01-31/missing-suite.json", false)]
    [InlineData("Logs/ci/2026-01-31/case-sensitive.json", false)]
    [InlineData("C:/logs/ci/2026-01-31/absolute.json", false)]
    [InlineData("/logs/ci/2026-01-31/rooted.json", false)]
    [InlineData("logs\\ci\\2026-01-31\\backslashes.json", false)]
    public void Should_Validate_Log_Artifact_References(string candidate, bool expected)
    {
        IsValidLogArtifactRef(candidate).Should().Be(expected);
    }
}
