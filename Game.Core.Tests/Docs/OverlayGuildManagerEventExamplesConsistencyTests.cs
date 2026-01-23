using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.Docs;

public sealed class OverlayGuildManagerEventExamplesConsistencyTests
{
    private const string ExpectedCoreGuildCreatedEventType = "core.guild.created";

    // ACC:T42.3
    [Fact]
    public void Should_Find_At_Least_One_EventType_Constant_In_Game_Assemblies()
    {
        var eventTypes = EventTypeDiscovery.GetAllEventTypesFromGameAssemblies();

        eventTypes.Should().NotBeEmpty("the project should expose at least one contract EventType constant");
        eventTypes.Should().OnlyContain(t => !string.IsNullOrWhiteSpace(t));
    }

    [Fact]
    public void Should_Not_Have_Any_EventType_With_Game_Prefix()
    {
        var eventTypes = EventTypeDiscovery.GetAllEventTypesFromGameAssemblies();

        eventTypes.Should().NotContain(t => t.StartsWith("game.", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_Contain_Core_Guild_Created_EventType()
    {
        var eventTypes = EventTypeDiscovery.GetAllEventTypesFromGameAssemblies();

        eventTypes.Should().Contain(ExpectedCoreGuildCreatedEventType);
    }

    private static class EventTypeDiscovery
    {
        public static IReadOnlyList<string> GetAllEventTypesFromGameAssemblies()
        {
            var assemblies = LoadReferencedGameAssemblies();
            var eventTypes = new HashSet<string>(StringComparer.Ordinal);

            foreach (var assembly in assemblies)
            {
                foreach (var type in SafeGetTypes(assembly))
                {
                    foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                    {
                        if (!string.Equals(field.Name, "EventType", StringComparison.Ordinal))
                            continue;

                        if (field.FieldType != typeof(string))
                            continue;

                        if (!field.IsLiteral || field.IsInitOnly)
                            continue;

                        if (field.GetRawConstantValue() is string value && !string.IsNullOrWhiteSpace(value))
                            eventTypes.Add(value);
                    }
                }
            }

            return eventTypes.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }

        private static IReadOnlyList<Assembly> LoadReferencedGameAssemblies()
        {
            var testAssembly = typeof(OverlayGuildManagerEventExamplesConsistencyTests).Assembly;
            var loadFailures = new List<string>();

            foreach (var reference in testAssembly.GetReferencedAssemblies())
            {
                var name = reference.Name;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (!name.StartsWith("Game.", StringComparison.Ordinal))
                    continue;

                try
                {
                    Assembly.Load(reference);
                }
                catch (Exception ex)
                {
                    loadFailures.Add($"{reference.FullName}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            loadFailures.Should().BeEmpty("contract discovery requires referenced Game.* assemblies to load deterministically");

            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .Where(a =>
                {
                    var name = a.GetName().Name ?? string.Empty;
                    return name.StartsWith("Game.", StringComparison.Ordinal)
                        && !name.EndsWith(".Tests", StringComparison.Ordinal);
                })
                .ToArray();
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t is not null)!;
            }
        }
    }
}
