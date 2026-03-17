#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.CI;

[Collection("CI")]
[Trait("Category", "CI")]
public sealed class V11EvidenceSummaryPersistenceTests
{
    private static readonly string[] RequiredAnchorVerdicts =
    {
        "ACC:T55.1",
        "ACC:T55.2",
        "ACC:T55.3"
    };

    // ACC:T55.11
    [Fact]
    public void ShouldBuildTask55SummaryPath_WhenUsingLogsCiDateFolder()
    {
        var dateUtc = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);

        var summaryPath = BuildSummaryRelativePath(dateUtc);

        summaryPath.Should().Be("logs/ci/2026-03-15/v11-task-55/summary.json");
    }

    [Fact]
    public void ShouldPass_WhenSummaryIsPersistedWithAllRequiredVerdictFields()
    {
        var dateUtc = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var repoRoot = CreateUniqueTempDirectory();

        try
        {
            var summaryFullPath = ToPlatformPath(repoRoot, BuildSummaryRelativePath(dateUtc));
            Directory.CreateDirectory(Path.GetDirectoryName(summaryFullPath)!);

            var summaryJson = CreateSummaryJson(RequiredAnchorVerdicts);
            File.WriteAllText(summaryFullPath, summaryJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var result = ValidateSummaryPersistence(repoRoot, dateUtc);

            result.IsSuccess.Should().BeTrue();
            result.FailureReason.Should().BeEmpty();
            result.MissingAnchors.Should().BeEmpty();
            result.DetectedAnchors.Should().BeEquivalentTo(RequiredAnchorVerdicts);
        }
        finally
        {
            SafeDeleteDirectory(repoRoot);
        }
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, "")]
    [InlineData(true, "ACC:T55.2")]
    public void ShouldFail_WhenSummaryIsMissingEmptyOrLacksRequiredFields(bool writeFile, string? missingAnchor)
    {
        var dateUtc = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var repoRoot = CreateUniqueTempDirectory();

        try
        {
            var summaryFullPath = ToPlatformPath(repoRoot, BuildSummaryRelativePath(dateUtc));
            Directory.CreateDirectory(Path.GetDirectoryName(summaryFullPath)!);

            if (writeFile)
            {
                if (string.Equals(missingAnchor, string.Empty, StringComparison.Ordinal))
                {
                    File.WriteAllText(summaryFullPath, string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                }
                else
                {
                    var anchorsToPersist = RequiredAnchorVerdicts
                        .Where(anchor => !string.Equals(anchor, missingAnchor, StringComparison.Ordinal))
                        .ToArray();

                    var summaryJson = CreateSummaryJson(anchorsToPersist);
                    File.WriteAllText(summaryFullPath, summaryJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                }
            }

            var result = ValidateSummaryPersistence(repoRoot, dateUtc);

            result.IsSuccess.Should().BeFalse();
            result.SummaryRelativePath.Should().Be("logs/ci/2026-03-15/v11-task-55/summary.json");

            if (!writeFile)
            {
                result.FailureReason.Should().Contain("does not exist");
            }
            else if (string.Equals(missingAnchor, string.Empty, StringComparison.Ordinal))
            {
                result.FailureReason.Should().Contain("empty");
            }
            else
            {
                result.FailureReason.Should().Contain("required anchor verdict fields");
                result.MissingAnchors.Should().Contain("ACC:T55.2");
            }
        }
        finally
        {
            SafeDeleteDirectory(repoRoot);
        }
    }

    private static ValidationResult ValidateSummaryPersistence(string repoRoot, DateTime dateUtc)
    {
        var summaryRelativePath = BuildSummaryRelativePath(dateUtc);
        var summaryFullPath = ToPlatformPath(repoRoot, summaryRelativePath);

        if (!File.Exists(summaryFullPath))
        {
            return ValidationResult.Fail(summaryRelativePath, "summary file does not exist", RequiredAnchorVerdicts, Array.Empty<string>());
        }

        var content = File.ReadAllText(summaryFullPath, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(content))
        {
            return ValidationResult.Fail(summaryRelativePath, "summary file is empty", RequiredAnchorVerdicts, Array.Empty<string>());
        }

        return ValidateSummaryContent(summaryRelativePath, content);
    }

    private static ValidationResult ValidateSummaryContent(string summaryRelativePath, string summaryJson)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(summaryJson);
        }
        catch (JsonException ex)
        {
            return ValidationResult.Fail(
                summaryRelativePath,
                "summary file is not valid JSON: " + ex.Message,
                RequiredAnchorVerdicts,
                Array.Empty<string>());
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("anchors", out var anchorsElement) ||
                anchorsElement.ValueKind != JsonValueKind.Object)
            {
                return ValidationResult.Fail(
                    summaryRelativePath,
                    "required anchors object is missing",
                    RequiredAnchorVerdicts,
                    Array.Empty<string>());
            }

            var missingAnchors = RequiredAnchorVerdicts
                .Where(anchor => !HasPassedVerdict(anchorsElement, anchor))
                .ToArray();

            var detectedAnchors = RequiredAnchorVerdicts
                .Except(missingAnchors, StringComparer.Ordinal)
                .ToArray();

            if (missingAnchors.Length > 0)
            {
                return ValidationResult.Fail(
                    summaryRelativePath,
                    "required anchor verdict fields are missing",
                    missingAnchors,
                    detectedAnchors);
            }

            return ValidationResult.Pass(summaryRelativePath, detectedAnchors);
        }
    }

    private static bool HasPassedVerdict(JsonElement anchorsElement, string anchor)
    {
        if (!anchorsElement.TryGetProperty(anchor, out var verdictElement) ||
            verdictElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!verdictElement.TryGetProperty("passed", out var passedElement))
        {
            return false;
        }

        return passedElement.ValueKind is JsonValueKind.True or JsonValueKind.False;
    }

    private static string CreateSummaryJson(string[] anchorsWithVerdict)
    {
        var anchors = anchorsWithVerdict.ToDictionary(
            anchor => anchor,
            _ => (object)new Dictionary<string, bool>(StringComparer.Ordinal) { ["passed"] = true },
            StringComparer.Ordinal);

        var root = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["task_id"] = 55,
            ["anchors"] = anchors
        };

        return JsonSerializer.Serialize(root);
    }

    private static string BuildSummaryRelativePath(DateTime dateUtc)
    {
        return $"logs/ci/{dateUtc:yyyy-MM-dd}/v11-task-55/summary.json";
    }

    private static string ToPlatformPath(string repoRoot, string relativePath)
    {
        return Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string CreateUniqueTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "v11-task-55-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void SafeDeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed record ValidationResult(
        bool IsSuccess,
        string SummaryRelativePath,
        string FailureReason,
        IReadOnlyCollection<string> MissingAnchors,
        IReadOnlyCollection<string> DetectedAnchors)
    {
        public static ValidationResult Pass(string summaryRelativePath, IReadOnlyCollection<string> detectedAnchors)
        {
            return new ValidationResult(true, summaryRelativePath, string.Empty, Array.Empty<string>(), detectedAnchors);
        }

        public static ValidationResult Fail(
            string summaryRelativePath,
            string failureReason,
            IReadOnlyCollection<string> missingAnchors,
            IReadOnlyCollection<string> detectedAnchors)
        {
            return new ValidationResult(false, summaryRelativePath, failureReason, missingAnchors, detectedAnchors);
        }
    }
}
