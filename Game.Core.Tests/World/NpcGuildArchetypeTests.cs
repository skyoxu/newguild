#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.World
{
    public sealed class NpcGuildArchetypeTests
    {
        private const string SampleContentJson = """
{
  "archetypes": [
    { "id": "merchant_guild" },
    { "id": "thieves_guild" }
  ]
}
""";

        // ACC:T41.1
        [Fact]
        public void Should_Load_NpcGuildArchetypes_From_ContentJson()
        {
            var catalog = LoadCatalogOrFail(SampleContentJson);
            var items = ExtractArchetypeItems(catalog);

            items.Should().HaveCount(2, "the loader should be data-driven and return one item per archetype entry");

            var ids = items
                .Select(TryGetArchetypeId)
                .ToArray();

            ids.Should().Contain(new[] { "merchant_guild", "thieves_guild" }, "archetypes should preserve ids from content JSON");
        }

        // ACC:T41.3
        [Fact]
        public void Should_Expose_Archetypes_For_World_Generation_Consumption()
        {
            var catalog = LoadCatalogOrFail(SampleContentJson);

            var found = TryLookupById(catalog, "merchant_guild", out var archetype);
            found.Should().BeTrue("world generation should be able to resolve an archetype by id (port-friendly lookup)");

            archetype.Should().NotBeNull();
            TryGetArchetypeId(archetype!).Should().Be("merchant_guild");
        }

        private static object LoadCatalogOrFail(string json)
        {
            var coreAssembly = TryGetOrLoadAssembly("Game.Core");
            coreAssembly.Should().NotBeNull("Game.Core assembly should be referenced by this test project");

            var type = FindLoaderType(coreAssembly!);
            type.Should().NotBeNull(
                "an NPC guild archetype loader/catalog type must exist to load content JSON (e.g., Game.Core.World.NpcGuildArchetypeCatalog)"
            );

            var method = FindLoadMethod(type!);
            method.Should().NotBeNull(
                "a public loader method must exist (e.g., LoadFromContentJson(string json) or LoadFromJson(string json))"
            );

            var result = method!.Invoke(null, new object[] { json });
            result.Should().NotBeNull("loading content JSON should produce a non-null catalog/collection");

            return result!;
        }

        private static Assembly? TryGetOrLoadAssembly(string simpleName)
        {
            var alreadyLoaded = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, simpleName, StringComparison.Ordinal));

            if (alreadyLoaded != null)
            {
                return alreadyLoaded;
            }

            try
            {
                return Assembly.Load(simpleName);
            }
            catch
            {
                return null;
            }
        }

        private static Type? FindLoaderType(Assembly coreAssembly)
        {
            var preferredFullNames = new[]
            {
                "Game.Core.World.NpcGuildArchetypeCatalog",
                "Game.Core.World.NpcGuildArchetypeLoader",
                "Game.Core.World.NpcGuildArchetypeRegistry",
                "Game.Core.Content.NpcGuildArchetypeCatalog",
                "Game.Core.Content.NpcGuildArchetypeLoader",
            };

            foreach (var fullName in preferredFullNames)
            {
                var candidateType = coreAssembly.GetType(fullName, throwOnError: false, ignoreCase: false);
                if (candidateType != null)
                {
                    return candidateType;
                }
            }

            try
            {
                return coreAssembly
                    .GetTypes()
                    .FirstOrDefault(t =>
                        t.IsClass
                        && t.IsPublic
                        && t.Name.Contains("NpcGuildArchetype", StringComparison.Ordinal)
                        && t.GetMethods(BindingFlags.Public | BindingFlags.Static)
                            .Any(m =>
                                m.GetParameters().Length == 1
                                && m.GetParameters()[0].ParameterType == typeof(string)
                                && m.ReturnType != typeof(void)
                            ));
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types
                    .Where(t => t != null)
                    .Cast<Type>()
                    .FirstOrDefault(t =>
                        t.IsClass
                        && t.IsPublic
                        && t.Name.Contains("NpcGuildArchetype", StringComparison.Ordinal));
            }
        }

        private static MethodInfo? FindLoadMethod(Type loaderType)
        {
            var candidateNames = new[]
            {
                "LoadFromContentJson",
                "LoadFromJson",
                "FromContentJson",
                "FromJson",
                "ParseContentJson",
                "ParseJson",
            };

            foreach (var name in candidateNames)
            {
                var method = loaderType.GetMethod(
                    name,
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(string) },
                    modifiers: null
                );

                if (method != null && method.ReturnType != typeof(void))
                {
                    return method;
                }
            }

            return loaderType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m =>
                    m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType == typeof(string)
                    && m.ReturnType != typeof(void));
        }

        private static IReadOnlyList<object> ExtractArchetypeItems(object catalogOrCollection)
        {
            if (catalogOrCollection is IDictionary dict)
            {
                var items = new List<object>();
                foreach (DictionaryEntry entry in dict)
                {
                    if (entry.Value != null)
                    {
                        items.Add(entry.Value);
                    }
                }

                return items;
            }

            if (catalogOrCollection is IEnumerable enumerable && catalogOrCollection is not string)
            {
                var items = new List<object>();
                foreach (var item in enumerable)
                {
                    if (item != null)
                    {
                        items.Add(item);
                    }
                }

                return items;
            }

            return Array.Empty<object>();
        }

        private static string? TryGetArchetypeId(object archetype)
        {
            var type = archetype.GetType();

            var prop = type.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)
                       ?? type.GetProperty("id", BindingFlags.Public | BindingFlags.Instance)
                       ?? type.GetProperty("ArchetypeId", BindingFlags.Public | BindingFlags.Instance)
                       ?? type.GetProperty("Key", BindingFlags.Public | BindingFlags.Instance);

            if (prop != null && prop.PropertyType == typeof(string))
            {
                return (string?)prop.GetValue(archetype);
            }

            var field = type.GetField("Id", BindingFlags.Public | BindingFlags.Instance)
                        ?? type.GetField("id", BindingFlags.Public | BindingFlags.Instance);

            if (field != null && field.FieldType == typeof(string))
            {
                return (string?)field.GetValue(archetype);
            }

            return null;
        }

        private static bool TryLookupById(object catalogOrCollection, string id, out object? archetype)
        {
            archetype = null;

            if (catalogOrCollection is IDictionary dict)
            {
                if (!dict.Contains(id))
                {
                    return false;
                }

                archetype = dict[id];
                return archetype != null;
            }

            var type = catalogOrCollection.GetType();

            var tryGetMethod = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m =>
                    (string.Equals(m.Name, "TryGetById", StringComparison.Ordinal)
                     || string.Equals(m.Name, "TryGetValue", StringComparison.Ordinal))
                    && m.GetParameters().Length == 2
                    && m.GetParameters()[0].ParameterType == typeof(string)
                    && m.GetParameters()[1].ParameterType.IsByRef
                    && m.ReturnType == typeof(bool));

            if (tryGetMethod != null)
            {
                var args = new object?[] { id, null };
                var ok = (bool)tryGetMethod.Invoke(catalogOrCollection, args)!;
                archetype = args[1];
                return ok && archetype != null;
            }

            var indexer = type.GetProperty("Item", BindingFlags.Public | BindingFlags.Instance, binder: null, returnType: null, types: new[] { typeof(string) }, modifiers: null);
            if (indexer != null)
            {
                try
                {
                    archetype = indexer.GetValue(catalogOrCollection, new object[] { id });
                    return archetype != null;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }
    }
}
