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

    private sealed class FaultingEventBus : IEventBus
    {
        private readonly Exception _exceptionToThrow;

        public FaultingEventBus(Exception exceptionToThrow)
        {
            _exceptionToThrow = exceptionToThrow;
        }

        public Task PublishAsync(DomainEvent evt)
        {
            throw _exceptionToThrow;
        }

        public IDisposable Subscribe(Func<DomainEvent, Task> handler) => new DummySubscription();

        private sealed class DummySubscription : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private static (EventEngine Engine, CapturingEventBus Bus) CreateEngine()
    {
        var bus = new CapturingEventBus();
        var catalog = new DummyEventCatalog();
        var engine = new EventEngine(catalog, bus);
        return (engine, bus);
    }

    private static string? GetEventType(DomainEvent e)
    {
        return e.Type;
    }

    [Fact]
    public void Constructor_WithNullEventCatalog_ThrowsArgumentNullException()
    {
        // Arrange
        IEventCatalog? nullCatalog = null;
        var bus = new CapturingEventBus();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new EventEngine(nullCatalog!, bus));
        Assert.Equal("eventCatalog", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullEventBus_ThrowsArgumentNullException()
    {
        // Arrange
        var catalog = new DummyEventCatalog();
        IEventBus? nullBus = null;

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new EventEngine(catalog, nullBus!));
        Assert.Equal("eventBus", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange
        var catalog = new DummyEventCatalog();
        var bus = new CapturingEventBus();

        // Act
        var engine = new EventEngine(catalog, bus);

        // Assert
        Assert.NotNull(engine);
    }

    [Fact]
    public async Task ExecuteResolutionPhase_does_not_change_week_or_phase_by_default()
    {
        // Arrange
        var (engine, _) = CreateEngine();
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.Resolution,
            SaveId: new SaveIdValue("save-1"),
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act
        var next = await engine.ExecuteResolutionPhaseAsync(state);

        // Assert
        next.Week.Should().Be(state.Week);
        next.Phase.Should().Be(GameTurnPhase.Resolution);
    }

    [Fact]
    public async Task ExecutePlayerPhase_does_not_change_week_or_phase_by_default()
    {
        // Arrange
        var (engine, _) = CreateEngine();
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.Player,
            SaveId: new SaveIdValue("save-1"),
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act
        var next = await engine.ExecutePlayerPhaseAsync(state);

        // Assert
        next.Week.Should().Be(state.Week);
        next.Phase.Should().Be(GameTurnPhase.Player);
    }

    [Fact]
    public async Task ExecuteAiPhase_does_not_change_week_or_phase_by_default()
    {
        // Arrange
        var (engine, _) = CreateEngine();
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.AiSimulation,
            SaveId: new SaveIdValue("save-1"),
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act
        var next = await engine.ExecuteAiPhaseAsync(state);

        // Assert
        next.Week.Should().Be(state.Week);
        next.Phase.Should().Be(GameTurnPhase.AiSimulation);
    }

    [Fact]
    public async Task ExecuteResolutionPhase_PublishesGuildCreatedEvent_WhenGuildCreated()
    {
        // Arrange
        var (engine, bus) = CreateEngine();
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.Resolution,
            SaveId: new SaveIdValue("save-1"),
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act
        var next = await engine.ExecuteResolutionPhaseAsync(state);

        // Assert
        // T2 minimal implementation: verify EventEngine can publish core.guild.created
        // This test validates event bus integration per ADR-0004 CloudEvents naming
        // ✓ IMPLEMENTED: EventEngine.ExecuteResolutionPhaseAsync() publishes GuildCreated
        bus.Published.Should().ContainSingle(e => GetEventType(e) == "core.guild.created");
    }

    [Fact]
    public async Task ExecutePlayerPhase_PublishesGuildMemberJoinedEvent_WhenMemberJoins()
    {
        // Arrange
        var (engine, bus) = CreateEngine();
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.Player,
            SaveId: new SaveIdValue("save-1"),
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act
        var next = await engine.ExecutePlayerPhaseAsync(state);

        // Assert
        // T2 minimal implementation: verify EventEngine can publish core.guild.member.joined
        // This test validates event bus integration per ADR-0004 CloudEvents naming
        // TODO: Implement member join logic in EventEngine to make this test pass
        bus.Published.Should().ContainSingle(e => GetEventType(e) == "core.guild.member.joined");
    }

    [Fact]
    public async Task ExecuteAiPhase_PublishesGuildMemberLeftEvent_WhenMemberLeaves()
    {
        // Arrange
        var (engine, bus) = CreateEngine();
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.AiSimulation,
            SaveId: new SaveIdValue("save-1"),
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act
        var next = await engine.ExecuteAiPhaseAsync(state);

        // Assert
        // T2 minimal implementation: verify EventEngine can publish core.guild.member.left
        // This test validates event bus integration per ADR-0004 CloudEvents naming
        // TODO: Implement member leave logic in EventEngine to make this test pass
        bus.Published.Should().ContainSingle(e => GetEventType(e) == "core.guild.member.left");
    }

    [Fact]
    public async Task ExecuteResolutionPhase_PropagatesException_WhenEventBusFails()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Event bus connection failed");
        var faultingBus = new FaultingEventBus(expectedException);
        var catalog = new DummyEventCatalog();
        var engine = new EventEngine(catalog, faultingBus);
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.Resolution,
            SaveId: new SaveIdValue("save-1"),
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await engine.ExecuteResolutionPhaseAsync(state)
        );
        exception.Message.Should().Be("Event bus connection failed");
    }

    [Fact]
    public async Task ExecutePlayerPhase_PropagatesException_WhenEventBusFails()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Event bus unavailable");
        var faultingBus = new FaultingEventBus(expectedException);
        var catalog = new DummyEventCatalog();
        var engine = new EventEngine(catalog, faultingBus);
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.Player,
            SaveId: new SaveIdValue("save-1"),
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await engine.ExecutePlayerPhaseAsync(state)
        );
        exception.Message.Should().Be("Event bus unavailable");
    }

    [Fact]
    public async Task ExecuteAiPhase_PropagatesException_WhenEventBusFails()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Event publishing timeout");
        var faultingBus = new FaultingEventBus(expectedException);
        var catalog = new DummyEventCatalog();
        var engine = new EventEngine(catalog, faultingBus);
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.AiSimulation,
            SaveId: new SaveIdValue("save-1"),
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await engine.ExecuteAiPhaseAsync(state)
        );
        exception.Message.Should().Be("Event publishing timeout");
    }

    [Fact]
    public async Task ExecuteResolutionPhase_HandlesNullState_Gracefully()
    {
        // Arrange
        var (engine, _) = CreateEngine();
        GameTurnState? nullState = null;

        // Act & Assert - Should handle null appropriately
        // In production code, this would validate state != null
        // For now, testing current behavior
        await engine.Invoking(e => e.ExecuteResolutionPhaseAsync(nullState!))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecutePlayerPhase_HandlesNullState_Gracefully()
    {
        // Arrange
        var (engine, _) = CreateEngine();
        GameTurnState? nullState = null;

        // Act & Assert
        await engine.Invoking(e => e.ExecutePlayerPhaseAsync(nullState!))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAiPhase_HandlesNullState_Gracefully()
    {
        // Arrange
        var (engine, _) = CreateEngine();
        GameTurnState? nullState = null;

        // Act & Assert
        await engine.Invoking(e => e.ExecuteAiPhaseAsync(nullState!))
            .Should().NotThrowAsync();
    }
}
