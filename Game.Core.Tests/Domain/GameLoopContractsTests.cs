using System;
using FluentAssertions;
using Game.Core.Contracts.GameLoop;
using Xunit;

namespace Game.Core.Tests.Domain;

public class GameLoopContractsTests
{
    [Fact]
    public void GameTurnStarted_EventType_should_match_expected()
    {
        GameTurnStarted.EventType.Should().Be("core.game_turn.started");
    }

    [Fact]
    public void GameTurnStarted_should_accept_valid_fields()
    {
        var now = DateTimeOffset.UtcNow;

        var evt = new GameTurnStarted(
            SaveId: "save-1",
            Week: 1,
            Phase: "Resolution",
            StartedAt: now
        );

        evt.SaveId.Should().Be("save-1");
        evt.Week.Should().Be(1);
        evt.Phase.Should().Be("Resolution");
        evt.StartedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GameTurnPhaseChanged_EventType_should_match_expected()
    {
        GameTurnPhaseChanged.EventType.Should().Be("core.game_turn.phase_changed");
    }

    [Fact]
    public void GameTurnPhaseChanged_should_accept_valid_fields()
    {
        var now = DateTimeOffset.UtcNow;

        var evt = new GameTurnPhaseChanged(
            SaveId: "save-1",
            Week: 1,
            PreviousPhase: "Resolution",
            CurrentPhase: "Player",
            ChangedAt: now
        );

        evt.SaveId.Should().Be("save-1");
        evt.Week.Should().Be(1);
        evt.PreviousPhase.Should().Be("Resolution");
        evt.CurrentPhase.Should().Be("Player");
        evt.ChangedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GameWeekAdvanced_EventType_should_match_expected()
    {
        GameWeekAdvanced.EventType.Should().Be("core.game_turn.week_advanced");
    }

    [Fact]
    public void GameWeekAdvanced_should_accept_valid_fields()
    {
        var now = DateTimeOffset.UtcNow;

        var evt = new GameWeekAdvanced(
            SaveId: "save-1",
            PreviousWeek: 1,
            CurrentWeek: 2,
            AdvancedAt: now
        );

        evt.SaveId.Should().Be("save-1");
        evt.PreviousWeek.Should().Be(1);
        evt.CurrentWeek.Should().Be(2);
        evt.AdvancedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
    }
}

