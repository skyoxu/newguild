using System;
using FluentAssertions;
using Game.Core.Contracts.Persistence;
using Game.Core.Contracts.State;
using Xunit;

namespace Game.Core.Tests.Contracts;

public sealed class GameStateManagerEventsContractsTests
{
    [Fact]
    public void Should_Have_GameStateUpdated_EventType()
    {
        GameStateUpdated.EventType.Should().Be("core.game_state.updated");
    }

    [Fact]
    public void Should_Have_SaveDeleted_EventType()
    {
        SaveDeleted.EventType.Should().Be("core.save.deleted");
    }

    [Fact]
    public void Should_Have_AutoSaveEnabled_EventType()
    {
        AutoSaveEnabled.EventType.Should().Be("core.autosave.enabled");
    }

    [Fact]
    public void Should_Have_AutoSaveDisabled_EventType()
    {
        AutoSaveDisabled.EventType.Should().Be("core.autosave.disabled");
    }

    [Fact]
    public void Should_Have_AutoSaveCompleted_EventType()
    {
        AutoSaveCompleted.EventType.Should().Be("core.autosave.completed");
    }

    [Fact]
    public void Should_Accept_Valid_Fields_For_Events()
    {
        var now = DateTimeOffset.UtcNow;

        _ = new GameStateUpdated(StateId: "state-1", HasConfig: true, UpdatedAt: now);
        _ = new SaveDeleted(SaveId: "save-1", DeletedAt: now);
        _ = new AutoSaveEnabled(IntervalMs: 1000, EnabledAt: now);
        _ = new AutoSaveDisabled(DisabledAt: now);
        _ = new AutoSaveCompleted(SaveId: "save-1", IntervalMs: 1000, CompletedAt: now);
    }
}

