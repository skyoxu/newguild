using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using FluentAssertions.Execution;
using Game.Core.Tests.Docs.Support;
using Xunit;

namespace Game.Core.Tests.Docs;

public sealed class Task22DocsLinksAcceptanceTests
{
    private const string ThisTestFilePath = "Game.Core.Tests/Docs/Task22DocsLinksAcceptanceTests.cs";

    private const string PrdPath = "docs/prd.txt";
    private const string OverlayIndexPath = "docs/architecture/overlays/PRD-Guild-Manager/08/_index.md";
    private const string OverlayFeatureSlicePath = "docs/architecture/overlays/PRD-Guild-Manager/08/08-FeatureSlice-Guild-Manager.md";
    private const string OverlayAcceptanceChecklistPath = "docs/architecture/overlays/PRD-Guild-Manager/08/ACCEPTANCE_CHECKLIST.md";

    private static readonly string[] TaskmasterTasksJsonCandidates =
    [
        ".taskmaster/tasks/tasks.json",
        "tasks/tasks.json",
    ];

    private static readonly string[] TaskmasterTasksBackJsonCandidates =
    [
        ".taskmaster/tasks/tasks_back.json",
        "tasks/tasks_back.json",
    ];

    private const string TaskLinksValidateScriptPath = "scripts/python/task_links_validate.py";
    private const string AcceptanceCheckScriptPath = "scripts/sc/acceptance_check.py";

    private static readonly string[] RequiredAdrRefs =
    [
        "ADR-0004",
        "ADR-0005",
        "ADR-0018",
        "ADR-0019",
    ];

    private static readonly string[] RequiredChapterRefs =
    [
        "CH01",
        "CH02",
        "CH03",
        "CH04",
        "CH06",
        "CH07",
    ];

    // ACC:T22.1
    [Fact]
    public void ShouldRequirePrdAndOverlayPrdRefs_WhenValidatingGuildManagerBaseline()
    {
        var repo = RepoRootLocator.FindRepoRoot();

        var prd = RepoFiles.ReadAllTextUtf8(repo, PrdPath);
        prd.Should().Contain("Product Requirements Document - Guild Manager", "PRD must exist and describe the Guild Manager product");
        prd.Should().MatchRegex(
            new Regex(@"3\.2\.1\s+公会管理模块", RegexOptions.CultureInvariant),
            "PRD must include a Guild Manager module section (used as the baseline for overlay alignment)");

        var indexMarkdown = RepoFiles.ReadAllTextUtf8(repo, OverlayIndexPath);
        var featureMarkdown = RepoFiles.ReadAllTextUtf8(repo, OverlayFeatureSlicePath);

        var indexFront = MarkdownFrontMatter.TryParse(indexMarkdown);
        var featureFront = MarkdownFrontMatter.TryParse(featureMarkdown);

        using var _ = new AssertionScope();

        DocRef.EqualsNormalized(indexFront.GetScalar("PRD-ID") ?? string.Empty, "PRD-Guild-Manager").Should().BeTrue(
            "{0} must declare PRD-ID: PRD-Guild-Manager in front-matter",
            OverlayIndexPath);
        DocRef.EqualsNormalized(featureFront.GetScalar("PRD-ID") ?? string.Empty, "PRD-Guild-Manager").Should().BeTrue(
            "{0} must declare PRD-ID: PRD-Guild-Manager in front-matter",
            OverlayFeatureSlicePath);

        indexFront.GetList("PRD-Refs", "PRDRefs", "prd_refs")
            .Select(DocRef.Normalize)
            .Should()
            .Contain(DocRef.Normalize(PrdPath), "{0} must reference {1} so PRD->Overlay alignment can be audited", OverlayIndexPath, PrdPath);

        featureFront.GetList("PRD-Refs", "PRDRefs", "prd_refs")
            .Select(DocRef.Normalize)
            .Should()
            .Contain(DocRef.Normalize(PrdPath), "{0} must reference {1} so PRD->Overlay alignment can be audited", OverlayFeatureSlicePath, PrdPath);

        indexMarkdown.Should().Contain(
            "08-FeatureSlice-Guild-Manager.md",
            "{0} must include an entry for the Guild Manager feature slice",
            OverlayIndexPath);
    }

