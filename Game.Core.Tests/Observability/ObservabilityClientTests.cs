using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Observability;
using Game.Core.Ports;
using Game.Core.Services;
using Game.Core.State;
using Xunit;

namespace Game.Core.Tests.Observability;

public sealed class ObservabilityClientTests
{
    // ACC:T24.2
    [Fact]
    public void Should_Expose_LoggerPort_From_GameCore_Without_GodotOrSentryDependency()
    {
        typeof(ILogger).IsInterface.Should().BeTrue();
        typeof(ILogger).Namespace.Should().Be("Game.Core.Ports");

        var referenced = typeof(ILogger).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
        referenced.Should().NotContain("Godot");
        referenced.Should().NotContain("Sentry");
    }

    [Theory]
    [InlineData(false, null, null, false)]
    [InlineData(true, "1", null, false)]
    [InlineData(true, null, "true", false)]
    [InlineData(true, "0", "", true)]
    public void Should_Control_SensitiveDetails_By_Debug_SecureMode_And_CI(
        bool isDebugBuild,
        string? secureMode,
        string? ci,
        bool expected)
    {
        var env = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["GD_SECURE_MODE"] = secureMode,
            ["CI"] = ci
        };

        var actual = SensitiveDetailsPolicy.IncludeSensitiveDetails(
            isDebugBuild,
            key => env.TryGetValue(key, out var value) ? value : null);

        actual.Should().Be(expected);
    }

    [Fact]
    public void Should_Allow_Core_Services_To_Accept_ILogger_Via_Constructor_Injection()
    {
        typeof(InMemoryEventBus)
            .GetConstructors()
            .Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(ILogger)))
            .Should().BeTrue();

        typeof(GameStateManager)
            .GetConstructors()
            .Any(c =>
            {
                var parameters = c.GetParameters();
                return parameters.Any(p => p.ParameterType == typeof(IDataStore))
                    && parameters.Any(p => p.ParameterType == typeof(ILogger));
            })
            .Should().BeTrue();

        var store = new InMemoryDataStore();
        var logger = new NullLogger();

        var manager = new GameStateManager(store, options: null, logger: logger);
        manager.Should().NotBeNull();
    }

    [Fact]
    public void Should_Create_ObservabilityClient_With_Injected_Logger()
    {
        var logger = new NullLogger();
        var client = new ObservabilityClient(logger);
        client.Logger.Should().BeSameAs(logger);
    }

    [Fact]
    public void StructuredLogger_Should_Write_Json_With_Level_Message_And_Source()
    {
        var lines = new List<string>();
        var logger = new StructuredLogger(lines.Add, source: "unit-tests", now: () => DateTimeOffset.Parse("2026-01-11T00:00:00Z"));

        logger.Info("hello");
        lines.Count.Should().Be(1);

        using var doc = JsonDocument.Parse(lines[0]);
        doc.RootElement.GetProperty("level").GetString().Should().Be("info");
        doc.RootElement.GetProperty("message").GetString().Should().Be("hello");
        doc.RootElement.GetProperty("source").GetString().Should().Be("unit-tests");
        doc.RootElement.GetProperty("ts").GetString().Should().Be("2026-01-11T00:00:00.0000000+00:00");
        doc.RootElement.TryGetProperty("exception", out var exEl).Should().BeTrue();
        exEl.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void StructuredLogger_Should_Write_Exception_Object_For_Error_With_Exception()
    {
        var lines = new List<string>();
        var logger = new StructuredLogger(lines.Add);

        logger.Error("boom", new InvalidOperationException("bad"));
        lines.Count.Should().Be(1);

        using var doc = JsonDocument.Parse(lines[0]);
        doc.RootElement.GetProperty("level").GetString().Should().Be("error");
        doc.RootElement.GetProperty("message").GetString().Should().Be("boom");

        var exEl = doc.RootElement.GetProperty("exception");
        exEl.ValueKind.Should().Be(JsonValueKind.Object);
        exEl.GetProperty("type").GetString().Should().Be("InvalidOperationException");
        exEl.GetProperty("message").GetString().Should().Be("bad");
    }

    [Fact]
    public void StructuredLogger_Should_Scrub_GodotPaths_By_Default()
    {
        var lines = new List<string>();
        var logger = new StructuredLogger(lines.Add);

        logger.Error("failed to open user://secrets/game.db");
        lines.Count.Should().Be(1);

        using var doc = JsonDocument.Parse(lines[0]);
        var msg = doc.RootElement.GetProperty("message").GetString();
        msg.Should().NotBeNull();
        msg!.Should().NotContain("user://");
        msg.Should().NotContain(".db");
    }

    [Fact]
    public void PiiDataScrubber_Should_Scrub_Windows_Absolute_Paths()
    {
        var scrubbed = PiiDataScrubber.Scrub(@"failed at C:\Users\me\secret.txt");
        scrubbed.Should().NotContain(@"C:\Users\me\secret.txt");
        scrubbed.Should().Contain("[path]");
    }

    [Fact]
    public void PiiDataScrubber_Should_Return_Input_When_Null_Or_Empty()
    {
        PiiDataScrubber.Scrub(string.Empty).Should().BeEmpty();
        PiiDataScrubber.Scrub(null!).Should().BeNull();
    }

    private sealed class NullLogger : ILogger
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
        public void Error(string message, Exception ex) { }
    }

    private sealed class InMemoryDataStore : IDataStore
    {
        private readonly Dictionary<string, string> _data = new(StringComparer.Ordinal);

        public Task SaveAsync(string key, string json)
        {
            _data[key] = json;
            return Task.CompletedTask;
        }

        public Task<string?> LoadAsync(string key)
        {
            return Task.FromResult(_data.TryGetValue(key, out var value) ? value : null);
        }

        public Task DeleteAsync(string key)
        {
            _data.Remove(key);
            return Task.CompletedTask;
        }
    }
}
