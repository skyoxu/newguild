using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.Architecture;

public sealed class DependencyGuardSummaryFormatTests
{
    private sealed record Violation(
        string RuleId,
        string From,
        string To,
        string Details
    );

    private static string FormatMarkdownSummary(IReadOnlyList<Violation> violations)
    {
        if (violations is null) throw new ArgumentNullException(nameof(violations));

        var normalized = violations
            .Select(v => new Violation(
                RuleId: v.RuleId?.Trim() ?? string.Empty,
                From: v.From?.Trim() ?? string.Empty,
                To: v.To?.Trim() ?? string.Empty,
                Details: v.Details?.Trim() ?? string.Empty))
            .Where(v => v.RuleId.Length > 0 || v.From.Length > 0 || v.To.Length > 0 || v.Details.Length > 0)
            .OrderBy(v => v.RuleId, StringComparer.Ordinal)
            .ThenBy(v => v.From, StringComparer.Ordinal)
            .ThenBy(v => v.To, StringComparer.Ordinal)
            .ThenBy(v => v.Details, StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("## Dependency Guard");
        sb.AppendLine();
        sb.AppendLine($"Total violations: {normalized.Count}");
        sb.AppendLine();

        if (normalized.Count == 0)
        {
            sb.AppendLine("No violations detected.");
            return sb.ToString();
        }

        sb.AppendLine("Violations:");
        foreach (var violation in normalized)
        {
            var rule = violation.RuleId.Length == 0 ? "<unknown-rule>" : violation.RuleId;
            var from = violation.From.Length == 0 ? "<unknown-from>" : violation.From;
            var to = violation.To.Length == 0 ? "<unknown-to>" : violation.To;
            var details = violation.Details.Length == 0 ? "" : $" ({violation.Details})";
            sb.AppendLine($"- [{rule}] {from} -> {to}{details}");
        }

        return sb.ToString();
    }

    private static string NormalizeNewlines(string value) => value.Replace("\r\n", "\n");

    // ACC:T43.3 - Step Summary formatting contract for dependency guard violations.
    [Fact]
    public void Should_FormatSummary_WithDeterministicHeaderCountAndOrderedBullets()
    {
        var violations = new List<Violation>
        {
            new("DG-002", "Game.Godot", "Game.Core", "Disallowed direction"),
            new("DG-001", "Game.Core", "GodotSharp", "Forbidden reference"),
        };

        var summary = NormalizeNewlines(FormatMarkdownSummary(violations));

        summary.Should().StartWith("## Dependency Guard\n\nTotal violations: 2\n\n");
        summary.Should().Contain("Violations:\n");

        var expectedOrder =
            "- [DG-001] Game.Core -> GodotSharp (Forbidden reference)\n" +
            "- [DG-002] Game.Godot -> Game.Core (Disallowed direction)\n";

        summary.Should().Contain(expectedOrder);
    }

    [Fact]
    public void Should_FormatSummary_ForEmptyViolations_AsNoViolationsDetected()
    {
        var summary = NormalizeNewlines(FormatMarkdownSummary(Array.Empty<Violation>()));

        summary.Should().Contain("Total violations: 0\n\nNo violations detected.\n");
        summary.Should().NotContain("Violations:\n");
    }

    [Fact]
    public void Should_FormatSummary_IgnoresWhitespaceOnlyFields_AndRemainsDeterministic()
    {
        var violations = new List<Violation>
        {
            new("  ", " ", "\t", "\n"),
            new("DG-010", " A ", " B ", "  "),
        };

        var summary = NormalizeNewlines(FormatMarkdownSummary(violations));

        summary.Should().Contain("Total violations: 1\n\n");
        summary.Should().Contain("- [DG-010] A -> B\n");
    }
}
