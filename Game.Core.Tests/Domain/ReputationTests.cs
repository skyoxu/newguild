#nullable enable

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Contracts.Media;
using Game.Core.Services;
using Game.Core.Tests.TestDoubles;
using Xunit;

namespace Game.Core.Tests.Domain;

public sealed class ReputationTests
{
    // ACC:T19.1
    [Fact]
    public async Task ReputationSystem_Should_Apply_Deltas_And_Record_Source_Totals()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var time = new FixedTime(now);
        var ids = new SequenceIdGenerator("evt-1");
        var bus = new InMemoryEventBus();
        var system = new ReputationSystem(bus, time, ids);

        (await system.ApplyDeltaAsync("guild-1", delta: 10, reason: "battle_victory", sourceId: "raid:1"))
            .Should().Be(10);

        system.GetReputation("guild-1").Should().Be(10);
        var totals = system.GetSourceTotals("guild-1");
        totals.Should().ContainKey("raid:1");
        totals["raid:1"].Should().Be(10);
    }

    // ACC:T19.5
    [Fact]
    public async Task ReputationSystem_Should_Clamp_Within_Bounds_And_Reject_Invalid_GuildId()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var time = new FixedTime(now);
        var ids = new SequenceIdGenerator("evt-1", "evt-2", "evt-3");
        var bus = new InMemoryEventBus();
        var system = new ReputationSystem(bus, time, ids);

        Func<Task> invalidGuild = () => system.ApplyDeltaAsync("", delta: 1, reason: "invalid", sourceId: "x");
        await invalidGuild.Should().ThrowAsync<ArgumentException>();

        // If min/max are implemented, repeated large deltas must clamp deterministically.
        // (Default expectation: reputation is non-negative.)
        var valueAfterNegative = await system.ApplyDeltaAsync("guild-1", delta: -10_000, reason: "negative_event", sourceId: "scandal:1");
        valueAfterNegative.Should().Be(ReputationSystem.MinReputation);

        var valueAfterPositive = await system.ApplyDeltaAsync("guild-1", delta: 10_000, reason: "battle_victory", sourceId: "raid:2");
        valueAfterPositive.Should().Be(ReputationSystem.MaxReputation);

        // Event contract constants should remain stable (ADR-0004 aligned).
        ReputationChanged.EventType.Should().Be("core.reputation.changed");
    }
}
