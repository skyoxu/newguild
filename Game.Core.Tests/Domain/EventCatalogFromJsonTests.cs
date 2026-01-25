using System;
using System.Linq;
using FluentAssertions;
using Game.Core.Engine;
using Game.Core.Services;
using Xunit;

namespace Game.Core.Tests.Domain;

public sealed class EventCatalogFromJsonTests
{
    // ACC:T29.1
    [Fact]
    public void FromJson_Should_Parse_Event_Types_And_Default_To_Enabled()
    {
        var json = "{\"catalogId\":\"test\",\"schemaVersion\":\"1\",\"events\":[{\"type\":\"core.event_catalog.loaded\"},{\"type\":\"core.content.manifest.loaded\"}]}";

        var catalog = EventCatalog.FromJson(json);

        catalog.IsEventEnabled("core.event_catalog.loaded").Should().BeTrue();
        catalog.IsEventEnabled("core.content.manifest.loaded").Should().BeTrue();
        catalog.GetEnabledEventTypes().Should().Equal(new[] { "core.content.manifest.loaded", "core.event_catalog.loaded" });
    }

    // ACC:T29.1
    [Fact]
    public void FromJson_Should_Skip_Invalid_Entries_And_Support_Numeric_Version()
    {
        var json =
            "{\"version\":1," +
            "\"events\":[\"not-an-object\",{\"type\":\"\"},{\"type\":\"core.event_catalog.loaded\",\"enabled\":true}]}";

        var catalog = EventCatalog.FromJson(json);

        catalog.GetEnabledEventTypes().Should().Equal(new[] { "core.event_catalog.loaded" });
    }

    // ACC:T29.1
    [Fact]
    public void FromJson_Should_Respect_Disabled_Entries()
    {
        var json =
            "{\"events\":[" +
            "{\"type\":\"core.event_catalog.loaded\",\"enabled\":false}," +
            "{\"type\":\"core.content.manifest.loaded\",\"enabledByDefault\":true}" +
            "]}";

        var catalog = EventCatalog.FromJson(json);

        catalog.IsEventEnabled("core.event_catalog.loaded").Should().BeFalse();
        catalog.IsEventEnabled("core.content.manifest.loaded").Should().BeTrue();
        catalog.GetEnabledEventTypes().Should().Equal(new[] { "core.content.manifest.loaded" });
    }

    // ACC:T29.1
    [Fact]
    public void FromJson_Should_Skip_NonCore_EventTypes()
    {
        var nonCore = string.Join('.', new[] { "ui", "menu", "start" });
        var json =
            "{\"events\":[" +
            $"{{\"type\":\"{nonCore}\",\"enabled\":true}}," +
            "{\"type\":\"core.content.manifest.loaded\",\"enabled\":true}" +
            "]}";

        var catalog = EventCatalog.FromJson(json);

        catalog.IsEventEnabled(nonCore).Should().BeFalse();
        catalog.GetEnabledEventTypes().Should().Equal(new[] { "core.content.manifest.loaded" });
    }

    // ACC:T29.1
    [Fact]
    public void GenerateEvents_Should_Yield_Empty_When_No_Enabled_Types_Or_Count_Is_Zero()
    {
        var now = new DateTimeOffset(2026, 01, 01, 12, 00, 00, TimeSpan.Zero);
        var disabledCatalog = EventCatalog.FromJson("{\"events\":[{\"type\":\"core.event_catalog.loaded\",\"enabled\":false}]}");
        var enabledCatalog = EventCatalog.FromJson("{\"events\":[{\"type\":\"core.event_catalog.loaded\",\"enabled\":true}]}");

        EventEngine.GenerateEvents(disabledCatalog, seed: 1, now: now, count: 1).ToArray().Should().BeEmpty();
        EventEngine.GenerateEvents(enabledCatalog, seed: 1, now: now, count: 0).ToArray().Should().BeEmpty();
    }
}
