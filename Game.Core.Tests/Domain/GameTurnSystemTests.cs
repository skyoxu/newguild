using System;
using FluentAssertions;
using Game.Core.Domain.Turn;
using Game.Core.Engine;
using Xunit;

namespace Game.Core.Tests.Domain;

public class GameTurnSystemTests
{
    private sealed class DummyEventEngine : IEventEngine
    {
        public GameTurnState ExecuteResolutionPhase(GameTurnState state) => state;
        public GameTurnState ExecutePlayerPhase(GameTurnState state) => state;
        public GameTurnState ExecuteAiPhase(GameTurnState state) => state;
    }

    private sealed class DummyAICoordinator : IAICoordinator
    {
        public GameTurnState StepAiCycle(GameTurnState state) => state;
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
    public void Advance_moves_from_resolution_to_player_phase()
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
        var next = system.Advance(state);

        // Assert
        next.Week.Should().Be(1);
        next.Phase.Should().Be(GameTurnPhase.Player);
    }

    [Fact]
    public void Advance_moves_from_player_to_ai_phase()
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
        var next = system.Advance(state);

        // Assert
        next.Week.Should().Be(1);
        next.Phase.Should().Be(GameTurnPhase.AiSimulation);
    }

    [Fact]
    public void Advance_moves_from_ai_phase_to_next_week_resolution()
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
        var next = system.Advance(state);

        // Assert
        next.Week.Should().Be(2);
        next.Phase.Should().Be(GameTurnPhase.Resolution);
    }
}

