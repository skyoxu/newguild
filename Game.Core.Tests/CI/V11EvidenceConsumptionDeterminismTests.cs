using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.CI;

public class V11EvidenceConsumptionDeterminismTests
{
    // ACC:T55.5
    [Fact]
    public void ShouldProduceIdenticalMachineReadableOutcome_WhenExecutedTwiceWithSameInput()
    {
        var engine = new CurrentEvidenceConsumptionEngine();
        var firstInput = new EvidenceInput(
            "ACC:T55.5",
            new[]
            {
                new EvidenceItem(EvidenceKind.MachineReadableArtifact, "logs/unit/2026-03-15/coverage.json"),
                new EvidenceItem(EvidenceKind.MachineReadableArtifact, "logs/ci/2026-03-15/task-links.json")
            });
        var secondInput = new EvidenceInput(
            "ACC:T55.5",
            new[]
            {
                new EvidenceItem(EvidenceKind.MachineReadableArtifact, "logs/unit/2026-03-15/coverage.json"),
                new EvidenceItem(EvidenceKind.MachineReadableArtifact, "logs/ci/2026-03-15/task-links.json")
            });
        var reorderedInput = new EvidenceInput(
            "ACC:T55.5",
            new[]
            {
                new EvidenceItem(EvidenceKind.MachineReadableArtifact, "logs/ci/2026-03-15/task-links.json"),
                new EvidenceItem(EvidenceKind.MachineReadableArtifact, "logs/unit/2026-03-15/coverage.json")
            });

        var first = engine.Evaluate(firstInput);
        var second = engine.Evaluate(secondInput);
        var reordered = engine.Evaluate(reorderedInput);

        second.Should().Be(
            first,
            "equivalent machine-readable inputs must produce identical machine-readable conclusions");
        reordered.Should().Be(
            first,
            "evidence ordering must not change deterministic result");
        first.Passed.Should().BeTrue();
        first.FailureReason.Should().BeEmpty();
        first.Digest.Should().Be("ACC:T55.5:MachineReadableArtifact:logs/ci/2026-03-15/task-links.json|MachineReadableArtifact:logs/unit/2026-03-15/coverage.json");
    }

    // ACC:T55.10
    [Fact]
    public void ShouldRejectEvidenceRound_WhenAnyNonMachineReadableEvidenceIsPresent()
    {
        var engine = new CurrentEvidenceConsumptionEngine();
        var input = new EvidenceInput(
            "ACC:T55.10",
            new[]
            {
                new EvidenceItem(EvidenceKind.MachineReadableArtifact, "logs/e2e/2026-03-15/smoke-summary.json"),
                new EvidenceItem(EvidenceKind.ManualStatement, "Human reviewer approved this round.")
            });

        var result = engine.Evaluate(input);

        result.Passed.Should().BeFalse(
            "manual statements, screenshots, or non-replayable outputs must refuse acceptance");
        result.FailureReason.Should().NotBeNullOrWhiteSpace();
        result.FailureReason.Should().Contain("non_machine_readable_evidence");
    }

    [Fact]
    public void ShouldFailRound_WhenInputContainsOnlyManualAndScreenshotConclusions()
    {
        var engine = new CurrentEvidenceConsumptionEngine();
        var input = new EvidenceInput(
            "ACC:T55.2",
            new[]
            {
                new EvidenceItem(EvidenceKind.ManualStatement, "Looks fine in local run."),
                new EvidenceItem(EvidenceKind.ScreenshotConclusion, "See screenshot in chat."),
                new EvidenceItem(EvidenceKind.NonReplayableOutput, "Output copied from terminal history.")
            });

        var result = engine.Evaluate(input);

        result.Passed.Should().BeFalse();
        result.FailureReason.Should().Contain("machine-readable");
    }

    private sealed class CurrentEvidenceConsumptionEngine
    {
        public ConsumptionResult Evaluate(EvidenceInput input)
        {
            var hasMachineReadableArtifact = input.Items.Any(item => item.Kind == EvidenceKind.MachineReadableArtifact);
            var hasDisallowedEvidence = input.Items.Any(item => item.Kind != EvidenceKind.MachineReadableArtifact);

            var canonicalBase = string.Join(
                "|",
                input.Items
                    .Select(item => $"{item.Kind}:{item.Payload}")
                    .OrderBy(entry => entry, System.StringComparer.Ordinal));
            var digest = $"{input.AcceptanceId}:{canonicalBase}";

            var passed = hasMachineReadableArtifact && !hasDisallowedEvidence;
            var reason = passed
                ? string.Empty
                : hasDisallowedEvidence
                    ? "non_machine_readable_evidence: acceptance requires fully machine-readable artifacts."
                    : "missing_machine_readable_evidence: deterministic acceptance requires machine-readable artifacts.";

            return new ConsumptionResult(passed, digest, reason);
        }
    }

    private sealed record EvidenceInput(string AcceptanceId, IReadOnlyList<EvidenceItem> Items);

    private sealed record EvidenceItem(EvidenceKind Kind, string Payload);

    private enum EvidenceKind
    {
        MachineReadableArtifact,
        ManualStatement,
        ScreenshotConclusion,
        NonReplayableOutput
    }

    private sealed record ConsumptionResult(bool Passed, string Digest, string FailureReason);
}
