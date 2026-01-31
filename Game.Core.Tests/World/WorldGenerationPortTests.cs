using System;
using FluentAssertions;
using Game.Core.Contracts.Engine;
using Game.Core.World;
using Xunit;

namespace Game.Core.Tests.World;

public sealed class WorldGenerationPortTests
{
    // ACC:T40.1
    [Fact]
    public void Should_Have_A_World_Generation_Port_Or_System_Type()
    {
        var system = new WorldGenerationSystem("seed-1");
        system.Should().NotBeNull();
    }

    // ACC:T40.2
    [Fact]
    public void Should_Expose_An_Explicit_Seed_Input_Surface()
    {
        var system = new WorldGenerationSystem("seed-2");
        system.Seed.Should().Be("seed-2");
    }

    // ACC:T40.3
    [Fact]
    public void Should_Use_CoreGameStarted_EventType()
    {
        GameStarted.EventType.Should().Be("core.game.started");
    }

    [Fact]
    public void Should_Reject_Empty_Seed()
    {
        var act = () => new WorldGenerationSystem(" ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Should_Reject_Negative_Guild_Count()
    {
        var system = new WorldGenerationSystem("seed-4");
        var act = () => system.GenerateNpcGuildIds(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ACC:T40.1
    [Fact]
    public void Should_Be_Deterministic_For_Same_Seed()
    {
        var first = new WorldGenerationSystem("seed-3").GenerateNpcGuildIds(count: 5);
        var second = new WorldGenerationSystem("seed-3").GenerateNpcGuildIds(count: 5);

        first.Should().Equal(second);
    }

    // ACC:T40.1
    [Fact]
    public void Should_Change_Output_When_Seed_Changes()
    {
        var first = new WorldGenerationSystem("seed-a").GenerateNpcGuildIds(count: 5);
        var second = new WorldGenerationSystem("seed-b").GenerateNpcGuildIds(count: 5);

        first.Should().NotEqual(second);
    }
}
