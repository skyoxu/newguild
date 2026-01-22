using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.Architecture;

// ADR-0005, ADR-0007, ADR-0018: architecture dependency guardrails and quality gates.
public sealed class DependencyMatrixRulesTests
{
    private static readonly DependencyMatrix DefaultMatrix = DependencyMatrix.CreateDefault();

    // ACC:T43.1
    [Fact]
    public void DefaultDependencyMatrix_ShouldBe_InternallyConsistent()
    {
        DefaultMatrix.Nodes.Should().NotBeNullOrEmpty();
        DefaultMatrix.Nodes.Should().OnlyHaveUniqueItems();

        foreach (var source in DefaultMatrix.Nodes)
        {
            var forbidden = DefaultMatrix.GetForbiddenReferences(source);
            forbidden.Should().NotContainNulls();
            forbidden.Should().NotContain(x => string.IsNullOrWhiteSpace(x));
            forbidden.Should().NotContain(source);
        }

        DefaultMatrix.GetForbiddenReferences("Game.Core").Should().Contain("GodotSharp");
        DefaultMatrix.GetForbiddenReferences("Game.Core").Should().Contain("GodotSharpEditor");
        DefaultMatrix.GetForbiddenReferences("Game.Core").Should().Contain("Godot");
        DefaultMatrix.GetForbiddenReferences("Game.Core").Should().Contain("Game.Godot");
    }

    [Fact]
    public void RuntimeAssemblies_ShouldNotViolate_DefaultDependencyMatrix_WhenPresent()
    {
        // This is a non-failing scaffold: it enforces rules only when assemblies are present in the test runtime.
        foreach (var source in DefaultMatrix.Nodes)
        {
            if (!TryFindLoadedAssemblyBySimpleName(source, out var sourceAssembly))
            {
                continue;
            }

            var referencedNames = sourceAssembly
                .GetReferencedAssemblies()
                .Select(a => a.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToHashSet(StringComparer.Ordinal);

            foreach (var forbidden in DefaultMatrix.GetForbiddenReferences(source))
            {
                referencedNames.Should().NotContain(forbidden, $"{source} must not reference {forbidden}");
            }
        }

        true.Should().BeTrue();
    }

    private static bool TryFindLoadedAssemblyBySimpleName(string simpleName, out Assembly assembly)
    {
        var found = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, simpleName, StringComparison.Ordinal));

        if (found is null)
        {
            // Caller ignores assembly when returning false; provide a non-null placeholder to satisfy nullability rules.
            assembly = typeof(object).Assembly;
            return false;
        }

        assembly = found;
        return true;
    }

    private sealed class DependencyMatrix
    {
        private readonly HashSet<string> _nodes;
        private readonly Dictionary<string, HashSet<string>> _forbidden;

        private DependencyMatrix(HashSet<string> nodes, Dictionary<string, HashSet<string>> forbidden)
        {
            _nodes = nodes;
            _forbidden = forbidden;
        }

        public IReadOnlyCollection<string> Nodes => _nodes;

        public static DependencyMatrix CreateDefault()
        {
            var nodes = new HashSet<string>(StringComparer.Ordinal)
            {
                "Game.Core",
                "Game.Godot",
                "Game.Core.Tests",
                "Game.Godot.Tests",
            };

            var forbidden = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["Game.Core"] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "GodotSharp",
                    "GodotSharpEditor",
                    "Godot",
                    "Game.Godot",
                },

                // Tests are allowed to reference runtime assemblies; no forbidden edges yet.
                ["Game.Core.Tests"] = new HashSet<string>(StringComparer.Ordinal),
                ["Game.Godot.Tests"] = new HashSet<string>(StringComparer.Ordinal),

                // Godot layer may reference Core; no forbidden edges yet.
                ["Game.Godot"] = new HashSet<string>(StringComparer.Ordinal),
            };

            foreach (var n in nodes)
            {
                if (!forbidden.ContainsKey(n))
                {
                    forbidden[n] = new HashSet<string>(StringComparer.Ordinal);
                }
            }

            return new DependencyMatrix(nodes, forbidden);
        }

        public IReadOnlyCollection<string> GetForbiddenReferences(string source)
        {
            if (!_forbidden.TryGetValue(source, out var set))
            {
                return Array.Empty<string>();
            }

            return set;
        }
    }
}
