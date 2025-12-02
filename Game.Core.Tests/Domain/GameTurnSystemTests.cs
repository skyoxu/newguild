using System;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Domain.Turn;
using Game.Core.Engine;
using Xunit;

namespace Game.Core.Tests.Domain;

public class GameTurnSystemTests
{
    private sealed class DummyEventEngine : IEventEngine
    {
        public Task<GameTurnState> ExecuteResolutionPhaseAsync(GameTurnState state) => Task.FromResult(state);
        public Task<GameTurnState> ExecutePlayerPhaseAsync(GameTurnState state) => Task.FromResult(state);
        public Task<GameTurnState> ExecuteAiPhaseAsync(GameTurnState state) => Task.FromResult(state);
    }

    private sealed class DummyAICoordinator : IAICoordinator
    {
        public GameTurnState StepAiCycle(GameTurnState state) => state;
    }

    private sealed class FaultingEventEngine : IEventEngine
    {
        private readonly Exception _exceptionToThrow;

        public FaultingEventEngine(Exception exceptionToThrow)
        {
            _exceptionToThrow = exceptionToThrow;
        }

        public Task<GameTurnState> ExecuteResolutionPhaseAsync(GameTurnState state)
        {
            throw _exceptionToThrow;
        }

        public Task<GameTurnState> ExecutePlayerPhaseAsync(GameTurnState state)
        {
            throw _exceptionToThrow;
        }

        public Task<GameTurnState> ExecuteAiPhaseAsync(GameTurnState state)
        {
            throw _exceptionToThrow;
        }
    }

    private static GameTurnSystem CreateSystem()
    {
        var engine = new DummyEventEngine();
        var ai = new DummyAICoordinator();
        return new GameTurnSystem(engine, ai);
    }

    [Fact]
    public void StartNewWeek_initializes_week_and_phase()
    {
        // Arrange
        var system = CreateSystem();
        var saveId = "save-1";

        // Act
        var state = system.StartNewWeek(saveId);

        // Assert
        state.Week.Should().Be(1);
        state.Phase.Should().Be(GameTurnPhase.Resolution);
        state.SaveId.Should().Be(saveId);
        state.CurrentTime.Should().BeOnOrAfter(DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Advance_moves_from_resolution_to_player_phase()
    {
        // Arrange
        var system = CreateSystem();
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.Resolution,
            SaveId: "save-1",
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act
        var next = await system.Advance(state);

        // Assert
        next.Week.Should().Be(1);
        next.Phase.Should().Be(GameTurnPhase.Player);
    }

    [Fact]
    public async Task Advance_moves_from_player_to_ai_phase()
    {
        // Arrange
        var system = CreateSystem();
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.Player,
            SaveId: "save-1",
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act
        var next = await system.Advance(state);

        // Assert
        next.Week.Should().Be(1);
        next.Phase.Should().Be(GameTurnPhase.AiSimulation);
    }

    [Fact]
    public async Task Advance_moves_from_ai_phase_to_next_week_resolution()
    {
        // Arrange
        var system = CreateSystem();
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.AiSimulation,
            SaveId: "save-1",
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act
        var next = await system.Advance(state);

        // Assert
        next.Week.Should().Be(2);
        next.Phase.Should().Be(GameTurnPhase.Resolution);
    }

    [Fact]
    public async Task Full_week_cycle_from_start_new_week_advances_to_week_two_resolution()
    {
        // Arrange
        var system = CreateSystem();
        var saveId = "save-t2";

        // Act
        var startState = system.StartNewWeek(saveId);
        var afterResolution = await system.Advance(startState);
        var afterPlayer = await system.Advance(afterResolution);
        var afterAi = await system.Advance(afterPlayer);

        // Assert
        startState.Week.Should().Be(1);
        startState.Phase.Should().Be(GameTurnPhase.Resolution);

        afterResolution.Week.Should().Be(1);
        afterResolution.Phase.Should().Be(GameTurnPhase.Player);

        afterPlayer.Week.Should().Be(1);
        afterPlayer.Phase.Should().Be(GameTurnPhase.AiSimulation);

        afterAi.Week.Should().Be(2);
        afterAi.Phase.Should().Be(GameTurnPhase.Resolution);
        afterAi.SaveId.Should().Be(saveId);
    }

    [Fact]
    public async Task Advance_PropagatesException_FromResolutionPhase()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Resolution phase failed");
        var faultingEngine = new FaultingEventEngine(expectedException);
        var ai = new DummyAICoordinator();
        var system = new GameTurnSystem(faultingEngine, ai);
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.Resolution,
            SaveId: "save-1",
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await system.Advance(state)
        );
        exception.Message.Should().Be("Resolution phase failed");
    }

    [Fact]
    public async Task Advance_PropagatesException_FromPlayerPhase()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Player phase failed");
        var faultingEngine = new FaultingEventEngine(expectedException);
        var ai = new DummyAICoordinator();
        var system = new GameTurnSystem(faultingEngine, ai);
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.Player,
            SaveId: "save-1",
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await system.Advance(state)
        );
        exception.Message.Should().Be("Player phase failed");
    }

    [Fact]
    public async Task Advance_PropagatesException_FromAiPhase()
    {
        // Arrange
        var expectedException = new InvalidOperationException("AI phase failed");
        var faultingEngine = new FaultingEventEngine(expectedException);
        var ai = new DummyAICoordinator();
        var system = new GameTurnSystem(faultingEngine, ai);
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.AiSimulation,
            SaveId: "save-1",
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await system.Advance(state)
        );
        exception.Message.Should().Be("AI phase failed");
    }
}
