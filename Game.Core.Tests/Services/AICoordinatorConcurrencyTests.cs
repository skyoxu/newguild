using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Contracts;
using Game.Core.Domain.Turn;
using Game.Core.Engine;
using Xunit;

namespace Game.Core.Tests.Services;

public class AICoordinatorConcurrencyTests
{
    [Fact]
    public void Should_Expose_StepAiCycle_With_Stable_Signature()
    {
        var method = typeof(AICoordinator).GetMethod(nameof(IAICoordinator.StepAiCycle));

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(GameTurnState));

        var parameters = method.GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(GameTurnState));

        typeof(IAICoordinator).IsAssignableFrom(typeof(AICoordinator)).Should().BeTrue();
    }

    [Fact]
    public void Should_Expose_GenerateAiEvents_With_Stable_Signature()
    {
        var method = typeof(AICoordinator).GetMethod(nameof(IAICoordinator.GenerateAiEvents));

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(System.Collections.Generic.IReadOnlyList<DomainEvent>));

        var parameters = method.GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(GameTurnState));
    }

    // ACC:T16.4
    [Fact]
    public async Task Should_Be_Safe_To_Invoke_StepAiCycle_Concurrently()
    {
        var coordinator = new AICoordinator();
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var input = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.AiSimulation,
            SaveId: new SaveIdValue("save-1"),
            CurrentTime: now
        );

        var workers = Math.Max(2, Environment.ProcessorCount);
        const int iterationsPerWorker = 50;
        var expectedCalls = workers * iterationsPerWorker;

        var results = new ConcurrentBag<GameTurnState>();

        var tasks = Enumerable.Range(0, workers)
            .Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < iterationsPerWorker; i++)
                {
                    var next = coordinator.StepAiCycle(input);
                    results.Add(next);
                }
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        results.Should().HaveCount(expectedCalls);
        results.Should().OnlyContain(s => s != null);
        results.Should().OnlyContain(s => s.SaveId == input.SaveId);
        results.Should().OnlyContain(s => s.Week == input.Week);
    }
}
