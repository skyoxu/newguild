#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FluentAssertions;
using Game.Core.Contracts.AI;
using Game.Core.Domain.Turn;
using Game.Core.Engine;
using Game.Core.Ports.AI;
using Game.Core.Services;
using Xunit;

namespace Game.Core.Tests.Domain;

public sealed class AICoordinatorPerformanceTests
{
    // ACC:T16.3
    [Fact]
    public void GenerateAiEvents_WhenHandling200PlusEntities_ShouldHaveBoundedOutput()
    {
        var saveId = new SaveIdValue("save-1");
        var world = new InMemoryAiWorldStatePort();
        world.Seed(saveId, week: 1, CreateLargeWorld(saveId, memberCount: 2_000, guildCount: 200));

        var coordinator = new AICoordinator(world, new GuidIdGenerator());
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.AiSimulation,
            SaveId: saveId,
            CurrentTime: DateTimeOffset.UtcNow
        );

        var sw = Stopwatch.StartNew();
        var events = coordinator.GenerateAiEvents(state);
        sw.Stop();

        var intentEvents = events
            .Where(e => e.Type == AiIntentIssued.EventType)
            .Select(e => (AiIntentIssued)e.Data!)
            .ToList();

        intentEvents.Count.Should().BeLessThanOrEqualTo(200, "the minimal conflict rule allows at most one accepted join per guild per cycle");
        intentEvents.Select(i => i.TargetId).Should().OnlyHaveUniqueItems();

        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30));
    }

    private static AiWorldSnapshot CreateLargeWorld(SaveIdValue saveId, int memberCount, int guildCount)
    {
        var guilds = new Dictionary<string, AiWorldGuild>(StringComparer.Ordinal);
        for (var g = 0; g < guildCount; g++)
        {
            var id = $"npc-guild-{g:D3}";
            guilds[id] = new AiWorldGuild(id, CurrentMembers: 0, MaxMembers: 10_000);
        }

        var members = new Dictionary<string, AiWorldMember>(StringComparer.Ordinal);
        var affinity = new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.Ordinal);

        for (var m = 0; m < memberCount; m++)
        {
            var memberId = $"npc-{m:D6}";
            members[memberId] = new AiWorldMember(memberId, CurrentGuildId: null);

            var preferred = $"npc-guild-{(m % guildCount):D3}";
            affinity[memberId] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [preferred] = 100,
            };
        }

        return new AiWorldSnapshot(guilds, members, affinity);
    }
}

