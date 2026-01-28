using FluentAssertions;
using Game.Core.Contracts.Content;
using Game.Core.Contracts.Events;
using Xunit;

namespace Game.Core.Tests.Contracts;

public class Phase2ContentAndEventsContractsTests
{
    // ACC:T42.7
    // ACC:T27.5
    // ACC:T29.7
    // ACC:T33.4
    [Fact]
    public void Should_Have_CoreContentManifestLoaded_EventType()
    {
        ContentManifestLoaded.EventType.Should().Be("core.content.manifest.loaded");
    }

    // ACC:T27.9
    // ACC:T29.5
    // ACC:T33.8
    [Fact]
    public void Should_Have_CoreEventCatalogLoaded_EventType()
    {
        EventCatalogLoaded.EventType.Should().Be("core.event_catalog.loaded");
    }
}

