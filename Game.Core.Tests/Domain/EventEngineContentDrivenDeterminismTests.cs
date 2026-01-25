using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using FluentAssertions;
using Xunit;

namespace Game.Core.Tests.Domain;

public sealed class EventEngineContentDrivenDeterminismTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 01, 01, 12, 00, 00, TimeSpan.Zero);

    // ACC:T29.1
    [Fact]
    public void Should_Produce_Deterministic_Events_Given_Fixed_Time_Seed_And_ContentDriven_Catalog()
    {
        var api = EventEngineTestApi.Discover();
        var catalog = api.CreateSampleCatalog();

        var first = api.GenerateEventFingerprints(catalog, seed: 1337, now: FixedNow, count: 10);
        var second = api.GenerateEventFingerprints(catalog, seed: 1337, now: FixedNow, count: 10);

        first.Should().NotBeEmpty("the engine should produce events when the catalog contains eligible entries");
        first.Should().Equal(second, "fixed seed + fixed time + identical catalog content must yield the same event sequence");
    }

    // ACC:T29.1
    [Fact]
    public void Should_Refuse_To_Run_When_EventCatalog_Is_Null_And_Not_Fall_Back_To_Hud_Defaults()
    {
        var api = EventEngineTestApi.Discover();

        Action act = () => api.GenerateEventFingerprints(catalog: null, seed: 1, now: FixedNow, count: 1);

        act.Should().Throw<ArgumentNullException>("EventEngine must not silently fall back to any HUD-level EmptyEventCatalog");
    }

    private sealed class EventEngineTestApi
    {
        private readonly Type _engineType;
        private readonly Type _catalogType;

        private EventEngineTestApi(Type engineType, Type catalogType)
        {
            _engineType = engineType;
            _catalogType = catalogType;
        }

        public static EventEngineTestApi Discover()
        {
            var assemblies = GetCandidateAssemblies();
            var engineType = FindSingleTypeBySimpleName(assemblies, "EventEngine");
            var catalogType = FindSingleTypeBySimpleName(assemblies, "EventCatalog")
                             ?? FindSingleTypeBySimpleName(assemblies, "IEventCatalog")
                             ?? throw new InvalidOperationException("Could not locate an EventCatalog or IEventCatalog type in loaded assemblies.");

            if (engineType is null)
                throw new InvalidOperationException("Could not locate an EventEngine type in loaded assemblies.");

            return new EventEngineTestApi(engineType, catalogType);
        }

        public object CreateSampleCatalog()
        {
            var json = "{\"version\":1,\"events\":[{\"type\":\"core.event_catalog.loaded\",\"weight\":1},{\"type\":\"core.content.manifest.loaded\",\"weight\":1}]}";

            if (TryCreateCatalogFromString(json, out var fromStringCatalog))
                return fromStringCatalog;

            if (TryCreateEmptyCatalog(out var emptyCatalog) && TryPopulateCatalogInMemory(emptyCatalog))
                return emptyCatalog;

            throw new InvalidOperationException(
                "Unable to create a sample EventCatalog instance. " +
                "Expected either a static factory accepting a string (e.g., FromJson/Parse/Load) or a mutable catalog that can be populated in-memory.");
        }

        public IReadOnlyList<string> GenerateEventFingerprints(object? catalog, int seed, DateTimeOffset now, int count)
        {
            var generator = ResolveGenerator();
            var events = generator(catalog, seed, now, count);
            return events.Select(Fingerprint).ToArray();
        }

        private Func<object?, int, DateTimeOffset, int, IReadOnlyList<object?>> ResolveGenerator()
        {
            var methods = _engineType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => !m.IsSpecialName)
                .ToArray();

            var candidates = methods
                .Select(m => new { Method = m, Score = ScoreMethod(m) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Select(x => x.Method)
                .ToArray();

            foreach (var method in candidates)
            {
                if (TryBuildInvoker(method, out var invoker))
                    return invoker;
            }

            throw new InvalidOperationException(
                "Could not resolve an invokable EventEngine generation method. " +
                "Expected a method that can be called with some combination of: EventCatalog, seed, time, and count.");
        }

        private bool TryBuildInvoker(MethodInfo method, out Func<object?, int, DateTimeOffset, int, IReadOnlyList<object?>> invoker)
        {
            invoker = default!;

            if (!TryCreateEngineFactoryIfNeeded(method, out var engineFactory))
                return false;

            var parameters = method.GetParameters();
            if (!CanSatisfyParameters(parameters, requiresCatalogType: _catalogType))
                return false;

            invoker = (catalog, seed, now, count) =>
            {
                var engineInstance = engineFactory(catalog, seed, now);
                var args = BuildArguments(parameters, catalog, seed, now, count);
                var result = method.Invoke(engineInstance, args);
                return MaterializeEvents(result, count);
            };

            return true;
        }

        private bool TryCreateEngineFactoryIfNeeded(MethodInfo generationMethod, out Func<object?, int, DateTimeOffset, object?> engineFactory)
        {
            if (generationMethod.IsStatic)
            {
                engineFactory = (_, _, _) => null;
                return true;
            }

            var ctors = _engineType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            var ordered = ctors
                .Select(c => new { Ctor = c, Score = ScoreConstructor(c) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Select(x => x.Ctor)
                .ToArray();

            foreach (var ctor in ordered)
            {
                var parameters = ctor.GetParameters();
                if (!CanSatisfyParameters(parameters, requiresCatalogType: _catalogType))
                    continue;

                engineFactory = (catalog, seed, now) =>
                {
                    var args = BuildArguments(parameters, catalog, seed, now, count: 0);
                    return ctor.Invoke(args);
                };

                return true;
            }

            engineFactory = default!;
            return false;
        }

        private static int ScoreMethod(MethodInfo method)
        {
            var name = method.Name;
            var score = 0;

            if (name.Contains("Generate", StringComparison.OrdinalIgnoreCase)) score += 10;
            if (name.Contains("Next", StringComparison.OrdinalIgnoreCase)) score += 8;
            if (name.Contains("Produce", StringComparison.OrdinalIgnoreCase)) score += 8;
            if (name.Contains("Event", StringComparison.OrdinalIgnoreCase)) score += 3;

            if (IsEnumerableReturn(method.ReturnType)) score += 6;
            if (method.ReturnType != typeof(void) && method.ReturnType != typeof(string)) score += 2;

            var ps = method.GetParameters();
            if (ps.Any(p => p.ParameterType == typeof(DateTimeOffset) || p.ParameterType == typeof(DateTime))) score += 4;
            if (ps.Any(p => p.ParameterType == typeof(int) || p.ParameterType == typeof(long))) score += 2;

            return score;
        }

        private int ScoreConstructor(ConstructorInfo ctor)
        {
            var score = 1;
            var ps = ctor.GetParameters();

            if (ps.Any(p => _catalogType.IsAssignableFrom(p.ParameterType))) score += 10;
            if (ps.Any(p => p.ParameterType == typeof(int) || p.ParameterType == typeof(long))) score += 4;
            if (ps.Any(p => p.ParameterType == typeof(Random))) score += 3;
            if (ps.Any(p => LooksLikeTimeProvider(p.ParameterType))) score += 3;

            return score;
        }

        private static IReadOnlyList<object?> MaterializeEvents(object? result, int count)
        {
            if (result is null)
                return Array.Empty<object?>();

            if (result is string)
                return new object?[] { result };

            if (result is IEnumerable enumerable)
            {
                var list = new List<object?>();
                foreach (var item in enumerable)
                {
                    list.Add(item);
                    if (count > 0 && list.Count >= count)
                        break;
                }
                return list;
            }

            return new object?[] { result };
        }

        private static bool IsEnumerableReturn(Type returnType)
        {
            return typeof(IEnumerable).IsAssignableFrom(returnType) && returnType != typeof(string);
        }

        private static bool CanSatisfyParameters(ParameterInfo[] parameters, Type requiresCatalogType)
        {
            foreach (var p in parameters)
            {
                var t = p.ParameterType;

                if (requiresCatalogType.IsAssignableFrom(t))
                    continue;

                if (t == typeof(object))
                    continue;

                if (t == typeof(int) || t == typeof(long))
                    continue;

                if (t == typeof(DateTimeOffset) || t == typeof(DateTime) || t == typeof(TimeSpan))
                    continue;

                if (t == typeof(Random))
                    continue;

                if (t == typeof(CancellationToken))
                    continue;

                if (LooksLikeTimeProvider(t))
                    continue;

                if (t == typeof(Guid))
                    continue;

                if (t.IsEnum)
                    continue;

                return false;
            }

            return true;
        }

        private static object?[] BuildArguments(ParameterInfo[] parameters, object? catalog, int seed, DateTimeOffset now, int count)
        {
            var args = new object?[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                var t = p.ParameterType;
                var n = p.Name ?? string.Empty;

                if (t.IsInstanceOfType(catalog))
                {
                    args[i] = catalog;
                    continue;
                }

                if (t == typeof(object) && catalog is not null)
                {
                    args[i] = catalog;
                    continue;
                }

                if (t == typeof(int))
                {
                    if (n.Contains("seed", StringComparison.OrdinalIgnoreCase)) args[i] = seed;
                    else if (n.Contains("count", StringComparison.OrdinalIgnoreCase) || n.Contains("take", StringComparison.OrdinalIgnoreCase) || n.Contains("limit", StringComparison.OrdinalIgnoreCase) || n.Contains("max", StringComparison.OrdinalIgnoreCase)) args[i] = count;
                    else args[i] = count;
                    continue;
                }

                if (t == typeof(long))
                {
                    if (n.Contains("seed", StringComparison.OrdinalIgnoreCase)) args[i] = (long)seed;
                    else args[i] = (long)count;
                    continue;
                }

                if (t == typeof(DateTimeOffset))
                {
                    args[i] = now;
                    continue;
                }

                if (t == typeof(DateTime))
                {
                    args[i] = now.UtcDateTime;
                    continue;
                }

                if (t == typeof(TimeSpan))
                {
                    args[i] = TimeSpan.Zero;
                    continue;
                }

                if (t == typeof(Random))
                {
                    args[i] = new Random(seed);
                    continue;
                }

                if (t == typeof(CancellationToken))
                {
                    args[i] = default(CancellationToken);
                    continue;
                }

                if (LooksLikeTimeProvider(t))
                {
                    args[i] = CreateFixedTimeProvider(t, now);
                    continue;
                }

                if (t == typeof(Guid))
                {
                    args[i] = Guid.Empty;
                    continue;
                }

                if (t.IsEnum)
                {
                    args[i] = Enum.GetValues(t).Length > 0 ? Enum.GetValues(t).GetValue(0) : Activator.CreateInstance(t);
                    continue;
                }

                args[i] = null;
            }

            return args;
        }

        private bool TryCreateCatalogFromString(string text, out object catalog)
        {
            var methods = _catalogType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.ReturnType != typeof(void))
                .ToArray();

            var names = new[] { "FromJson", "Parse", "ParseJson", "Load", "LoadFromJson", "FromString", "Deserialize" };

            foreach (var name in names)
            {
                foreach (var m in methods.Where(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    var ps = m.GetParameters();
                    if (ps.Length == 1 && ps[0].ParameterType == typeof(string))
                    {
                        var created = m.Invoke(null, new object?[] { text });
                        if (created is not null)
                        {
                            catalog = created;
                            return true;
                        }
                    }
                }
            }

            catalog = default!;
            return false;
        }

        private bool TryCreateEmptyCatalog(out object catalog)
        {
            if (_catalogType.IsInterface)
            {
                catalog = default!;
                return false;
            }

            var ctor = _catalogType.GetConstructor(Type.EmptyTypes);
            if (ctor is null)
            {
                catalog = default!;
                return false;
            }

            catalog = ctor.Invoke(Array.Empty<object?>());
            return true;
        }

        private bool TryPopulateCatalogInMemory(object catalog)
        {
            var addMethod = catalog.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m =>
                {
                    if (!string.Equals(m.Name, "Add", StringComparison.OrdinalIgnoreCase) &&
                        !m.Name.Contains("Add", StringComparison.OrdinalIgnoreCase))
                        return false;

                    var ps = m.GetParameters();
                    return ps.Length == 1;
                });

                if (addMethod is not null)
                {
                    var entryType = addMethod.GetParameters()[0].ParameterType;
                    var first = CreateCatalogEntry(entryType, "core.event_catalog.loaded");
                    var second = CreateCatalogEntry(entryType, "core.content.manifest.loaded");

                addMethod.Invoke(catalog, new[] { first });
                addMethod.Invoke(catalog, new[] { second });
                return true;
            }

            var writableListProperty = catalog.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p =>
                    p.CanWrite &&
                    p.GetIndexParameters().Length == 0 &&
                    (p.Name.Equals("Events", StringComparison.OrdinalIgnoreCase) ||
                     p.Name.Equals("Entries", StringComparison.OrdinalIgnoreCase) ||
                     p.Name.Equals("Definitions", StringComparison.OrdinalIgnoreCase)));

            if (writableListProperty is null)
                return false;

            var propType = writableListProperty.PropertyType;
            var elementType = TryGetEnumerableElementType(propType) ?? typeof(object);

            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
            list.Add(CreateCatalogEntry(elementType, "core.event_catalog.loaded"));
            list.Add(CreateCatalogEntry(elementType, "core.content.manifest.loaded"));

            if (propType.IsAssignableFrom(list.GetType()))
            {
                writableListProperty.SetValue(catalog, list);
                return true;
            }

            if (propType.IsArray)
            {
                var array = Array.CreateInstance(elementType, list.Count);
                for (var i = 0; i < list.Count; i++)
                    array.SetValue(list[i], i);

                writableListProperty.SetValue(catalog, array);
                return true;
            }

            return false;
        }

        private static Type? TryGetEnumerableElementType(Type type)
        {
            if (type.IsArray)
                return type.GetElementType();

            var enumerableInterface = type
                .GetInterfaces()
                .Concat(new[] { type })
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            return enumerableInterface?.GetGenericArguments()[0];
        }

        private static object CreateCatalogEntry(Type entryType, string eventType)
        {
            if (entryType == typeof(string))
                return eventType;

            if (!entryType.IsAbstract && !entryType.IsInterface)
            {
                var ctor = entryType
                    .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                    .OrderByDescending(c => c.GetParameters().Length)
                    .FirstOrDefault(c =>
                    {
                        var ps = c.GetParameters();
                        return ps.Length >= 1 && ps[0].ParameterType == typeof(string);
                    });

                if (ctor is not null)
                {
                    var ps = ctor.GetParameters();
                    var args = new object?[ps.Length];
                    args[0] = eventType;

                    for (var i = 1; i < ps.Length; i++)
                    {
                        var t = ps[i].ParameterType;
                        if (t == typeof(int)) args[i] = 1;
                        else if (t == typeof(long)) args[i] = 1L;
                        else if (t == typeof(double)) args[i] = 1.0;
                        else if (t == typeof(float)) args[i] = 1.0f;
                        else if (t == typeof(bool)) args[i] = true;
                        else if (t == typeof(string)) args[i] = string.Empty;
                        else if (t == typeof(TimeSpan)) args[i] = TimeSpan.Zero;
                        else if (t == typeof(DateTimeOffset)) args[i] = DateTimeOffset.UnixEpoch;
                        else if (t == typeof(DateTime)) args[i] = DateTime.UnixEpoch;
                        else args[i] = t.IsValueType ? Activator.CreateInstance(t) : null;
                    }

                    var created = ctor.Invoke(args);
                    TrySetCommonEntryProperties(created, eventType);
                    return created;
                }

                var emptyCtor = entryType.GetConstructor(Type.EmptyTypes);
                if (emptyCtor is not null)
                {
                    var created = emptyCtor.Invoke(Array.Empty<object?>());
                    TrySetCommonEntryProperties(created, eventType);
                    return created;
                }
            }

            throw new InvalidOperationException($"Unable to create a catalog entry for type '{entryType.FullName}'.");
        }

        private static void TrySetCommonEntryProperties(object instance, string eventType)
        {
            var props = instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            var typeProp = props.FirstOrDefault(p => p.CanWrite &&
                                                    p.GetIndexParameters().Length == 0 &&
                                                    p.PropertyType == typeof(string) &&
                                                    (p.Name.Equals("Type", StringComparison.OrdinalIgnoreCase) ||
                                                     p.Name.Equals("EventType", StringComparison.OrdinalIgnoreCase) ||
                                                     p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                                                     p.Name.Equals("Key", StringComparison.OrdinalIgnoreCase)));
            typeProp?.SetValue(instance, eventType);

            var weightProp = props.FirstOrDefault(p => p.CanWrite &&
                                                      p.GetIndexParameters().Length == 0 &&
                                                      (p.Name.Equals("Weight", StringComparison.OrdinalIgnoreCase) ||
                                                       p.Name.Equals("Chance", StringComparison.OrdinalIgnoreCase) ||
                                                       p.Name.Equals("Probability", StringComparison.OrdinalIgnoreCase)) &&
                                                      (p.PropertyType == typeof(int) || p.PropertyType == typeof(double) || p.PropertyType == typeof(float)));

            if (weightProp is not null)
            {
                if (weightProp.PropertyType == typeof(int)) weightProp.SetValue(instance, 1);
                else if (weightProp.PropertyType == typeof(double)) weightProp.SetValue(instance, 1.0);
                else if (weightProp.PropertyType == typeof(float)) weightProp.SetValue(instance, 1.0f);
            }
        }

        private static string Fingerprint(object? evt)
        {
            if (evt is null)
                return "<null>";

            var type = evt.GetType();
            var parts = new List<string> { type.FullName ?? type.Name };

            var props = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetIndexParameters().Length == 0)
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .Take(16)
                .ToArray();

            foreach (var p in props)
            {
                object? value;
                try
                {
                    value = p.GetValue(evt);
                }
                catch
                {
                    continue;
                }

                parts.Add(p.Name + "=" + FormatScalar(value));
            }

            return string.Join("|", parts);
        }

        private static string FormatScalar(object? value)
        {
            if (value is null)
                return "<null>";

            return value switch
            {
                DateTimeOffset dto => dto.ToString("O"),
                DateTime dt => dt.ToUniversalTime().ToString("O"),
                TimeSpan ts => ts.ToString(),
                Guid g => g.ToString("D"),
                string s => s,
                bool b => b ? "true" : "false",
                int i => i.ToString(),
                long l => l.ToString(),
                double d => d.ToString("R"),
                float f => f.ToString("R"),
                Enum e => e.ToString(),
                _ => value.ToString() ?? value.GetType().FullName ?? value.GetType().Name,
            };
        }

        private static Type? FindSingleTypeBySimpleName(IReadOnlyList<Assembly> assemblies, string simpleName)
        {
            var matches = assemblies
                .SelectMany(a => SafeGetTypes(a))
                .Where(t => string.Equals(t.Name, simpleName, StringComparison.Ordinal))
                .ToArray();

            if (matches.Length == 1)
                return matches[0];

            if (matches.Length > 1)
            {
                var exact = matches.FirstOrDefault(t => string.Equals(t.FullName, simpleName, StringComparison.Ordinal));
                return exact ?? matches[0];
            }

            var suffixMatches = assemblies
                .SelectMany(a => SafeGetTypes(a))
                .Where(t => t.FullName is not null && t.FullName.EndsWith("." + simpleName, StringComparison.Ordinal))
                .ToArray();

            return suffixMatches.FirstOrDefault();
        }

        private static IReadOnlyList<Assembly> GetCandidateAssemblies()
        {
            var loaded = AppDomain.CurrentDomain.GetAssemblies().ToList();

            TryLoad("Game.Core", loaded);

            return loaded
                .Where(a =>
                {
                    var name = a.GetName().Name ?? string.Empty;
                    return name.Contains("Game", StringComparison.OrdinalIgnoreCase) &&
                           (name.Contains("Core", StringComparison.OrdinalIgnoreCase) || name.Contains("Domain", StringComparison.OrdinalIgnoreCase));
                })
                .Distinct()
                .ToArray();
        }

        private static void TryLoad(string assemblyName, List<Assembly> loaded)
        {
            if (loaded.Any(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase)))
                return;

            try
            {
                loaded.Add(Assembly.Load(assemblyName));
            }
            catch
            {
            }
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t is not null)!.Cast<Type>();
            }
        }

        private static bool LooksLikeTimeProvider(Type type)
        {
            if (!type.IsInterface)
                return false;

            var name = type.Name;
            if (name.Contains("Time", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Clock", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Now", StringComparison.OrdinalIgnoreCase))
                return true;

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            return methods.Any(m =>
                (m.ReturnType == typeof(DateTimeOffset) || m.ReturnType == typeof(DateTime)) &&
                (m.Name.Contains("Now", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("Utc", StringComparison.OrdinalIgnoreCase)));
        }

        private static object CreateFixedTimeProvider(Type interfaceType, DateTimeOffset fixedNow)
        {
            if (!interfaceType.IsInterface)
                throw new InvalidOperationException("Fixed time providers can only be created for interfaces.");

            var proxy = DispatchProxy.Create(interfaceType, typeof(FixedTimeProxy));
            ((FixedTimeProxy)(object)proxy).FixedNow = fixedNow;
            return proxy;
        }

        private sealed class FixedTimeProxy : DispatchProxy
        {
            public DateTimeOffset FixedNow { get; set; }

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                if (targetMethod is null)
                    return null;

                var returnType = targetMethod.ReturnType;

                if (returnType == typeof(DateTimeOffset))
                    return FixedNow;

                if (returnType == typeof(DateTime))
                    return FixedNow.UtcDateTime;

                if (returnType == typeof(long))
                    return FixedNow.ToUnixTimeMilliseconds();

                if (returnType == typeof(int))
                    return (int)(FixedNow.ToUnixTimeMilliseconds() & int.MaxValue);

                if (returnType.IsValueType)
                    return Activator.CreateInstance(returnType);

                return null;
            }
        }
    }
}
