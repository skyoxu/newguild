using System;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Contracts;
using Game.Core.Contracts.Media;
using Game.Core.Services;
using Game.Core.Tests.TestDoubles;
using Xunit;

namespace Game.Core.Tests.Domain;

public sealed class ReputationDomainEventsTests
{
    // ACC:T19.2
    [Fact]
    public async Task ApplyDelta_Should_Publish_ReputationChanged_DomainEvent()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var time = new FixedTime(now);
        var ids = new SequenceIdGenerator("evt-1");
        var bus = new InMemoryEventBus();

        DomainEvent? observed = null;
        using var _ = bus.Subscribe(evt =>
        {
            observed = evt;
            return Task.CompletedTask;
        });

        var system = new ReputationSystem(bus, time, ids);

        var updated = await system.ApplyDeltaAsync("guild-1", delta: 3, reason: "raid_success", sourceId: "raid:1");
        updated.Should().BeGreaterOrEqualTo(0);

        observed.Should().NotBeNull();
        observed!.Type.Should().Be(ReputationChanged.EventType);
        observed.Source.Should().NotBeNullOrWhiteSpace();
        observed.Id.Should().Be("evt-1");

        observed.Data.Should().BeOfType<ReputationChanged>();
        var data = (ReputationChanged)observed.Data!;
        data.GuildId.Should().Be("guild-1");
        data.NewValue.Should().Be(updated);
        data.Reason.Should().NotBeNullOrWhiteSpace();
        data.ChangedAt.Should().Be(now);
    }

    [Fact]
    public void Contracts_Should_Define_Stable_EventType_Constants()
    {
        ReputationChanged.EventType.Should().Be("core.reputation.changed");
        MediaBeatTriggered.EventType.Should().Be("core.media.beat.triggered");
    }
}
