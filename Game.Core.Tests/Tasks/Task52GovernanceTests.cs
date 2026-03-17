using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Game.Core.Performance;
using Xunit;

namespace Game.Core.Tests.Tasks;

public sealed class Task52GovernanceTests
{
    [Fact]
    public void ShouldAlignAcceptanceAndEvidenceRefs_WhenComparingTask53MasterAndView()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var masterTask = LoadMasterTask(Path.Combine(repositoryRoot, ".taskmaster", "tasks", "tasks.json"), 53);
        var viewTask = LoadViewTask(Path.Combine(repositoryRoot, ".taskmaster", "tasks", "tasks_back.json"), 53);

        masterTask.Acceptance.Should().HaveCount(3);
        var parsedItems = masterTask.Acceptance
            .Select(ParseAcceptanceItem)
            .ToArray();
        parsedItems.Should().OnlyContain(item => item.Anchor.StartsWith("ACC:T53.", StringComparison.Ordinal));
        parsedItems.Select(item => item.Anchor).Should().Equal("ACC:T53.1", "ACC:T53.2", "ACC:T53.3");
        parsedItems.Should().OnlyContain(item => item.Refs.Count > 0);

        var refsFromAcceptance = parsedItems
            .SelectMany(item => item.Refs)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        refsFromAcceptance.Should().NotBeEmpty();
        refsFromAcceptance.Should().OnlyContain(testRef => masterTask.TestRefs.Contains(testRef, StringComparer.Ordinal));

