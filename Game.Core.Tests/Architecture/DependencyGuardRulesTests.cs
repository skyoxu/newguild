using System;
using System.Linq;
using FluentAssertions;
using Game.Core.Domain;
using Xunit;

namespace Game.Core.Tests.Architecture;

public sealed class DependencyGuardRulesTests
{
    [Fact]
    public void GameCoreAssembly_ShouldNotReference_GodotAssembly()
    {
        // This is a deterministic guardrail: Game.Core must remain pure .NET (no Godot SDK references).
        var referenced = typeof(Guild).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

        referenced.Should().NotContain("GodotSharp");
        referenced.Should().NotContain("GodotSharpEditor");
        referenced.Should().NotContain("Godot");
    }

    [Fact]
    public void GameCoreTypes_ShouldNotUse_GodotNamespace()
    {
        // Source-level scanning is handled by scripts/python/dependency_guard.py.
        // This runtime reflection check is a fast sanity gate for CI.
        var coreAssembly = typeof(Guild).Assembly;
        var godotTypes = coreAssembly.GetTypes().Where(t => (t.Namespace ?? string.Empty).StartsWith("Godot", StringComparison.Ordinal));

        godotTypes.Should().BeEmpty();
    }
}

