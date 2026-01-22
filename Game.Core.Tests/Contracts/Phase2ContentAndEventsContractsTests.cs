using FluentAssertions;
using Game.Core.Contracts.Content;
using Game.Core.Contracts.Events;
using Xunit;

namespace Game.Core.Tests.Contracts;

public class Phase2ContentAndEventsContractsTests
{
    [Fact]
    public void ContentManifestLoaded_EventType_Should_Be_CoreContentManifestLoaded()
    {
        ContentManifestLoaded.EventType.Should().Be("core.content.manifest.loaded");
    }

    [Fact]
    public void EventCatalogLoaded_EventType_Should_Be_CoreEventCatalogLoaded()
    {
        EventCatalogLoaded.EventType.Should().Be("core.event_catalog.loaded");
    }
}