        masterTask.ArtifactRefs.Should().ContainSingle("T53 requires a single summary artifact for deterministic evidence");
        masterTask.ArtifactRefs.Should().Equal(viewTask.ArtifactRefs, because: "Task/View governance must keep gate evidence references aligned");
        masterTask.TestRefs.Should().Equal(viewTask.TestRefs, because: "Task/View governance must keep test references aligned");
    }

    [Fact]
    public void ShouldResolveLatestEvidenceDeterministically_WhenTask53SummaryUsesDatedArtifactRef()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var masterTask = LoadMasterTask(Path.Combine(repositoryRoot, ".taskmaster", "tasks", "tasks.json"), 53);
        var summaryRef = masterTask.ArtifactRefs.Single();

        var tempRoot = Directory.CreateTempSubdirectory("task53-deterministic-");
        try
        {
            var olderSummary = Path.Combine(tempRoot.FullName, "logs", "ci", "2099-12-30", "sc-acceptance-check-task-53", "summary.json");
            var latestSummary = Path.Combine(tempRoot.FullName, "logs", "ci", "2099-12-31", "sc-acceptance-check-task-53", "summary.json");
            Directory.CreateDirectory(Path.GetDirectoryName(olderSummary) ?? throw new InvalidDataException("older summary parent is null"));
            Directory.CreateDirectory(Path.GetDirectoryName(latestSummary) ?? throw new InvalidDataException("latest summary parent is null"));
            File.WriteAllText(olderSummary, BuildTask53SummaryJson("53", "old-run", "warn", 1, 1, 0, 1, 1, "pass"));
            File.WriteAllText(latestSummary, BuildTask53SummaryJson("53", "new-run", "ok", 1, 1, 1, 1, 1, "pass"));

            var resolvedFirst = ResolveArtifactPath(tempRoot.FullName, summaryRef);
            var resolvedSecond = ResolveArtifactPath(tempRoot.FullName, summaryRef);

            resolvedFirst.Should().Be(latestSummary);
            resolvedSecond.Should().Be(latestSummary, because: "same input and environment must resolve deterministically");

            var baselineSample = new[]
            {
                ExecutionSample.RetryableFailure(1, 3),
                ExecutionSample.RetryableFailure(3, 3),
                ExecutionSample.NonRetryableFailure(),
                ExecutionSample.Success(),
            };
            var firstSummary = ComputeStabilitySummary(baselineSample, retryFailureThreshold: 0, hardFailureThreshold: 0);
            var secondSummary = ComputeStabilitySummary(baselineSample, retryFailureThreshold: 0, hardFailureThreshold: 0);
            firstSummary.Should().BeEquivalentTo(secondSummary);
            firstSummary.FlakyCount.Should().Be(1);
            firstSummary.RetryCount.Should().Be(1);
            firstSummary.FailureCount.Should().Be(1);
            firstSummary.GateResult.Should().Be("fail");

            var boundaryPass = ComputeStabilitySummary(baselineSample, retryFailureThreshold: 1, hardFailureThreshold: 1);
            boundaryPass.GateResult.Should().Be("pass", because: "boundary '=' must be considered pass when failures <= threshold");

            var boundaryFail = ComputeStabilitySummary(baselineSample, retryFailureThreshold: 0, hardFailureThreshold: 1);
            boundaryFail.GateResult.Should().Be("fail", because: "when retry count is above threshold gate must fail");
        }
        finally
        {
            if (tempRoot.Exists)
            {
                tempRoot.Delete(recursive: true);
            }
        }
    }

    [Fact]
    public void ShouldValidateTask53SummaryArtifactSchema_WhenEvidenceIsResolved()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var masterTask = LoadMasterTask(Path.Combine(repositoryRoot, ".taskmaster", "tasks", "tasks.json"), 53);
        var viewTask = LoadViewTask(Path.Combine(repositoryRoot, ".taskmaster", "tasks", "tasks_back.json"), 53);

        masterTask.Id.Should().Be("53");
        viewTask.TaskmasterId.Should().Be(53);
        viewTask.ArtifactRefs.Should().Equal(masterTask.ArtifactRefs);
        masterTask.ArtifactRefs.Single().Should().Be("logs/ci/<date>/sc-acceptance-check-task-53/summary.json");

        var tempRoot = Directory.CreateTempSubdirectory("task53-summary-");
        try
        {
            var summaryPath = Path.Combine(tempRoot.FullName, "logs", "ci", "2099-12-31", "sc-acceptance-check-task-53", "summary.json");
            Directory.CreateDirectory(Path.GetDirectoryName(summaryPath) ?? throw new InvalidDataException("summary parent is null"));
            File.WriteAllText(summaryPath, BuildTask53SummaryJson("53", "run-53", "ok", 1, 1, 1, 1, 1, "pass"));

            var resolved = ResolveGovernanceEvidenceFiles(tempRoot.FullName, masterTask.ArtifactRefs);
            resolved.Should().ContainSingle().Which.Should().Be(summaryPath);

            using var document = JsonDocument.Parse(File.ReadAllText(summaryPath));
            ValidateTask53SummaryDocument(document.RootElement);
        }
        finally
        {
            if (tempRoot.Exists)
            {
                tempRoot.Delete(recursive: true);
            }
        }
    }

    // ACC:T53.1
    [Fact]
    public void ShouldProduceStableClassification_WhenSamplesAreReorderedAndRepeated()
    {
        var samples = new[]
        {
            ExecutionSample.RetryableFailure(1, 3),
            ExecutionSample.RetryableFailure(3, 3),
            ExecutionSample.NonRetryableFailure(),
            ExecutionSample.Success(),
        };

        var first = ClassifySamples(samples);
        var second = ClassifySamples(samples);
        var reordered = ClassifySamples(samples.Reverse().ToArray());

        first.Should().Equal(second, because: "same input must produce identical classification output");
        first.Should().ContainInOrder("flaky", "retry", "failure", "success");
        reordered.OrderBy(x => x, StringComparer.Ordinal).Should().Equal(first.OrderBy(x => x, StringComparer.Ordinal));
        first.Count(value => value == "flaky").Should().Be(1);
        first.Count(value => value == "retry").Should().Be(1);
        first.Count(value => value == "failure").Should().Be(1);
        first.Count(value => value == "success").Should().Be(1);
        first.Count(value => value is "flaky" or "retry" or "failure" or "success").Should().Be(samples.Length, because: "each sample must map to exactly one category");
    }

    // ACC:T53.2
    [Fact]
    public void ShouldApplyUnifiedThresholdRule_WhenMetricEqualsAboveOrBelowThreshold()
    {
        EvaluateGate(metric: 3, threshold: 3).Should().Be("pass", because: "rule is pass iff metric <= threshold");
        EvaluateGate(metric: 2, threshold: 3).Should().Be("pass");
        EvaluateGate(metric: 4, threshold: 3).Should().Be("fail");

        var repeated = Enumerable.Range(0, 5)
            .Select(_ => EvaluateGate(metric: 4, threshold: 3))
            .ToArray();
        repeated.Should().OnlyContain(result => result == "fail");
    }

    // ACC:T53.3
    [Fact]
    public void ShouldKeepSummaryValuesConsistent_WhenRecomputingFromFixedSamples()
    {
        var samples = new[]
        {
            ExecutionSample.RetryableFailure(1, 3),
            ExecutionSample.RetryableFailure(3, 3),
            ExecutionSample.NonRetryableFailure(),
            ExecutionSample.Success(),
        };
        var computed = ComputeStabilitySummary(samples, retryFailureThreshold: 1, hardFailureThreshold: 1);
        var json = BuildTask53SummaryJson(
            "53",
            "replay-run",
            status: "ok",
            flakyCount: computed.FlakyCount,
            retryCount: computed.RetryCount,
            failureCount: computed.FailureCount,
            retryMax: 1,
            failureMax: 1,
            gateResult: computed.GateResult);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        ValidateTask53SummaryDocument(root);

        root.GetProperty("classification_counts").GetProperty("flaky").GetInt32().Should().Be(computed.FlakyCount);
        root.GetProperty("classification_counts").GetProperty("retry").GetInt32().Should().Be(computed.RetryCount);
        root.GetProperty("classification_counts").GetProperty("failure").GetInt32().Should().Be(computed.FailureCount);

        var retryMax = root.GetProperty("thresholds").GetProperty("retry_max").GetInt32();
        var failureMax = root.GetProperty("thresholds").GetProperty("failure_max").GetInt32();
        var recomputedGate = computed.RetryCount > retryMax || computed.FailureCount > failureMax ? "fail" : "pass";
        root.GetProperty("gate_result").GetString().Should().Be(recomputedGate);
    }

    // ACC:T55.1
    [Fact]
    public void ShouldFailWhenAnchorOrRefsAreMissing_WhenEvaluatingTask55AnchorBoundary()
    {
        var parsed = ParseAcceptanceItem("ACC:T55.1 deterministic governance check. Refs: Game.Core.Tests/Tasks/Task52GovernanceTests.cs");
        parsed.Anchor.Should().Be("ACC:T55.1");
        parsed.Refs.Should().ContainSingle("Game.Core.Tests/Tasks/Task52GovernanceTests.cs");

        var missingAnchor = () => ParseAcceptanceItem("T55.1 deterministic governance check. Refs: Game.Core.Tests/Tasks/Task52GovernanceTests.cs");
        var missingRefs = () => ParseAcceptanceItem("ACC:T55.1 deterministic governance check.");
        var emptyRefs = () => ParseAcceptanceItem("ACC:T55.1 deterministic governance check. Refs: ");

        missingAnchor.Should().Throw<InvalidDataException>();
        missingRefs.Should().Throw<InvalidDataException>();
        emptyRefs.Should().Throw<InvalidDataException>();
    }

    // ACC:T55.2
    [Fact]
    public void ShouldProduceDeterministicDecision_WhenRecomputingTask55CoreBehavior()
    {
        var samples = new[]
        {
            ExecutionSample.RetryableFailure(1, 3),
            ExecutionSample.RetryableFailure(3, 3),
            ExecutionSample.NonRetryableFailure(),
            ExecutionSample.Success(),
        };
        var first = ComputeStabilitySummary(samples, retryFailureThreshold: 1, hardFailureThreshold: 1);
        var second = ComputeStabilitySummary(samples, retryFailureThreshold: 1, hardFailureThreshold: 1);

        first.Should().BeEquivalentTo(second);
        var stricter = ComputeStabilitySummary(samples, retryFailureThreshold: 0, hardFailureThreshold: 1);
        stricter.GateResult.Should().Be("fail");
        first.GateResult.Should().Be("pass");
    }

    // ACC:T55.3
    [Fact]
    public void ShouldFailWhenSummaryFileIsMissing_WhenEvaluatingTask55SummaryPersistence()
    {
        var tempRoot = Directory.CreateTempSubdirectory("task55-summary-missing-");
        try
        {
            var expectedArtifact = new[] { "logs/ci/<date>/v11-task-55/summary.json" };
            var action = () => ResolveGovernanceEvidenceFiles(tempRoot.FullName, expectedArtifact);
            action.Should().Throw<InvalidDataException>();
        }
        finally
        {
            if (tempRoot.Exists)
            {
                tempRoot.Delete(recursive: true);
            }
        }
    }

    // ACC:T55.3
    [Fact]
    public void ShouldCreateReadableSummary_WhenTask55RoundProducesEvidence()
    {
        var tempRoot = Directory.CreateTempSubdirectory("task55-summary-present-");
        try
        {
            var summaryPath = Path.Combine(tempRoot.FullName, "logs", "ci", "2026-03-16", "v11-task-55", "summary.json");
            var decision = new UnifiedExecutionDecision("pass", string.Empty, string.Empty, "logs/ci/2026-03-16/v11-task-55/summary.json");
            ProduceTask55Summary(summaryPath, "run-55-summary", "2026-03-16", decision);

            var resolved = ResolveGovernanceEvidenceFiles(tempRoot.FullName, new[] { "logs/ci/<date>/v11-task-55/summary.json" });
            resolved.Should().ContainSingle().Which.Should().Be(summaryPath);

            using var summary = JsonDocument.Parse(File.ReadAllText(summaryPath));
            summary.RootElement.GetProperty("run_id").GetString().Should().Be("run-55-summary");
            summary.RootElement.GetProperty("date").GetString().Should().Be("2026-03-16");
            summary.RootElement.GetProperty("overall_verdict").GetString().Should().Be("pass");
        }
        finally
        {
            if (tempRoot.Exists)
            {
                tempRoot.Delete(recursive: true);
            }
        }
    }

    // ACC:T55.4
    [Fact]
    public void ShouldFailAggregate_WhenAnyCategoryIsMissingOrNotExecutedInSameRun()
    {
        const string runId = "run-55-a";
        var allExecuted = new[]
        {
            CategoryExecutionResult.Pass("unit", runId),
            CategoryExecutionResult.Pass("godot", runId),
            CategoryExecutionResult.Pass("acceptance", runId),
        };
        EvaluateUnifiedExecution(allExecuted).Should().Be("pass");

        var passDecision = EvaluateUnifiedExecutionWithEvidenceDetailed(
            allExecuted,
            new[]
            {
                EvidenceArtifact.Pass("unit", runId, "2026-03-16"),
                EvidenceArtifact.Pass("godot", runId, "2026-03-16"),
                EvidenceArtifact.Pass("acceptance", runId, "2026-03-16"),
            },
            summaryDate: "2026-03-16");
        var passSummaryJson = CreateUnifiedOverallSummary(passDecision);
        using (var passSummary = JsonDocument.Parse(passSummaryJson))
        {
            passSummary.RootElement.GetProperty("overall_verdict").GetString().Should().Be("pass");
            passSummary.RootElement.TryGetProperty("overall_verdicts", out _).Should().BeFalse();
        }

        var missingGodot = new[]
        {
            CategoryExecutionResult.Pass("unit", runId),
            CategoryExecutionResult.Pass("acceptance", runId),
        };
        EvaluateUnifiedExecution(missingGodot).Should().Be("fail");

        var notExecuted = new[]
        {
            CategoryExecutionResult.Pass("unit", runId),
            CategoryExecutionResult.NotExecuted("godot", runId),
            CategoryExecutionResult.Pass("acceptance", runId),
        };
        EvaluateUnifiedExecution(notExecuted).Should().Be("fail");
    }

    // ACC:T55.8
    [Fact]
    public void ShouldFailAggregate_WhenAnyCategoryFailsOrRunIdIsMixed()
    {
        const string runId = "run-55-b";
        var hasFailure = new[]
        {
            CategoryExecutionResult.Pass("unit", runId),
            CategoryExecutionResult.Fail("godot", runId),
            CategoryExecutionResult.Pass("acceptance", runId),
        };
        EvaluateUnifiedExecution(hasFailure).Should().Be("fail");

        var mixedRunId = new[]
        {
            CategoryExecutionResult.Pass("unit", "run-55-b"),
            CategoryExecutionResult.Pass("godot", "run-55-c"),
            CategoryExecutionResult.Pass("acceptance", "run-55-b"),
        };
        EvaluateUnifiedExecution(mixedRunId).Should().Be("fail");

        var validExecution = new[]
        {
            CategoryExecutionResult.Pass("unit", "run-55-d"),
            CategoryExecutionResult.Pass("godot", "run-55-d"),
            CategoryExecutionResult.Pass("acceptance", "run-55-d"),
        };
        var mixedEvidence = new[]
        {
            EvidenceArtifact.Pass("unit", "run-55-d", "2026-03-16"),
            EvidenceArtifact.Pass("godot", "run-55-old", "2026-03-15"),
            EvidenceArtifact.Pass("acceptance", "run-55-d", "2026-03-16"),
        };
        EvaluateUnifiedExecutionWithEvidence(validExecution, mixedEvidence).Should().Be("fail");

        var validEvidence = new[]
        {
            EvidenceArtifact.Pass("unit", "run-55-d", "2026-03-16"),
            EvidenceArtifact.Pass("godot", "run-55-d", "2026-03-16"),
            EvidenceArtifact.Pass("acceptance", "run-55-d", "2026-03-16"),
        };
        EvaluateUnifiedExecutionWithEvidence(validExecution, validEvidence).Should().Be("pass");
    }

    // ACC:T55.12
    [Fact]
    public void ShouldFailImmediately_WhenEvidenceRunOrDateDoesNotMatchCurrentRound()
    {
        var execution = new[]
        {
            CategoryExecutionResult.Pass("unit", "run-55-e"),
            CategoryExecutionResult.Pass("godot", "run-55-e"),
            CategoryExecutionResult.Pass("acceptance", "run-55-e"),
        };
        var wrongDateEvidence = new[]
        {
            EvidenceArtifact.Pass("unit", "run-55-e", "2026-03-16"),
            EvidenceArtifact.Pass("godot", "run-55-e", "2026-03-15"),
            EvidenceArtifact.Pass("acceptance", "run-55-e", "2026-03-16"),
        };

        var decision = EvaluateUnifiedExecutionWithEvidenceDetailed(
            execution,
            wrongDateEvidence,
            summaryDate: "2026-03-16");
        decision.Status.Should().Be("fail");
        decision.Reason.Should().Contain("mismatch");
        decision.ConflictSource.Should().Be("godot");
        decision.SummaryPath.Should().Be("logs/ci/2026-03-16/v11-task-55/summary.json");

        var summaryJson = CreateTask55MismatchSummary(decision);
        using var document = JsonDocument.Parse(summaryJson);
        document.RootElement.GetProperty("task_id").GetString().Should().Be("55");
        document.RootElement.GetProperty("status").GetString().Should().Be("fail");
        document.RootElement.GetProperty("summary_path").GetString().Should().Be("logs/ci/2026-03-16/v11-task-55/summary.json");
        document.RootElement.GetProperty("reason").GetString().Should().Contain("mismatch");
        document.RootElement.GetProperty("conflict_source").GetString().Should().Be("godot");

        var unlabeledEvidence = new[]
        {
            EvidenceArtifact.Pass("unit", "run-55-e", string.Empty),
            EvidenceArtifact.Pass("godot", "run-55-e", "2026-03-16"),
            EvidenceArtifact.Pass("acceptance", "run-55-e", "2026-03-16"),
        };
        var unlabeledDecision = EvaluateUnifiedExecutionWithEvidenceDetailed(
            execution,
            unlabeledEvidence,
            summaryDate: "2026-03-16");
        unlabeledDecision.Status.Should().Be("fail");
        unlabeledDecision.Reason.Should().Be("evidence_round_metadata_missing");
        unlabeledDecision.ConflictSource.Should().Be("unit");
    }

    // ACC:T52.4
    [Fact]
    public void ShouldResolveGovernanceEvidenceFiles_WhenTask52ArtifactsAreConfigured()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var masterTask = LoadMasterTask52(Path.Combine(repositoryRoot, ".taskmaster", "tasks", "tasks.json"));
        var viewTask = LoadViewTask52(Path.Combine(repositoryRoot, ".taskmaster", "tasks", "tasks_back.json"));

        masterTask.ArtifactRefs.Should().Contain("logs/ci/<date>/sc-acceptance-check-task-52/summary.json");
        masterTask.ArtifactRefs.Should().Contain("logs/ci/<date>/sc-acceptance-check-task-52/task-links-validate-summary.json");
        viewTask.ArtifactRefs.Should().Equal(masterTask.ArtifactRefs);

        var tempRoot = Directory.CreateTempSubdirectory("task52-evidence-");
        try
        {
            var syntheticDate = "2099-12-31";
            var summaryPath = Path.Combine(
                tempRoot.FullName,
                "logs",
                "ci",
                syntheticDate,
                "sc-acceptance-check-task-52",
                "summary.json");
            var taskLinksPath = Path.Combine(
                tempRoot.FullName,
                "logs",
                "ci",
                syntheticDate,
                "sc-acceptance-check-task-52",
                "task-links-validate-summary.json");

            Directory.CreateDirectory(Path.GetDirectoryName(summaryPath) ?? throw new InvalidDataException("summary parent is null"));
            File.WriteAllText(summaryPath, "{\"status\":\"ok\",\"task_id\":\"52\"}");
            File.WriteAllText(taskLinksPath, "{\"status\":\"ok\"}");

            var resolvedEvidence = ResolveGovernanceEvidenceFiles(tempRoot.FullName, masterTask.ArtifactRefs);
            resolvedEvidence.Should().Contain(summaryPath);
            resolvedEvidence.Should().Contain(taskLinksPath);

            using var summaryDocument = JsonDocument.Parse(File.ReadAllText(summaryPath));
            summaryDocument.RootElement.GetProperty("status").GetString().Should().Be("ok");
            summaryDocument.RootElement.GetProperty("task_id").GetString().Should().Be("52");
        }
        finally
        {
            if (tempRoot.Exists)
            {
                tempRoot.Delete(recursive: true);
            }
        }
    }

    [Fact]
    public void ShouldThrowInvalidDataException_WhenTask52EvidenceFilesAreMissing()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var masterTask = LoadMasterTask52(Path.Combine(repositoryRoot, ".taskmaster", "tasks", "tasks.json"));
        var tempRoot = Directory.CreateTempSubdirectory("task52-evidence-missing-");

        try
        {
            var act = () => ResolveGovernanceEvidenceFiles(tempRoot.FullName, masterTask.ArtifactRefs);
            act.Should().Throw<InvalidDataException>();
        }
        finally
        {
            if (tempRoot.Exists)
            {
                tempRoot.Delete(recursive: true);
            }
        }
    }

    [Fact]
    public void ShouldThrowInvalidDataException_WhenViewTasksShapeUnsupported()
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempPath, "{\"unexpected\":true}");
            var act = () => LoadViewTasks(tempPath);
            act.Should().Throw<InvalidDataException>();
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public void ShouldThrowInvalidDataException_WhenTask53SummaryMissingRequiredFields()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var masterTask = LoadMasterTask(Path.Combine(repositoryRoot, ".taskmaster", "tasks", "tasks.json"), 53);
        var tempRoot = Directory.CreateTempSubdirectory("task53-summary-missing-fields-");

        try
        {
            var summaryPath = Path.Combine(tempRoot.FullName, "logs", "ci", "2099-12-31", "sc-acceptance-check-task-53", "summary.json");
            Directory.CreateDirectory(Path.GetDirectoryName(summaryPath) ?? throw new InvalidDataException("summary parent is null"));
            File.WriteAllText(summaryPath, "{\"task_id\":\"53\"}");

            var resolved = ResolveGovernanceEvidenceFiles(tempRoot.FullName, masterTask.ArtifactRefs);
            resolved.Should().ContainSingle();

            using var document = JsonDocument.Parse(File.ReadAllText(resolved.Single()));
            var act = () => ValidateTask53SummaryDocument(document.RootElement);
            act.Should().Throw<InvalidDataException>();
        }
        finally
        {
            if (tempRoot.Exists)
            {
                tempRoot.Delete(recursive: true);
            }
        }
    }

    [Fact]
    public void ShouldThrowInvalidDataException_WhenTask53AcceptanceItemIsMalformed()
    {
        var actMissingRefs = () => ParseAcceptanceItem("ACC:T53.1 malformed item without refs");
        actMissingRefs.Should().Throw<InvalidDataException>();

        var actEmptyRefs = () => ParseAcceptanceItem("ACC:T53.1 malformed item. Refs: ");
        actEmptyRefs.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void ShouldThrowInvalidDataException_WhenExecutionSampleAttemptsAreInvalid()
    {
        var invalidBelowZero = new[] { ExecutionSample.RetryableFailure(attempt: 0, maxAttempts: 3) };
        var invalidAboveMax = new[] { ExecutionSample.RetryableFailure(attempt: 4, maxAttempts: 3) };
        var act = () => ClassifySamples(invalidBelowZero);
        var actAboveMax = () => ClassifySamples(invalidAboveMax);
        act.Should().Throw<InvalidDataException>();
        actAboveMax.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void ShouldTouchGameCoreAssembly_WhenRunningTask53GovernanceTests()
    {
        var decision = PerformanceGateEvaluator.EvaluateP95(17.2, 16.6);
        decision.IsOverBudget.Should().BeTrue();
    }

    private static MasterTaskRecord LoadMasterTask(string masterTasksPath, int taskmasterId)
    {
        return LoadMasterTasks(masterTasksPath).Single(task => task.TaskmasterId == taskmasterId);
    }

    private static MasterTaskRecord LoadMasterTask52(string masterTasksPath)
    {
        return LoadMasterTask(masterTasksPath, 52);
    }

    private static IReadOnlyList<MasterTaskRecord> LoadMasterTasks(string masterTasksPath)
    {
        using var stream = File.OpenRead(masterTasksPath);
        using var document = JsonDocument.Parse(stream);
        var tasks = new List<MasterTaskRecord>();

        foreach (var taskElement in EnumerateMasterTasks(document.RootElement))
        {
            if (!TryReadMasterId(taskElement, out var id))
            {
                continue;
            }

            if (!int.TryParse(id, out var taskmasterId))
            {
                continue;
            }

            tasks.Add(new MasterTaskRecord(
                TaskmasterId: taskmasterId,
                Id: id,
                Title: ReadString(taskElement, "title"),
                Description: ReadString(taskElement, "description"),
                Acceptance: ReadStringArray(taskElement, "acceptance"),
                AdrRefs: ReadStringArray(taskElement, "adrRefs"),
                ChapterRefs: ReadStringArray(taskElement, "archRefs"),
                OverlayRef: ReadString(taskElement, "overlay"),
                TestRefs: ReadStringArray(taskElement, "testRefs"),
                ContractRefs: ReadStringArray(taskElement, "contractRefs"),
                ArtifactRefs: ReadStringArray(taskElement, "artifactRefs")));
        }

        return tasks;
    }

    private static ViewTaskRecord LoadViewTask52(string viewTasksPath)
    {
        return LoadViewTask(viewTasksPath, 52);
    }

    private static ViewTaskRecord LoadViewTask(string viewTasksPath, int taskmasterId)
    {
        return LoadViewTasks(viewTasksPath).Single(task => task.TaskmasterId == taskmasterId);
    }

    private static IReadOnlyList<ViewTaskRecord> LoadViewTasks(string viewTasksPath)
    {
        using var stream = File.OpenRead(viewTasksPath);
        using var document = JsonDocument.Parse(stream);

        var tasks = new List<ViewTaskRecord>();

        foreach (var taskElement in EnumerateViewTasks(document.RootElement))
        {
            if (!TryReadTaskmasterId(taskElement, out var taskmasterId))
            {
                continue;
            }

            tasks.Add(new ViewTaskRecord(
                TaskmasterId: taskmasterId,
                Title: ReadString(taskElement, "title"),
                Description: ReadString(taskElement, "description"),
                AdrRefs: ReadStringArray(taskElement, "adr_refs"),
                ChapterRefs: ReadStringArray(taskElement, "chapter_refs"),
                OverlayRefs: ReadStringArray(taskElement, "overlay_refs"),
                TestRefs: ReadStringArray(taskElement, "test_refs"),
                ContractRefs: ReadStringArray(taskElement, "contractRefs"),
                ArtifactRefs: ReadStringArray(taskElement, "artifactRefs")));
        }

        return tasks;
    }

    private static IEnumerable<JsonElement> EnumerateMasterTasks(JsonElement rootElement)
    {
        if (rootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var taskElement in rootElement.EnumerateArray())
            {
                if (taskElement.ValueKind == JsonValueKind.Object)
                {
                    yield return taskElement;
                }
            }

            yield break;
        }

        if (rootElement.ValueKind == JsonValueKind.Object &&
            rootElement.TryGetProperty("master", out var masterElement) &&
            masterElement.ValueKind == JsonValueKind.Object &&
            masterElement.TryGetProperty("tasks", out var tasksElement) &&
            tasksElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var taskElement in tasksElement.EnumerateArray())
            {
                if (taskElement.ValueKind == JsonValueKind.Object)
                {
                    yield return taskElement;
                }
            }

            yield break;
        }

        throw new InvalidDataException("Unsupported master tasks structure.");
    }

    private static IEnumerable<JsonElement> EnumerateViewTasks(JsonElement rootElement)
    {
        if (rootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var taskElement in rootElement.EnumerateArray())
            {
                if (taskElement.ValueKind == JsonValueKind.Object)
                {
                    yield return taskElement;
                }
            }

            yield break;
        }

        if (rootElement.ValueKind == JsonValueKind.Object &&
            rootElement.TryGetProperty("tasks", out var tasksElement) &&
            tasksElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var taskElement in tasksElement.EnumerateArray())
            {
                if (taskElement.ValueKind == JsonValueKind.Object)
                {
                    yield return taskElement;
                }
            }

            yield break;
        }

        throw new InvalidDataException("Unsupported view tasks structure.");
    }

    private static bool TryReadMasterId(JsonElement taskElement, out string id)
    {
        id = string.Empty;
        if (!taskElement.TryGetProperty("id", out var idElement))
        {
            return false;
        }

        if (idElement.ValueKind == JsonValueKind.String)
        {
            id = idElement.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(id);
        }

        if (idElement.ValueKind == JsonValueKind.Number && idElement.TryGetInt32(out var numericId))
        {
            id = numericId.ToString();
            return true;
        }

        return false;
    }

    private static bool TryReadTaskmasterId(JsonElement taskElement, out int taskmasterId)
    {
        taskmasterId = default;
        if (!taskElement.TryGetProperty("taskmaster_id", out var idElement))
        {
            return false;
        }

        if (idElement.ValueKind == JsonValueKind.Number)
        {
            return idElement.TryGetInt32(out taskmasterId);
        }

        if (idElement.ValueKind == JsonValueKind.String)
        {
            return int.TryParse(idElement.GetString(), out taskmasterId);
        }

        return false;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement parentElement, string propertyName)
    {
        if (!parentElement.TryGetProperty(propertyName, out var arrayElement) ||
            arrayElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return arrayElement
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private static string ReadString(JsonElement parentElement, string propertyName)
    {
        if (!parentElement.TryGetProperty(propertyName, out var valueElement) ||
            valueElement.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        return valueElement.GetString() ?? string.Empty;
    }

    private static string ResolveRepositoryRoot()
    {
        var environmentRoot = Environment.GetEnvironmentVariable("REPO_ROOT");
        if (!string.IsNullOrWhiteSpace(environmentRoot))
        {
            var normalizedEnvironmentRoot = Path.GetFullPath(environmentRoot);
            if (File.Exists(Path.Combine(normalizedEnvironmentRoot, "Game.sln")) &&
                Directory.Exists(Path.Combine(normalizedEnvironmentRoot, ".taskmaster", "tasks")))
            {
                return normalizedEnvironmentRoot;
            }
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Game.sln")) &&
                Directory.Exists(Path.Combine(current.FullName, ".taskmaster", "tasks")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Cannot locate repository root for Task 52 governance tests.");
    }

    private static IReadOnlyList<string> ResolveGovernanceEvidenceFiles(
        string repositoryRoot,
        IReadOnlyList<string> artifactRefs)
    {
        var resolved = new List<string>();
        foreach (var artifactRef in artifactRefs)
        {
            var resolvedPath = ResolveArtifactPath(repositoryRoot, artifactRef);
            if (!File.Exists(resolvedPath))
            {
                throw new InvalidDataException($"Missing evidence file for artifact ref: {artifactRef}");
            }

            resolved.Add(resolvedPath);
        }

        return resolved;
    }

    private static string ResolveArtifactPath(string repositoryRoot, string artifactRef)
    {
        if (!artifactRef.Contains("<date>", StringComparison.Ordinal))
        {
            return NormalizeAndCombine(repositoryRoot, artifactRef);
        }

        var marker = "<date>";
        var markerIndex = artifactRef.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return NormalizeAndCombine(repositoryRoot, artifactRef);
        }

        var prefix = artifactRef[..markerIndex].TrimEnd('/', '\\');
        var suffix = artifactRef[(markerIndex + marker.Length)..].TrimStart('/', '\\');
        var prefixPath = NormalizeAndCombine(repositoryRoot, prefix);
        if (!Directory.Exists(prefixPath))
        {
            throw new InvalidDataException($"Evidence date root not found: {prefixPath}");
        }

        var dateDirectories = Directory.GetDirectories(prefixPath)
            .OrderByDescending(Path.GetFileName)
            .ToArray();
        foreach (var dateDirectory in dateDirectories)
        {
            var candidate = Path.GetFullPath(Path.Combine(dateDirectory, suffix.Replace('/', Path.DirectorySeparatorChar)));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidDataException($"Cannot resolve dated artifact ref: {artifactRef}");
    }

    private static string NormalizeAndCombine(string repositoryRoot, string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(repositoryRoot, normalized));
    }

    private static string BuildTask53SummaryJson(
        string taskId,
        string runId,
        string status,
        int flakyCount,
        int retryCount,
        int failureCount,
        int retryMax,
        int failureMax,
        string gateResult)
    {
        return $$"""
        {
          "task_id": "{{taskId}}",
          "run_id": "{{runId}}",
          "status": "{{status}}",
          "generated_at": "2099-12-31T00:00:00Z",
          "classification_counts": {
            "flaky": {{flakyCount}},
            "retry": {{retryCount}},
            "failure": {{failureCount}}
          },
          "thresholds": {
            "retry_max": {{retryMax}},
            "failure_max": {{failureMax}}
          },
          "gate_result": "{{gateResult}}"
        }
        """;
    }

    private static void ValidateTask53SummaryDocument(JsonElement rootElement)
    {
        if (!rootElement.TryGetProperty("task_id", out var taskIdElement) || !string.Equals(taskIdElement.GetString(), "53", StringComparison.Ordinal))
        {
            throw new InvalidDataException("summary.task_id must be '53'.");
        }

        if (!rootElement.TryGetProperty("run_id", out var runIdElement) || string.IsNullOrWhiteSpace(runIdElement.GetString()))
        {
            throw new InvalidDataException("summary.run_id is required.");
        }

        if (!rootElement.TryGetProperty("status", out var statusElement) || string.IsNullOrWhiteSpace(statusElement.GetString()))
        {
            throw new InvalidDataException("summary.status is required.");
        }

        if (!rootElement.TryGetProperty("classification_counts", out var classificationElement) ||
            !classificationElement.TryGetProperty("flaky", out _) ||
            !classificationElement.TryGetProperty("retry", out _) ||
            !classificationElement.TryGetProperty("failure", out _))
        {
            throw new InvalidDataException("summary.classification_counts.{flaky,retry,failure} are required.");
        }

        if (!rootElement.TryGetProperty("thresholds", out var thresholdsElement) ||
            !thresholdsElement.TryGetProperty("retry_max", out _) ||
            !thresholdsElement.TryGetProperty("failure_max", out _))
        {
            throw new InvalidDataException("summary.thresholds.{retry_max,failure_max} are required.");
        }

        if (!rootElement.TryGetProperty("gate_result", out var gateResultElement) || string.IsNullOrWhiteSpace(gateResultElement.GetString()))
        {
            throw new InvalidDataException("summary.gate_result is required.");
        }
    }

    private static AcceptanceItem ParseAcceptanceItem(string acceptanceItem)
    {
        if (string.IsNullOrWhiteSpace(acceptanceItem) || !acceptanceItem.StartsWith("ACC:", StringComparison.Ordinal))
        {
            throw new InvalidDataException("acceptance item must start with ACC: anchor.");
        }

        var firstSpaceIndex = acceptanceItem.IndexOf(' ');
        if (firstSpaceIndex <= 0)
        {
            throw new InvalidDataException("acceptance item must contain anchor and description.");
        }
        var anchor = acceptanceItem[..firstSpaceIndex];

        var markerIndex = acceptanceItem.IndexOf("Refs:", StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            throw new InvalidDataException("acceptance item must contain Refs:.");
        }
        var refsSlice = acceptanceItem[(markerIndex + "Refs:".Length)..];
        var refs = refsSlice
            .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || token.EndsWith(".gd", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (refs.Length == 0)
        {
            throw new InvalidDataException("acceptance refs must not be empty.");
        }
        return new AcceptanceItem(anchor, refs);
    }

    private static StabilitySummary ComputeStabilitySummary(
        IReadOnlyList<ExecutionSample> samples,
        int retryFailureThreshold,
        int hardFailureThreshold)
    {
        var flakyCount = samples.Count(sample => sample.Type == SampleType.RetryableFailure && sample.Attempt < sample.MaxAttempts);
        var retryCount = samples.Count(sample => sample.Type == SampleType.RetryableFailure && sample.Attempt >= sample.MaxAttempts);
        var failureCount = samples.Count(sample => sample.Type == SampleType.NonRetryableFailure);
        var gateResult = retryCount > retryFailureThreshold || failureCount > hardFailureThreshold ? "fail" : "pass";
        return new StabilitySummary(flakyCount, retryCount, failureCount, gateResult);
    }

    private static IReadOnlyList<string> ClassifySamples(IReadOnlyList<ExecutionSample> samples)
    {
        return samples.Select(ClassifySample).ToArray();
    }

    private static string ClassifySample(ExecutionSample sample)
    {
        if (sample.Attempt <= 0 || sample.MaxAttempts <= 0 || sample.Attempt > sample.MaxAttempts)
        {
            throw new InvalidDataException("sample attempt/maxAttempts is invalid.");
        }

        return sample.Type switch
        {
            SampleType.RetryableFailure when sample.Attempt < sample.MaxAttempts => "flaky",
            SampleType.RetryableFailure => "retry",
            SampleType.NonRetryableFailure => "failure",
            _ => "success",
        };
    }

    private static string EvaluateGate(int metric, int threshold)
    {
        return metric <= threshold ? "pass" : "fail";
    }

    private static string EvaluateUnifiedExecution(IReadOnlyList<CategoryExecutionResult> results)
    {
        var requiredCategories = new[] { "unit", "godot", "acceptance" };
        var byCategory = results.ToDictionary(result => result.Category, StringComparer.Ordinal);

        if (requiredCategories.Any(category => !byCategory.ContainsKey(category)))
        {
            return "fail";
        }

        var runIds = byCategory.Values.Select(item => item.RunId).Distinct(StringComparer.Ordinal).ToArray();
        if (runIds.Length != 1)
        {
            return "fail";
        }

        if (byCategory.Values.Any(item => !item.Executed))
        {
            return "fail";
        }

        return byCategory.Values.All(item => item.Passed) ? "pass" : "fail";
    }

    private static string EvaluateUnifiedExecutionWithEvidence(
        IReadOnlyList<CategoryExecutionResult> executionResults,
        IReadOnlyList<EvidenceArtifact> evidenceArtifacts)
    {
        return EvaluateUnifiedExecutionWithEvidenceDetailed(
            executionResults,
            evidenceArtifacts,
            summaryDate: "1970-01-01").Status;
    }

    private static UnifiedExecutionDecision EvaluateUnifiedExecutionWithEvidenceDetailed(
        IReadOnlyList<CategoryExecutionResult> executionResults,
        IReadOnlyList<EvidenceArtifact> evidenceArtifacts,
        string summaryDate)
    {
        var executionGate = EvaluateUnifiedExecution(executionResults);
        var summaryPath = BuildTask55SummaryPath(summaryDate);
        if (!string.Equals(executionGate, "pass", StringComparison.Ordinal))
        {
            return new UnifiedExecutionDecision("fail", "execution_result_failed", "execution", summaryPath);
        }

        var requiredCategories = new[] { "unit", "godot", "acceptance" };
        var executionByCategory = executionResults.ToDictionary(result => result.Category, StringComparer.Ordinal);
        var evidenceByCategory = evidenceArtifacts.ToDictionary(result => result.Category, StringComparer.Ordinal);

        if (requiredCategories.Any(category => !evidenceByCategory.ContainsKey(category)))
        {
            return new UnifiedExecutionDecision("fail", "evidence_category_missing", "evidence", summaryPath);
        }

        var expectedRunId = executionByCategory["unit"].RunId;
        var expectedDate = evidenceByCategory["unit"].Date;
        if (string.IsNullOrWhiteSpace(expectedRunId) || string.IsNullOrWhiteSpace(expectedDate))
        {
            return new UnifiedExecutionDecision("fail", "evidence_round_metadata_missing", "unit", summaryPath);
        }

        foreach (var category in requiredCategories)
        {
            var evidence = evidenceByCategory[category];
            if (!evidence.Passed)
            {
                return new UnifiedExecutionDecision("fail", "evidence_failed", category, summaryPath);
            }

            if (!string.Equals(evidence.RunId, expectedRunId, StringComparison.Ordinal))
            {
                return new UnifiedExecutionDecision("fail", "evidence_run_id_mismatch", category, summaryPath);
            }

            if (!string.Equals(evidence.Date, expectedDate, StringComparison.Ordinal))
            {
                return new UnifiedExecutionDecision("fail", "evidence_date_mismatch", category, summaryPath);
            }

            if (string.IsNullOrWhiteSpace(evidence.RunId) || string.IsNullOrWhiteSpace(evidence.Date))
            {
                return new UnifiedExecutionDecision("fail", "evidence_round_metadata_missing", category, summaryPath);
            }
        }

        return new UnifiedExecutionDecision("pass", string.Empty, string.Empty, summaryPath);
    }

    private static string BuildTask55SummaryPath(string summaryDate)
    {
        return $"logs/ci/{summaryDate}/v11-task-55/summary.json";
    }

    private static string CreateTask55MismatchSummary(UnifiedExecutionDecision decision)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["task_id"] = "55",
            ["status"] = decision.Status,
            ["summary_path"] = decision.SummaryPath,
            ["reason"] = decision.Reason,
            ["conflict_source"] = decision.ConflictSource,
        };
        return JsonSerializer.Serialize(payload);
    }

    private static string CreateUnifiedOverallSummary(UnifiedExecutionDecision decision)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["task_id"] = "55",
            ["overall_verdict"] = decision.Status,
            ["reason"] = decision.Reason,
            ["conflict_source"] = decision.ConflictSource,
            ["summary_path"] = decision.SummaryPath,
        };
        return JsonSerializer.Serialize(payload);
    }

    private static void ProduceTask55Summary(
        string summaryPath,
        string runId,
        string date,
        UnifiedExecutionDecision decision)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(summaryPath) ?? throw new InvalidDataException("summary parent is null"));
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["task_id"] = "55",
            ["run_id"] = runId,
            ["date"] = date,
            ["overall_verdict"] = decision.Status,
            ["reason"] = decision.Reason,
            ["conflict_source"] = decision.ConflictSource,
            ["summary_path"] = decision.SummaryPath,
        };
        File.WriteAllText(summaryPath, JsonSerializer.Serialize(payload));
    }

    private static void AssertMasterRefsAreParsable(MasterTaskRecord task, string repositoryRoot)
    {
        task.AdrRefs.Should().NotBeEmpty();
        task.AdrRefs.Should().OnlyContain(value => IsAdrRefToken(value));

        task.ChapterRefs.Should().NotBeEmpty();
        task.ChapterRefs.Should().OnlyContain(value => IsChapterRefToken(value));

        task.OverlayRef.Should().NotBeNullOrWhiteSpace();
        AssertRepositoryFileExists(repositoryRoot, task.OverlayRef);

        task.TestRefs.Should().NotBeEmpty();
        task.TestRefs.Should().OnlyContain(value => IsAllowedTestFileRef(value));
        foreach (var testRef in task.TestRefs)
        {
            AssertRepositoryFileExists(repositoryRoot, testRef);
        }
    }

    private static void AssertViewRefsAreParsable(ViewTaskRecord task, string repositoryRoot)
    {
        task.AdrRefs.Should().NotBeEmpty();
        task.AdrRefs.Should().OnlyContain(value => IsAdrRefToken(value));

        task.ChapterRefs.Should().NotBeEmpty();
        task.ChapterRefs.Should().OnlyContain(value => IsChapterRefToken(value));

        task.OverlayRefs.Should().NotBeEmpty();
        foreach (var overlayRef in task.OverlayRefs)
        {
            AssertRepositoryFileExists(repositoryRoot, overlayRef);
        }

        task.TestRefs.Should().NotBeEmpty();
        task.TestRefs.Should().OnlyContain(value => IsAllowedTestFileRef(value));
        foreach (var testRef in task.TestRefs)
        {
            AssertRepositoryFileExists(repositoryRoot, testRef);
        }
    }

    private static bool IsAdrRefToken(string value)
    {
        return value.StartsWith("ADR-", StringComparison.Ordinal) &&
               value.Length == 8 &&
               value.Skip(4).All(char.IsDigit);
    }

    private static bool IsChapterRefToken(string value)
    {
        return value.StartsWith("CH", StringComparison.Ordinal) &&
               value.Length == 4 &&
               value.Skip(2).All(char.IsDigit);
    }

    private static bool IsAllowedTestFileRef(string value)
    {
        return value.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
               value.EndsWith(".gd", StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertRepositoryFileExists(string repositoryRoot, string relativePath)
    {
        relativePath.Should().NotBeNullOrWhiteSpace();
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(repositoryRoot, normalized));
        File.Exists(fullPath).Should().BeTrue($"expected referenced file to exist: {relativePath}");
    }

    private sealed record MasterTaskRecord(
        int TaskmasterId,
        string Id,
        string Title,
        string Description,
        IReadOnlyList<string> Acceptance,
        IReadOnlyList<string> AdrRefs,
        IReadOnlyList<string> ChapterRefs,
        string OverlayRef,
        IReadOnlyList<string> TestRefs,
        IReadOnlyList<string> ContractRefs,
        IReadOnlyList<string> ArtifactRefs);

    private sealed record ViewTaskRecord(
        int TaskmasterId,
        string Title,
        string Description,
        IReadOnlyList<string> AdrRefs,
        IReadOnlyList<string> ChapterRefs,
        IReadOnlyList<string> OverlayRefs,
        IReadOnlyList<string> TestRefs,
        IReadOnlyList<string> ContractRefs,
        IReadOnlyList<string> ArtifactRefs);

    private sealed record AcceptanceItem(string Anchor, IReadOnlyList<string> Refs);

    private sealed record StabilitySummary(int FlakyCount, int RetryCount, int FailureCount, string GateResult);

    private sealed record CategoryExecutionResult(string Category, string RunId, bool Executed, bool Passed)
    {
        public static CategoryExecutionResult Pass(string category, string runId)
        {
            return new CategoryExecutionResult(category, runId, Executed: true, Passed: true);
        }

        public static CategoryExecutionResult Fail(string category, string runId)
        {
            return new CategoryExecutionResult(category, runId, Executed: true, Passed: false);
        }

        public static CategoryExecutionResult NotExecuted(string category, string runId)
        {
            return new CategoryExecutionResult(category, runId, Executed: false, Passed: false);
        }
    }

    private sealed record EvidenceArtifact(string Category, string RunId, string Date, bool Passed)
    {
        public static EvidenceArtifact Pass(string category, string runId, string date)
        {
            return new EvidenceArtifact(category, runId, date, Passed: true);
        }
    }

    private sealed record UnifiedExecutionDecision(string Status, string Reason, string ConflictSource, string SummaryPath);

    private sealed record ExecutionSample(SampleType Type, int Attempt, int MaxAttempts)
    {
        public static ExecutionSample RetryableFailure(int attempt, int maxAttempts)
        {
            return new ExecutionSample(SampleType.RetryableFailure, attempt, maxAttempts);
        }

        public static ExecutionSample NonRetryableFailure()
        {
            return new ExecutionSample(SampleType.NonRetryableFailure, Attempt: 1, MaxAttempts: 1);
        }

        public static ExecutionSample Success()
        {
            return new ExecutionSample(SampleType.Success, Attempt: 1, MaxAttempts: 1);
        }
    }

    private enum SampleType
    {
        Success = 0,
        RetryableFailure = 1,
        NonRetryableFailure = 2,
    }
}
