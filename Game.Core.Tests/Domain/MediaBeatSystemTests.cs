#nullable enable

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Contracts;
using Game.Core.Contracts.Media;
using Game.Core.Services;
using Game.Core.Tests.TestDoubles;
using Xunit;

namespace Game.Core.Tests.Domain;

public sealed class MediaBeatSystemTests
{
    // ACC:T19.3
    [Fact]
    public async Task TriggerBeat_Should_Publish_MediaBeatTriggered_DomainEvent()
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

        var system = new MediaBeatSystem(bus, time, ids);
        await system.TriggerBeatAsync(
            beatId: "beat-1",
            guildId: "guild-1",
            sourceEventType: "core.raid.resolved",
            headline: "Guild wins decisive battle");

        observed.Should().NotBeNull();
        observed!.Type.Should().Be(MediaBeatTriggered.EventType);
        observed.Source.Should().Be(nameof(MediaBeatSystem));
        observed.Id.Should().Be("evt-1");

        observed.Data.Should().BeOfType<MediaBeatTriggered>();
        var contract = (MediaBeatTriggered)observed.Data!;
        contract.BeatId.Should().Be("beat-1");
        contract.GuildId.Should().Be("guild-1");
        contract.SourceEventType.Should().Be("core.raid.resolved");
        contract.Headline.Should().Be("Guild wins decisive battle");
        contract.TriggeredAt.Should().Be(now);
    }

    [Theory]
    [InlineData("", "guild-1", "core.raid.resolved", "headline", "beatId")]
    [InlineData("beat-1", "", "core.raid.resolved", "headline", "guildId")]
    [InlineData("beat-1", "guild-1", "", "headline", "sourceEventType")]
    [InlineData("beat-1", "guild-1", "core.raid.resolved", "", "headline")]
    public async Task TriggerBeat_Should_Reject_Invalid_Input(string beatId, string guildId, string sourceEventType, string headline, string expectedParam)
    {
        var bus = new InMemoryEventBus();
        var system = new MediaBeatSystem(bus);

        Func<Task> act = () => system.TriggerBeatAsync(beatId, guildId, sourceEventType, headline);
        var ex = await act.Should().ThrowAsync<ArgumentException>();
        ex.Which.ParamName.Should().Be(expectedParam);
    }
}

