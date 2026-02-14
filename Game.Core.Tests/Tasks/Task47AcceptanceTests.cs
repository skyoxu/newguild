#nullable enable
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.Tasks;

[Collection("CI")]
[Trait("Category", "CI")]
public sealed class Task47AcceptanceTests
{
    private const string StartScreenRelativePath = "Game.Godot/Scripts/Screens/StartScreen.cs";
    private const string GodotAcceptanceRelativePath = "Tests.Godot/tests/Scenes/test_task47_acceptance.gd";
    private const string ThisTestFilePath = "Game.Core.Tests/Tasks/Task47AcceptanceTests.cs";

    // ACC:T47.1
    [Fact]
    public void Should_Reference_Task47_TestFiles_In_TasksGameplay_TestRefs()
    {
        var repoRoot = FindRepoRoot();
        var tasksGameplayPath = Path.Combine(repoRoot, ".taskmaster", "tasks", "tasks_gameplay.json");

        File.Exists(tasksGameplayPath).Should().BeTrue();

        var json = File.ReadAllText(tasksGameplayPath, Encoding.UTF8);
        using var document = JsonDocument.Parse(json);

        var foundTask = false;
        var hasGodotRef = false;
        var hasCsharpRef = false;

        foreach (var task in document.RootElement.EnumerateArray())
        {
            if (!task.TryGetProperty("taskmaster_id", out var taskMasterIdElement) ||
                taskMasterIdElement.ValueKind != JsonValueKind.Number ||
                taskMasterIdElement.GetInt32() != 47)
            {
                continue;
            }

            foundTask = true;
            if (!task.TryGetProperty("test_refs", out var testRefsElement) ||
                testRefsElement.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            foreach (var reference in testRefsElement.EnumerateArray())
            {
                var value = reference.GetString() ?? string.Empty;
                if (value == GodotAcceptanceRelativePath)
                    hasGodotRef = true;

                if (value == ThisTestFilePath)
                    hasCsharpRef = true;
            }

            break;
        }

        foundTask.Should().BeTrue();
        hasGodotRef.Should().BeTrue();
        hasCsharpRef.Should().BeTrue();
    }

    // ACC:T47.2
    [Fact]
    public void Should_Have_Task47_Anchors_In_Godot_Acceptance_File_RedFirst()
    {
        var repoRoot = FindRepoRoot();
        var acceptancePath = Path.Combine(repoRoot, GodotAcceptanceRelativePath.Replace('/', Path.DirectorySeparatorChar));

        File.Exists(acceptancePath).Should().BeTrue();

        var source = File.ReadAllText(acceptancePath, Encoding.UTF8);
        source.Should().Contain("ACC:T47.1");
        source.Should().Contain("ACC:T47.2");

        source.Should().Contain("ACC:T47.3");
    }

    // ACC:T47.3
    [Fact]
    public void Should_Contain_RealScene_And_Shortcut_DragDrop_Coverage_In_Godot_Test()
    {
        var repoRoot = FindRepoRoot();
        var acceptancePath = Path.Combine(repoRoot, GodotAcceptanceRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var startScreenPath = Path.Combine(repoRoot, StartScreenRelativePath.Replace('/', Path.DirectorySeparatorChar));

        File.Exists(acceptancePath).Should().BeTrue();
        File.Exists(startScreenPath).Should().BeTrue();

        var acceptanceSource = File.ReadAllText(acceptancePath, Encoding.UTF8);
        acceptanceSource.Should().Contain("StartScreen.tscn");
        acceptanceSource.Should().Contain("_new_f1_key_event");
        acceptanceSource.Should().Contain("tooltip")
            .And.Contain("_dispatch_unhandled_input")
            .And.Contain("demos_disabled")
            .And.Contain("invalid_payload")
            .And.Contain("test_real_start_screen_drag_drop_valid_payload_opens_popup_and_emits_allow")
            .And.Contain("_call_can_drop_data");

        var startScreenSource = File.ReadAllText(startScreenPath, Encoding.UTF8);
        startScreenSource.Should().Contain("public override Variant _GetDragData")
            .And.Contain("public override bool _CanDropData")
            .And.Contain("public override void _DropData")
            .And.Contain("Key.F1")
            .And.Contain("ReasonInvalidPayload")
            .And.Contain("ReasonDemosDisabled");
    }

    // ACC:T47.4
    [Fact]
    public void Should_Declare_Task47_Description_With_DragDrop_Shortcut_And_Tooltip_Semantics()
    {
        var repoRoot = FindRepoRoot();
        var tasksGameplayPath = Path.Combine(repoRoot, ".taskmaster", "tasks", "tasks_gameplay.json");

        File.Exists(tasksGameplayPath).Should().BeTrue();

        var json = File.ReadAllText(tasksGameplayPath, Encoding.UTF8);
        using var document = JsonDocument.Parse(json);

        var description = string.Empty;
        foreach (var task in document.RootElement.EnumerateArray())
        {
            if (!task.TryGetProperty("taskmaster_id", out var taskMasterIdElement) ||
                taskMasterIdElement.ValueKind != JsonValueKind.Number ||
                taskMasterIdElement.GetInt32() != 47)
            {
                continue;
            }

            description = task.TryGetProperty("description", out var descriptionElement)
                ? descriptionElement.GetString() ?? string.Empty
                : string.Empty;
            break;
        }

        description.Should().Contain("drag-and-drop", because: "Task 47 requires drag-and-drop semantics.");
        description.Should().Contain("keyboard shortcuts", because: "Task 47 requires keyboard shortcut semantics.");
        description.Should().Contain("tooltip", because: "Task 47 requires tooltip semantics.");
    }

    // ACC:T47.5
    [Fact]
    public void Should_Declare_LogsEvidenceRequirement_In_Task47AcceptanceText()
    {
        var repoRoot = FindRepoRoot();
        var tasksGameplayPath = Path.Combine(repoRoot, ".taskmaster", "tasks", "tasks_gameplay.json");

        File.Exists(tasksGameplayPath).Should().BeTrue();

        var json = File.ReadAllText(tasksGameplayPath, Encoding.UTF8);
        using var document = JsonDocument.Parse(json);

        var containsLogsRequirement = false;
        foreach (var task in document.RootElement.EnumerateArray())
        {
            if (!task.TryGetProperty("taskmaster_id", out var taskMasterIdElement) ||
                taskMasterIdElement.ValueKind != JsonValueKind.Number ||
                taskMasterIdElement.GetInt32() != 47)
            {
                continue;
            }

            if (!task.TryGetProperty("acceptance", out var acceptanceElement) ||
                acceptanceElement.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            foreach (var acceptance in acceptanceElement.EnumerateArray())
            {
                var value = acceptance.GetString() ?? string.Empty;
                if (value.Contains("logs/**", StringComparison.Ordinal))
                {
                    containsLogsRequirement = true;
                    break;
                }
            }

            break;
        }

        containsLogsRequirement.Should().BeTrue();
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Game.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test base directory.");
    }
}
