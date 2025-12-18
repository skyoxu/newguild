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

/// <summary>
/// Smoke tests for CI integration validation.
/// Full functional tests are in GameTurnSystemTests.cs
/// </summary>
public class GameLoopTests
{
    [Fact]
    public void GameTurnState_Can_Be_Constructed_For_Week_And_Phase()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        // Act
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.Resolution,
            SaveId: new SaveIdValue("test-save"),
            CurrentTime: now
        );

        // Assert
        state.Week.Should().Be(1);
        state.Phase.Should().Be(GameTurnPhase.Resolution);
        state.SaveId.ToString().Should().Be("test-save");
        state.CurrentTime.Should().Be(now);
    }

    [Fact]
    public void GameTurnSystem_Can_Be_Constructed_With_Mock_Dependencies()
    {
        // Arrange & Act
        var system = CreateMinimalSystem();

        // Assert
        system.Should().NotBeNull();
    }

    [Fact]
    public void StartNewWeek_Returns_Valid_Initial_State()
    {
        // Arrange
        var system = CreateMinimalSystem();

        // Act
        var state = system.StartNewWeek(new SaveIdValue("smoke-test-save"));

        // Assert
        state.Should().NotBeNull();
        state.Week.Should().Be(1);
        state.Phase.Should().Be(GameTurnPhase.Resolution);
        state.SaveId.ToString().Should().Be("smoke-test-save");
    }

    [Fact]
    public async Task Single_Advance_From_Resolution_Completes_Successfully()
    {
        // Arrange
        var system = CreateMinimalSystem();
        var initialState = system.StartNewWeek(new SaveIdValue("smoke-test"));

        // Act
        var resultState = await system.Advance(initialState);

        // Assert
        resultState.Should().NotBeNull();
        resultState.Week.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task Full_Turn_Cycle_Advances_Through_All_Phases()
    {
        // Arrange
        var eventBus = new CapturingEventBus();
        var system = new GameTurnSystem(
            new MinimalEventEngine(),
            new MinimalAICoordinator(),
            eventBus,
            new FakeTime()
        );
        var state = system.StartNewWeek(new SaveIdValue("cycle-test"));

        // Act - Execute full cycle: Resolution -> Player -> AI Simulation -> Next Week
        state = await system.Advance(state); // Resolution -> Player
        state = await system.Advance(state); // Player -> AI Simulation
        state = await system.Advance(state); // AI Simulation -> Week 2 Resolution

        // Assert
        state.Week.Should().Be(2);
        state.Phase.Should().Be(GameTurnPhase.Resolution);
        eventBus.PublishedEvents.Should().NotBeEmpty("events should be published during cycle");
    }

    // Minimal test doubles for smoke testing
    private static GameTurnSystem CreateMinimalSystem()
    {
        return new GameTurnSystem(
            new MinimalEventEngine(),
            new MinimalAICoordinator(),
            new MinimalEventBus(),
            new FakeTime()
        );
    }

    private sealed class MinimalEventEngine : IEventEngine
    {
        public Task<GameTurnState> ExecuteResolutionPhaseAsync(GameTurnState state) => Task.FromResult(state);
        public Task<GameTurnState> ExecutePlayerPhaseAsync(GameTurnState state) => Task.FromResult(state);
        public Task<GameTurnState> ExecuteAiPhaseAsync(GameTurnState state) => Task.FromResult(state);
    }

    private sealed class MinimalAICoordinator : IAICoordinator
    {
        public GameTurnState StepAiCycle(GameTurnState state) => state;
    }

    private sealed class MinimalEventBus : IEventBus
    {
        public Task PublishAsync(DomainEvent evt) => Task.CompletedTask;
        public IDisposable Subscribe(Func<DomainEvent, Task> handler) => new DummySubscription();

        private sealed class DummySubscription : IDisposable
        {
            public void Dispose() { }
        }
    }

    private sealed class CapturingEventBus : IEventBus
    {
        public System.Collections.Generic.List<DomainEvent> PublishedEvents { get; } = new();

        public Task PublishAsync(DomainEvent evt)
        {
            PublishedEvents.Add(evt);
            return Task.CompletedTask;
        }

        public IDisposable Subscribe(Func<DomainEvent, Task> handler) => new DummySubscription();

        private sealed class DummySubscription : IDisposable
        {
            public void Dispose() { }
        }
    }

    private sealed class FakeTime : ITime
    {
        public double DeltaSeconds => 0.016; // Mock 16ms (60 FPS)
    }
}
