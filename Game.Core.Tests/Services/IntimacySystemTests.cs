using System;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Contracts;
using Game.Core.Contracts.Social;
using Game.Core.Services;
using Game.Core.Tests.TestDoubles;
using Xunit;

namespace Game.Core.Tests.Services;

public sealed class IntimacySystemTests
{
    // ACC:T18.1
    [Fact]
    public async Task ApplyInteractionAsync_Should_Update_Value_And_Publish_RelationshipChanged_Event()
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

        var system = new IntimacySystem(bus, time, ids);

        var updated = await system.ApplyInteractionAsync(guildId: "g1", subjectId: "a", otherId: "b", delta: 5);
        updated.Should().Be(5);
        system.GetIntimacy("g1", "a", "b").Should().Be(5);

        observed.Should().NotBeNull();
        observed!.Type.Should().Be(SocialRelationshipChanged.EventType);
        observed.Source.Should().Be(nameof(IntimacySystem));
        observed.Id.Should().Be("evt-1");

        observed.Data.Should().BeOfType<SocialRelationshipChanged>();
        var data = (SocialRelationshipChanged)observed.Data!;
        data.GuildId.Should().Be("g1");
        data.SubjectId.Should().Be("a");
        data.OtherId.Should().Be("b");
        data.OldValue.Should().Be(0);
        data.NewValue.Should().Be(5);
        data.ChangedAt.Should().Be(now);
    }

    // ACC:T18.4
    [Fact]
    public async Task ApplyInteractionAsync_Should_Clamp_And_Reject_Invalid_Pairs()
    {
        var bus = new InMemoryEventBus();
        var system = new IntimacySystem(bus);

        Func<Task> samePeer = () => system.ApplyInteractionAsync("g1", "m1", "m1", delta: 1);
        await samePeer.Should().ThrowAsync<ArgumentException>();

        Func<Task> emptyGuild = () => system.ApplyInteractionAsync("", "m1", "m2", delta: 1);
        await emptyGuild.Should().ThrowAsync<ArgumentException>();

        (await system.ApplyInteractionAsync("g1", "m1", "m2", delta: 10_000)).Should().Be(IntimacySystem.MaxIntimacy);
        (await system.ApplyInteractionAsync("g1", "m1", "m2", delta: -10_000)).Should().Be(IntimacySystem.MinIntimacy);
    }
}