    // ACC:T22.2
    [Fact]
    public void ShouldRequireConsistentAdrRefs_WhenComparingFeatureSliceAndAcceptanceChecklist()
    {
        var repo = RepoRootLocator.FindRepoRoot();
        var featureSlice = RepoFiles.ReadAllTextUtf8(repo, OverlayFeatureSlicePath);
        var checklist = RepoFiles.ReadAllTextUtf8(repo, OverlayAcceptanceChecklistPath);

        var featureFront = MarkdownFrontMatter.TryParse(featureSlice);
        var checklistFront = MarkdownFrontMatter.TryParse(checklist);

        var featureRefs = MarkdownRefs.CollectAdrRefs(featureSlice, featureFront);
        var checklistRefs = MarkdownRefs.CollectAdrRefs(checklist, checklistFront);

        using var _ = new AssertionScope();

        featureRefs.Should().Contain(RequiredAdrRefs, "{0} must reference required ADRs", OverlayFeatureSlicePath);
        checklistRefs.Should().Contain(RequiredAdrRefs, "{0} must reference required ADRs", OverlayAcceptanceChecklistPath);

        var missingInChecklist = featureRefs.Except(checklistRefs).OrderBy(x => x).ToArray();
        missingInChecklist.Should().BeEmpty(
            "{0} must include all ADR references used by {1} to keep references consistent",
            OverlayAcceptanceChecklistPath,
            OverlayFeatureSlicePath);
    }

    // ACC:T22.3
    [Fact]
    public void ShouldRequireTask22Backlinks_WhenCheckingOverlayAdrAndChapterRefs()
    {
        var repo = RepoRootLocator.FindRepoRoot();

        var tasksJsonPath = RepoFiles.ResolveFirstExisting(repo, TaskmasterTasksJsonCandidates);
        var tasksBackJsonPath = RepoFiles.ResolveFirstExisting(repo, TaskmasterTasksBackJsonCandidates);

        using var tasksDoc = JsonDocument.Parse(RepoFiles.ReadAllTextUtf8(repo, tasksJsonPath));
        using var tasksBackDoc = JsonDocument.Parse(RepoFiles.ReadAllTextUtf8(repo, tasksBackJsonPath));

        var task22 = TaskmasterJson.FindTaskById(tasksDoc.RootElement, "22");
        task22.Should().NotBeNull($"{tasksJsonPath} must contain master.tasks[].id == \"22\"");

        using var _ = new AssertionScope();

        var adrRefs = TaskmasterJson.ReadStringList(task22!.Value, "adrRefs", "adr_refs", "ADR-Refs", "ADR_Refs");
        adrRefs.Should().Contain(RequiredAdrRefs, "Task 22 must declare required ADR refs");

        var chapterRefs = TaskmasterJson.ReadStringList(task22.Value, "archRefs", "chapterRefs", "chapter_refs", "CH-Refs", "CH_Refs");
        chapterRefs.Should().Contain(RequiredChapterRefs, "Task 22 must declare required architecture chapter refs");

        var overlay = TaskmasterJson.ReadString(task22.Value, "overlay", "overlayPath", "overlay_path");
        overlay.Should().NotBeNullOrWhiteSpace("Task 22 must declare an overlay document path");
        DocRef.Normalize(overlay!).Should().Be(DocRef.Normalize(OverlayFeatureSlicePath), "Task 22 overlay must point at the feature slice document");

        var backRecord = TaskmasterJson.FindBackRecordByTaskmasterId(tasksBackDoc.RootElement, 22);
        backRecord.Should().NotBeNull($"{tasksBackJsonPath} must contain a record where taskmaster_id == 22");

        var overlayRefs = TaskmasterJson.ReadStringList(backRecord!.Value, "overlay_refs", "overlayRefs", "overlay-refs");
        overlayRefs.Select(DocRef.Normalize).Should().Contain(
            [DocRef.Normalize(OverlayIndexPath), DocRef.Normalize(OverlayFeatureSlicePath), DocRef.Normalize(OverlayAcceptanceChecklistPath)],
            "Task 22 back-references must include required overlay files");

        var testRefs = TaskmasterJson.ReadStringList(backRecord.Value, "test_refs", "testRefs", "test-refs");
        testRefs.Select(DocRef.Normalize).Should().Contain(DocRef.Normalize(ThisTestFilePath),
            "Task 22 test_refs must include this acceptance test to keep docs and tests aligned");

        var artifactRefs = TaskmasterJson.ReadStringList(backRecord.Value, "artifactRefs", "artifact_refs", "artifact-refs");
        artifactRefs.Select(DocRef.Normalize).Should().Contain(DocRef.Normalize(TaskLinksValidateScriptPath),
            "Task 22 artifactRefs must include task links validator script");
    }

