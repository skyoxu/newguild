using System;
using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using FluentAssertions.Execution;
using Game.Core.Contracts.Events;
using Game.Core.Services;
using Xunit;

namespace Game.Core.Tests.Contracts;

public sealed class Task28EventDefinitionEventChainAcceptanceTests
{
    private static readonly Regex EventTypeRegex = new(
        @"^[a-z0-9_]+(\.[a-z0-9_]+){2,}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // ACC:T28.1
    [Fact]
    public void Should_Expose_EventCatalogLoaded_EventType_As_PublicConst_And_Follow_Naming()
    {
        var eventTypeField = typeof(EventCatalogLoaded).GetField(
            "EventType",
            BindingFlags.Public | BindingFlags.Static);

        using (new AssertionScope())
        {
            eventTypeField.Should().NotBeNull("EventCatalogLoaded must expose a public static EventType constant");
            eventTypeField!.FieldType.Should().Be(typeof(string));
            eventTypeField.IsLiteral.Should().BeTrue("EventType must be a const string for constantization");
            eventTypeField.IsInitOnly.Should().BeFalse();

            var value = (string?)eventTypeField.GetRawConstantValue();
            value.Should().NotBeNullOrWhiteSpace();
            value!.Should().Be(value!.ToLowerInvariant(), "event types must be lowercase");
            value.Should().StartWith("core.", "this core contract must use the 'core' domain prefix");
            EventTypeRegex.IsMatch(value).Should().BeTrue("event types must follow the ADR naming pattern");
            value.Should().Be("core.event_catalog.loaded", "contractRefs requires the canonical event type to be stable");
        }
    }

    // ACC:T28.2
    [Fact]
    public void Should_Reject_EventDefinition_With_Invalid_EventType_Format()
    {
        var invalidEventType = "core.guild joined";

        Action act = () => new EventDefinition(
            eventType: invalidEventType,
            title: "Any",
            description: null,
            enabledByDefault: true);

        act.Should().Throw<ArgumentException>("EventDefinition must enforce event type format at construction time")
            .Where(ex =>
                ex.Message.Contains("type", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("event", StringComparison.OrdinalIgnoreCase));
    }

    // ACC:T28.3
    [Fact]
    public void Should_Reject_EventChainDefinition_With_Duplicate_EventTypes_In_Order()
    {
        var steps = new[]
        {
            EventCatalogLoaded.EventType,
            EventCatalogLoaded.EventType,
        };

        Action act = () => new EventChainDefinition(chainId: "chain.guild.created", eventTypes: steps);

        act.Should().Throw<ArgumentException>("EventChainDefinition must reject duplicate event types to prevent ambiguous chains");
    }

    [Fact]
    public void Should_Have_Canonical_EventCatalog_Implementation_And_Default_IsEventEnabled_To_False_For_Unknown_Event()
    {
        var catalog = new EventCatalog();

        catalog.IsEventEnabled("core.sample.unregistered")
            .Should().BeFalse("unknown events must be disabled by default");
    }

    [Fact]
    public void Should_Create_EventDefinition_When_Valid()
    {
        var def = new EventDefinition(
            eventType: EventCatalogLoaded.EventType,
            title: "Event Catalog Loaded",
            description: null,
            enabledByDefault: true);

        def.EventType.Should().Be(EventCatalogLoaded.EventType);
        def.EnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void Should_Throw_When_EventDefinition_EventType_Is_Missing()
    {
        Action act = () => new EventDefinition(
            eventType: "   ",
            title: "Any",
            description: null,
            enabledByDefault: false);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Should_Throw_When_EventDefinition_Title_Is_Missing()
    {
        Action act = () => new EventDefinition(
            eventType: EventCatalogLoaded.EventType,
            title: "",
            description: null,
            enabledByDefault: false);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Should_Create_EventChainDefinition_When_Valid()
    {
        var chain = new EventChainDefinition(
            chainId: "chain.guild.created",
            eventTypes: new[] { EventCatalogLoaded.EventType });

        chain.EventTypes.Should().HaveCount(1);
    }

    [Fact]
    public void Should_Throw_When_EventChainDefinition_EventTypes_Is_Null()
    {
        Action act = () => new EventChainDefinition(chainId: "x", eventTypes: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Should_Throw_When_EventChainDefinition_EventTypes_Is_Empty()
    {
        Action act = () => new EventChainDefinition(chainId: "x", eventTypes: Array.Empty<string>());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Should_Throw_When_EventChainDefinition_Has_Invalid_EventType()
    {
        Action act = () => new EventChainDefinition(chainId: "x", eventTypes: new[] { "core.guild created" });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Should_Resolve_EventCatalog_Gating_From_Definition()
    {
        var enabled = new EventDefinition(EventCatalogLoaded.EventType, "Event Catalog Loaded", null, enabledByDefault: true);
        var disabled = new EventDefinition("core.sample.disabled", "Disabled Sample", null, enabledByDefault: false);
        var def = new EventCatalogDefinition(
            CatalogId: "cat1",
            SchemaVersion: "1",
            Events: new[] { enabled, disabled },
            Chains: Array.Empty<EventChainDefinition>());

        var catalog = new EventCatalog(def);

        using (new AssertionScope())
        {
            catalog.IsEventEnabled(EventCatalogLoaded.EventType).Should().BeTrue();
            catalog.IsEventEnabled("core.sample.disabled").Should().BeFalse();
            catalog.IsEventEnabled("  ").Should().BeFalse();
        }
    }
}
