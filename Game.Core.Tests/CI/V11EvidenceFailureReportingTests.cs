using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.CI
{
    public sealed class V11EvidenceFailureReportingTests
    {
        private static FailureReport EvaluateEvidence(string evidenceJson, string expectedEvidencePath, DateTime dateUtc)
        {
            var summaryPath = BuildSummaryPath(dateUtc);

            if (string.IsNullOrWhiteSpace(evidenceJson))
            {
                return FailureReport.Fail("Evidence payload is missing.", summaryPath);
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(evidenceJson);
            }
            catch (JsonException ex)
            {
                return FailureReport.Fail("Evidence payload is not valid JSON: " + ex.Message, summaryPath);
            }

            using (document)
            {
                JsonElement pathElement;
                if (!document.RootElement.TryGetProperty("evidence_path", out pathElement) || pathElement.ValueKind != JsonValueKind.String)
                {
                    return FailureReport.Fail("Evidence path is missing in payload.", summaryPath);
                }

                var actualPath = NormalizePath(pathElement.GetString() ?? string.Empty);
                var expectedPath = NormalizePath(expectedEvidencePath);

                if (!string.Equals(actualPath, expectedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return FailureReport.Fail("Evidence path mismatch. expected='" + expectedPath + "', actual='" + actualPath + "'.", summaryPath);
                }
            }

            return FailureReport.Pass(summaryPath);
        }

        private static string BuildSummaryPath(DateTime dateUtc)
        {
            return "logs/ci/" + dateUtc.ToString("yyyy-MM-dd") + "/v11-task-55/summary.json";
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return path.Replace("\\", "/").Trim();
        }

        // ACC:T55.6
        [Fact]
        public void ShouldFailImmediatelyAndPointToTaskSummaryPath_WhenEvidenceIsMissing()
        {
            var result = EvaluateEvidence(
                evidenceJson: " ",
                expectedEvidencePath: "logs/ci/2026-03-15/v11-task-55/evidence.json",
                dateUtc: new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc));

            result.IsSuccess.Should().BeFalse();
            result.FailureReason.Should().Contain("missing");
            result.SummaryPath.Should().Be("logs/ci/2026-03-15/v11-task-55/summary.json");
            using var payload = PersistFailureSummaryToTempAndParse(result, "unit");
            payload.RootElement.GetProperty("status").GetString().Should().Be("fail");
            payload.RootElement.GetProperty("reason").GetString().Should().Contain("missing");
            payload.RootElement.GetProperty("summary_path").GetString().Should().Be("logs/ci/2026-03-15/v11-task-55/summary.json");
            payload.RootElement.GetProperty("conflict_source").GetString().Should().Be("unit");
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-json")]
        [InlineData("{\"evidence_path\":123}")]
        public void ShouldFail_WhenEvidenceIsUnparseableOrInvalid(string payload)
        {
            var result = EvaluateEvidence(
                evidenceJson: payload,
                expectedEvidencePath: "logs/ci/2026-03-15/v11-task-55/evidence.json",
                dateUtc: new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc));

            result.IsSuccess.Should().BeFalse();
            result.FailureReason.Should().NotBeNullOrWhiteSpace();
            result.SummaryPath.Should().EndWith("v11-task-55/summary.json");
        }

        [Fact]
        public void ShouldFail_WhenEvidencePathDoesNotMatchExpected()
        {
            var payloadJson = "{\"evidence_path\":\"logs/ci/2026-03-15/v11-task-55/other.json\"}";

            var result = EvaluateEvidence(
                evidenceJson: payloadJson,
                expectedEvidencePath: "logs/ci/2026-03-15/v11-task-55/evidence.json",
                dateUtc: new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc));

            result.IsSuccess.Should().BeFalse();
            result.FailureReason.Should().Contain("mismatch");
            using var payloadDocument = PersistFailureSummaryToTempAndParse(result, "godot");
            payloadDocument.RootElement.GetProperty("status").GetString().Should().Be("fail");
            payloadDocument.RootElement.GetProperty("reason").GetString().Should().Contain("mismatch");
            payloadDocument.RootElement.GetProperty("conflict_source").GetString().Should().Be("godot");
        }

        [Fact]
        public void ShouldPass_WhenEvidencePathMatchesExpected()
        {
            var payload = "{\"evidence_path\":\"logs\\\\ci\\\\2026-03-15\\\\v11-task-55\\\\evidence.json\"}";

            var result = EvaluateEvidence(
                evidenceJson: payload,
                expectedEvidencePath: "logs/ci/2026-03-15/v11-task-55/evidence.json",
                dateUtc: new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc));

            result.IsSuccess.Should().BeTrue();
            result.FailureReason.Should().BeEmpty();
            result.SummaryPath.Should().Be("logs/ci/2026-03-15/v11-task-55/summary.json");
        }

        private static string BuildSummaryPayloadJson(FailureReport report, string conflictSource)
        {
            var payload = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["status"] = report.IsSuccess ? "pass" : "fail",
                ["reason"] = report.FailureReason,
                ["summary_path"] = report.SummaryPath,
                ["conflict_source"] = conflictSource,
            };
            return JsonSerializer.Serialize(payload);
        }

        private static JsonDocument PersistFailureSummaryToTempAndParse(FailureReport report, string conflictSource)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "t55-failure-summary-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var summaryFile = Path.Combine(tempDir, "summary.json");
            File.WriteAllText(summaryFile, BuildSummaryPayloadJson(report, conflictSource));
            File.Exists(summaryFile).Should().BeTrue();
            var content = File.ReadAllText(summaryFile);
            content.Should().NotBeNullOrWhiteSpace();
            return JsonDocument.Parse(content);
        }

        private sealed class FailureReport
        {
            private FailureReport(bool isSuccess, string failureReason, string summaryPath)
            {
                IsSuccess = isSuccess;
                FailureReason = failureReason;
                SummaryPath = summaryPath;
            }

            public bool IsSuccess { get; private set; }

            public string FailureReason { get; private set; }

            public string SummaryPath { get; private set; }

            public static FailureReport Pass(string summaryPath)
            {
                return new FailureReport(true, string.Empty, summaryPath);
            }

            public static FailureReport Fail(string reason, string summaryPath)
            {
                return new FailureReport(false, reason, summaryPath);
            }
        }
    }
}
