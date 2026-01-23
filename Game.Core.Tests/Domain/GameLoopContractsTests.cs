using System;
using FluentAssertions;
using Game.Contracts.GameLoop;
using Game.Core.Domain.Turn;
using Xunit;

namespace Game.Core.Tests.Domain;

public class GameLoopContractsTests
{
    // ACC:T42.9
    [Fact]
    public void Should_Have_GameTurnStarted_EventType()
    {
        GameTurnStarted.EventType.Should().Be("core.game_turn.started");
    }

    [Fact]
    public void Should_Accept_Valid_Fields_For_GameTurnStarted()
    {
        var now = DateTimeOffset.UtcNow;

        var evt = new GameTurnStarted(
            SaveId: new SaveIdValue("save-1"),
            Week: 1,
            Phase: "Resolution",
            StartedAt: now
        );

        evt.SaveId.Value.Should().Be("save-1");
        evt.Week.Should().Be(1);
        evt.Phase.Should().Be("Resolution");
        evt.StartedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Should_Have_GameTurnPhaseChanged_EventType()
    {
        GameTurnPhaseChanged.EventType.Should().Be("core.game_turn.phase_changed");
    }

    [Fact]
    public void Should_Accept_Valid_Fields_For_GameTurnPhaseChanged()
    {
        var now = DateTimeOffset.UtcNow;

        var evt = new GameTurnPhaseChanged(
            SaveId: new SaveIdValue("save-1"),
            Week: 1,
            PreviousPhase: "Resolution",
            CurrentPhase: "Player",
            ChangedAt: now
        );

        evt.SaveId.Value.Should().Be("save-1");
        evt.Week.Should().Be(1);
        evt.PreviousPhase.Should().Be("Resolution");
        evt.CurrentPhase.Should().Be("Player");
        evt.ChangedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Should_Have_GameWeekAdvanced_EventType()
    {
        GameWeekAdvanced.EventType.Should().Be("core.game_turn.week_advanced");
    }

    [Fact]
    public void Should_Accept_Valid_Fields_For_GameWeekAdvanced()
    {
        var now = DateTimeOffset.UtcNow;

        var evt = new GameWeekAdvanced(
            SaveId: new SaveIdValue("save-1"),
            PreviousWeek: 1,
            CurrentWeek: 2,
            AdvancedAt: now
        );

        evt.SaveId.Value.Should().Be("save-1");
        evt.PreviousWeek.Should().Be(1);
        evt.CurrentWeek.Should().Be(2);
        evt.AdvancedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
    }
}
