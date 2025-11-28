using System;
using FluentAssertions;
using Game.Core.Domain.Turn;
using Xunit;

namespace Game.Core.Tests.Domain;

public class GameLoopTests
{
    [Fact]
    public void GameTurnState_can_be_constructed_for_week_and_phase()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        // Act
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.Resolution,
            SaveId: "test-save",
            CurrentTime: now
        );

        // Assert
        state.Week.Should().Be(1);
        state.Phase.Should().Be(GameTurnPhase.Resolution);
        state.SaveId.Should().Be("test-save");
        state.CurrentTime.Should().Be(now);
    }
}

