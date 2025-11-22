using System;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Contracts;
using Game.Core.Domain.Turn;
using Game.Core.Engine;
using Game.Core.Ports;
using Game.Core.Services;
using Xunit;

namespace Game.Core.Tests.Domain;

public class EventEngineTests
{
    private sealed class CapturingEventBus : IEventBus
    {
        public System.Collections.Generic.List<DomainEvent> Published { get; } = new();

        public Task PublishAsync(DomainEvent evt)
        {
            Published.Add(evt);
            return Task.CompletedTask;
        }

        public IDisposable Subscribe(Func<DomainEvent, Task> handler) => new DummySubscription();

        private sealed class DummySubscription : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class DummyEventCatalog : IEventCatalog
    {
        // Placeholder for future event definitions. For now, this is only
        // used to satisfy EventEngine construction in tests.
    }

    private static (EventEngine Engine, CapturingEventBus Bus) CreateEngine()
    {
        var bus = new CapturingEventBus();
        var catalog = new DummyEventCatalog();
        var engine = new EventEngine(catalog, bus);
        return (engine, bus);
    }

    [Fact]
    public void ExecuteResolutionPhase_does_not_change_week_or_phase_by_default()
    {
        // Arrange
        var (engine, _) = CreateEngine();
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.Resolution,
            SaveId: "save-1",
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act
        var next = engine.ExecuteResolutionPhase(state);

        // Assert
        next.Week.Should().Be(state.Week);
        next.Phase.Should().Be(GameTurnPhase.Resolution);
    }

    [Fact]
    public void ExecutePlayerPhase_does_not_change_week_or_phase_by_default()
    {
        // Arrange
        var (engine, _) = CreateEngine();
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.Player,
            SaveId: "save-1",
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act
        var next = engine.ExecutePlayerPhase(state);

        // Assert
        next.Week.Should().Be(state.Week);
        next.Phase.Should().Be(GameTurnPhase.Player);
    }

    [Fact]
    public void ExecuteAiPhase_does_not_change_week_or_phase_by_default()
    {
        // Arrange
        var (engine, _) = CreateEngine();
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.AiSimulation,
            SaveId: "save-1",
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act
        var next = engine.ExecuteAiPhase(state);

        // Assert
        next.Week.Should().Be(state.Week);
        next.Phase.Should().Be(GameTurnPhase.AiSimulation);
    }
}
