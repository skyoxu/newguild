using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Contracts;
using Game.Core.Contracts.Engine;
using Game.Core.Domain;
using Game.Core.Domain.ValueObjects;
using Game.Core.Engine;
using Game.Core.Services;
using Xunit;

namespace Game.Core.Tests.Engine;

public class GameEngineCoreEventTests
{
    private sealed class CapturingEventBus : IEventBus, IDisposable
    {
        public List<DomainEvent> Published { get; } = new();

        public Task PublishAsync(DomainEvent evt)
        {
            Published.Add(evt);
            return Task.CompletedTask;
        }

        public IDisposable Subscribe(Func<DomainEvent, Task> handler) => new DummySubscription();

        public void Dispose()
        {
            Published.Clear();
        }

        private sealed class DummySubscription : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private static GameEngineCore CreateEngineAndBus(out CapturingEventBus bus)
    {
        var config = new GameConfig(
            MaxLevel: 10,
            InitialHealth: 100,
            ScoreMultiplier: 1.0,
            AutoSave: false,
            Difficulty: Difficulty.Medium
        );
        var inventory = new Inventory();
        bus = new CapturingEventBus();
        return new GameEngineCore(config, inventory, seed: "test-seed", bus: bus);
    }

    // ACC:T42.2
    [Fact]
    public void Should_Publish_GameStarted_Event_When_Start_IsCalled()
    {
        // Arrange
        var engine = CreateEngineAndBus(out var bus);

        using (bus)
        {
            // Act
            engine.Start();

            // Assert
            bus.Published.Should().ContainSingle();
            var evt = bus.Published[0];
            evt.Type.Should().Be(GameStarted.EventType);
            evt.Source.Should().Be(nameof(GameEngineCore));
            evt.Data.Should().NotBeNull();
        }
    }

    // ACC:T42.5
    [Fact]
    public void Should_Publish_ScoreChanged_Event_When_AddScore_IsCalled()
    {
        // Arrange
        var engine = CreateEngineAndBus(out var bus);

        using (bus)
        {
            engine.Start();
            bus.Published.Clear();

            // Act
            engine.AddScore(10);

            // Assert
            bus.Published.Should().ContainSingle();
            var evt = bus.Published[0];
            evt.Type.Should().Be(ScoreChanged.EventType);
            evt.Source.Should().Be(nameof(GameEngineCore));
            evt.Data.Should().NotBeNull();
        }
    }

    // ACC:T42.6
    [Fact]
    public void Should_Publish_PlayerHealthChanged_Event_When_ApplyDamage_IsCalled()
    {
        // Arrange
        var engine = CreateEngineAndBus(out var bus);

        using (bus)
        {
            engine.Start();
            bus.Published.Clear();

            // Act
            engine.ApplyDamage(new Damage(Amount: 10, Type: DamageType.Physical, IsCritical: false));

            // Assert
            bus.Published.Should().ContainSingle();
            var evt = bus.Published[0];
            evt.Type.Should().Be(PlayerHealthChanged.EventType);
            evt.Source.Should().Be(nameof(GameEngineCore));
            evt.Data.Should().NotBeNull();
        }
    }

    [Fact]
    public void Should_Publish_PlayerMoved_Event_And_Update_Position_When_Move_IsCalled()
    {
        // Arrange
        var engine = CreateEngineAndBus(out var bus);

        using (bus)
        {
            engine.Start();
            bus.Published.Clear();

            // Act
            var state = engine.Move(5.0, 3.0);

            // Assert
            state.Position.X.Should().Be(5.0);
            state.Position.Y.Should().Be(3.0);
            bus.Published.Should().ContainSingle();
            var evt = bus.Published[0];
            evt.Type.Should().Be(PlayerMoved.EventType);
            evt.Source.Should().Be(nameof(GameEngineCore));
        }
    }

    [Fact]
    public void Should_Publish_GameEnded_Event_And_Return_Result_When_End_IsCalled()
    {
        // Arrange
        var engine = CreateEngineAndBus(out var bus);

        using (bus)
        {
            engine.Start();
            engine.Move(10.0, 10.0);
            engine.AddScore(100);
            bus.Published.Clear();

            // Act
            var result = engine.End();

            // Assert
            result.FinalScore.Should().Be(100);
            result.PlayTimeSeconds.Should().BeGreaterThan(0);
            bus.Published.Should().ContainSingle();
            var evt = bus.Published[0];
            evt.Type.Should().Be(GameEnded.EventType);
            evt.Source.Should().Be(nameof(GameEngineCore));
        }
    }

    [Fact]
    public void Should_Not_Throw_When_Bus_Is_Null()
    {
        var config = new GameConfig(
            MaxLevel: 10,
            InitialHealth: 100,
            ScoreMultiplier: 1.0,
            AutoSave: false,
            Difficulty: Difficulty.Medium
        );

        var engine = new GameEngineCore(config, new Inventory(), seed: "test-seed", bus: null);

        engine.Start();
        engine.Move(1.0, 1.0);
        engine.AddScore(1);
        engine.ApplyDamage(new Damage(Amount: 1, Type: DamageType.Physical, IsCritical: false));
        engine.End();
    }
}
