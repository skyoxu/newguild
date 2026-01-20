using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Services;
using Game.Core.Tests.TestDoubles;
using Xunit;

namespace Game.Core.Tests.Domain;

public sealed class ReputationSourceAggregationTests
{
    // ACC:T19.5
    [Fact]
    public async Task Should_Aggregate_Source_Deltas_By_SourceId()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var time = new FixedTime(now);
        var ids = new SequenceIdGenerator("evt-1", "evt-2", "evt-3", "evt-4");
        var bus = new InMemoryEventBus();

        var system = new ReputationSystem(bus, time, ids);

        await system.ApplyDeltaAsync("guild-1", delta: +10, reason: "quest_completion", sourceId: "quest");
        await system.ApplyDeltaAsync("guild-1", delta: +5, reason: "quest_bonus", sourceId: "quest");
        await system.ApplyDeltaAsync("guild-1", delta: -50, reason: "scandal", sourceId: "scandal");

        var totals = system.GetSourceTotals("guild-1");

        totals.Should().ContainKey("quest");
        totals["quest"].Should().Be(15);
        totals.Should().ContainKey("scandal");
        totals["scandal"].Should().Be(-50);
    }
}