    // ACC:T22.4
    [Fact]
    public void ShouldRequireTaskLinksValidationWiring_WhenCheckingAcceptanceChecklistTestRefs()
    {
        var repo = RepoRootLocator.FindRepoRoot();

        var validateScript = RepoFiles.ReadAllTextUtf8(repo, TaskLinksValidateScriptPath);
        var acceptanceCheck = RepoFiles.ReadAllTextUtf8(repo, AcceptanceCheckScriptPath);

        var checklistMarkdown = RepoFiles.ReadAllTextUtf8(repo, OverlayAcceptanceChecklistPath);
        var checklistFront = MarkdownFrontMatter.TryParse(checklistMarkdown);
        var testRefs = checklistFront.GetList("Test-Refs", "TestRefs", "Test_Refs").Select(DocRef.Normalize).ToArray();

        using var _ = new AssertionScope();

        testRefs.Should().Contain(DocRef.Normalize(ThisTestFilePath),
            "{0} Test-Refs must include this acceptance test to keep docs and tests aligned",
            OverlayAcceptanceChecklistPath);

        var hasLegacyTaskLinksHook = Regex.IsMatch(
            acceptanceCheck,
            @"task[_\-]links[_\-]validate",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var hasOrchestrationHook = Regex.IsMatch(
                acceptanceCheck,
                @"_acceptance_orchestration",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            && Regex.IsMatch(
                acceptanceCheck,
                @"run_registry_steps|build_step_plan",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        (hasLegacyTaskLinksHook || hasOrchestrationHook).Should().BeTrue(
            "{0} must wire task-links validation directly or through orchestration",
            AcceptanceCheckScriptPath);

        validateScript.Should().MatchRegex(
            new Regex(@"validate_view_ref_semantics|check_tasks_all_refs|check_tasks_back_references", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            "{0} must contain recognizable validation entrypoints for links/backrefs",
            TaskLinksValidateScriptPath);
    }

    [Fact]
    public void ShouldRejectPrdSpecificIds_WhenScanningBaseArchitectureDocs()
    {
        var repo = RepoRootLocator.FindRepoRoot();
        var baseDir = Path.Combine(repo, "docs", "architecture", "base");
        Directory.Exists(baseDir).Should().BeTrue("Base architecture directory must exist at {0}", baseDir);

        var allMd = Directory.EnumerateFiles(baseDir, "*.md", SearchOption.AllDirectories).ToArray();
        allMd.Should().NotBeEmpty("Base architecture docs must exist");

        var contaminated = new List<string>();
        foreach (var file in allMd)
        {
            var text = File.ReadAllText(file);
            if (text.Contains("PRD-Guild-Manager", StringComparison.OrdinalIgnoreCase))
            {
                var rel = Path.GetRelativePath(repo, file);
                contaminated.Add(DocRef.Normalize(rel));
            }
        }

        contaminated.Should().BeEmpty("Base docs must not contain overlay-specific PRD IDs");
    }
}
