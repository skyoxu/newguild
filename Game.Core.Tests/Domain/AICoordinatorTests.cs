using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Contracts.AI;
using Game.Core.Domain.Turn;
using Game.Core.Engine;
using Game.Core.Ports.AI;
using Game.Core.Services;
using Xunit;

namespace Game.Core.Tests.Domain;

public sealed class AICoordinatorTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 01, 18, 0, 0, 0, TimeSpan.Zero);

    // ACC:T16.1
    [Fact]
    public void GenerateAiEvents_Should_Emit_Ordered_Ai_Cycle_Events_And_Issue_Intents_From_World_Snapshot()
    {
        var saveId = new SaveIdValue("save-1");
        var world = new InMemoryAiWorldStatePort();
        world.Seed(saveId, week: 1, CreateWorldSnapshotForConflict(saveId));

        var coordinator = new AICoordinator(world, new DeterministicIdGenerator());
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.AiSimulation,
            SaveId: saveId,
            CurrentTime: FixedNow
        );

        var events = coordinator.GenerateAiEvents(state);

        var types = events.Select(e => e.Type).ToList();
        types.Should().NotBeEmpty();
        types.First().Should().Be(AiCycleStarted.EventType);
        types.Last().Should().Be(AiCycleCompleted.EventType);
        types.Should().Contain(AiIntentIssued.EventType);

        var startedIndex = types.IndexOf(AiCycleStarted.EventType);
        var firstIntentIndex = types.IndexOf(AiIntentIssued.EventType);
        var completedIndex = types.LastIndexOf(AiCycleCompleted.EventType);
        startedIndex.Should().BeLessThan(firstIntentIndex);
        firstIntentIndex.Should().BeLessThan(completedIndex);

        var intentEvents = events
            .Where(e => e.Type == AiIntentIssued.EventType)
            .Select(e => (AiIntentIssued)e.Data!)
            .ToList();

        intentEvents.Should().NotBeEmpty();
        intentEvents.Should().OnlyContain(i => i.IntentType == "core.guild.member.join");
        intentEvents.Select(i => i.ActorId).Should().OnlyHaveUniqueItems();
        intentEvents.Select(i => i.TargetId).Should().OnlyHaveUniqueItems("the minimal rule accepts at most one join per guild per cycle");

        var after = world.GetSnapshot(saveId, week: 1);
        after.Members["npc-0001"].CurrentGuildId.Should().Be("npc-guild-01", "lexicographic tie-break should pick npc-0001 for npc-guild-01");
        after.Guilds["npc-guild-01"].CurrentMembers.Should().Be(1);
    }

    // ACC:T16.3
    [Fact]
    public void GenerateAiEvents_Should_Be_Empty_And_Not_Mutate_World_When_Not_In_Ai_Phase()
    {
        var saveId = new SaveIdValue("save-1");
        var world = new InMemoryAiWorldStatePort();
        world.Seed(saveId, week: 1, CreateWorldSnapshotForConflict(saveId));

        var coordinator = new AICoordinator(world, new DeterministicIdGenerator());
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.Player,
            SaveId: saveId,
            CurrentTime: FixedNow
        );

        var before = world.GetSnapshot(saveId, week: 1);
        var events = coordinator.GenerateAiEvents(state);
        var after = world.GetSnapshot(saveId, week: 1);

        events.Should().BeEmpty();
        after.Should().BeEquivalentTo(before);
    }

    // ACC:T16.4
    [Fact]
    public async Task GenerateAiEvents_Should_Be_Safe_To_Invoke_Concurrently_With_ReadOnly_World()
    {
        var saveId = new SaveIdValue("save-1");
        var snapshot = CreateWorldSnapshotForConflict(saveId);
        var world = new ReadOnlyAiWorldStatePort(snapshot);
        var coordinator = new AICoordinator(world, new DeterministicIdGenerator());

        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.AiSimulation,
            SaveId: saveId,
            CurrentTime: FixedNow
        );

        var workers = Math.Max(2, Environment.ProcessorCount);
        const int iterationsPerWorker = 50;

        var tasks = Enumerable.Range(0, workers)
            .Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < iterationsPerWorker; i++)
                {
                    var events = coordinator.GenerateAiEvents(state);
                    events.First().Type.Should().Be(AiCycleStarted.EventType);
                    events.Last().Type.Should().Be(AiCycleCompleted.EventType);
                }
            }))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    private static AiWorldSnapshot CreateWorldSnapshotForConflict(SaveIdValue saveId)
    {
        var guilds = new Dictionary<string, AiWorldGuild>(StringComparer.Ordinal)
        {
            ["npc-guild-01"] = new AiWorldGuild("npc-guild-01", CurrentMembers: 0, MaxMembers: 2),
            ["npc-guild-02"] = new AiWorldGuild("npc-guild-02", CurrentMembers: 0, MaxMembers: 2),
        };

        var members = new Dictionary<string, AiWorldMember>(StringComparer.Ordinal)
        {
            ["npc-0001"] = new AiWorldMember("npc-0001", CurrentGuildId: null),
            ["npc-0002"] = new AiWorldMember("npc-0002", CurrentGuildId: null),
        };

        var affinity = new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.Ordinal)
        {
            ["npc-0001"] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["npc-guild-01"] = 10,
                ["npc-guild-02"] = 1,
            },
            ["npc-0002"] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["npc-guild-01"] = 10,
                ["npc-guild-02"] = 9,
            },
        };

        return new AiWorldSnapshot(guilds, members, affinity);
    }

    private sealed class DeterministicIdGenerator : Game.Core.Ports.IIdGenerator
    {
        private long _n;

        public string NewId()
        {
            var next = Interlocked.Increment(ref _n);
            return next.ToString("D8", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private sealed class ReadOnlyAiWorldStatePort : IAiWorldStatePort
    {
        private readonly AiWorldSnapshot _snapshot;

        public ReadOnlyAiWorldStatePort(AiWorldSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public AiWorldSnapshot GetSnapshot(SaveIdValue saveId, int week) => _snapshot;

        public void Apply(SaveIdValue saveId, int week, AiWorldDelta delta)
        {
        }
    }
}

