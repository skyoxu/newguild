using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Core.Contracts;
using Game.Core.Domain.Turn;
using Game.Core.Engine;
using Game.Core.Ports;
using Game.Core.Services;
using Xunit;

namespace Game.Core.Tests.Domain;

public class GameTurnSystemTests
{
    private sealed class CapturingEventBus : IEventBus
    {
        public List<DomainEvent> Published { get; } = new();

        public Task PublishAsync(DomainEvent evt)
        {
            Published.Add(evt);
            return Task.CompletedTask;
        }

        public IDisposable Subscribe(Func<DomainEvent, Task> handler) => new DummySubscription();

        private sealed class DummySubscription : IDisposable
        {
            public void Dispose() { }
        }
    }

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

    private sealed class FakeTime : ITime
    {
        public double DeltaSeconds => 0.016; // Mock 16ms (60 FPS)
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
        var eventBus = new CapturingEventBus();
        var time = new FakeTime();
        return new GameTurnSystem(engine, ai, eventBus, time);
    }

    private static string? GetEventType(DomainEvent e)
    {
        return e.Type;
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
        state.SaveId.ToString().Should().Be(saveId);
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
            SaveId: new SaveIdValue("save-1"),
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
            SaveId: new SaveIdValue("save-1"),
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
            SaveId: new SaveIdValue("save-1"),
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
        afterAi.SaveId.ToString().Should().Be(saveId);
    }

    [Fact]
    public async Task Advance_PropagatesException_FromResolutionPhase()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Resolution phase failed");
        var faultingEngine = new FaultingEventEngine(expectedException);
        var ai = new DummyAICoordinator();
        var eventBus = new CapturingEventBus();
        var time = new FakeTime();
        var system = new GameTurnSystem(faultingEngine, ai, eventBus, time);
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.Resolution,
            SaveId: new SaveIdValue("save-1"),
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
        var eventBus = new CapturingEventBus();
        var time = new FakeTime();
        var system = new GameTurnSystem(faultingEngine, ai, eventBus, time);
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.Player,
            SaveId: new SaveIdValue("save-1"),
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
        var eventBus = new CapturingEventBus();
        var time = new FakeTime();
        var system = new GameTurnSystem(faultingEngine, ai, eventBus, time);
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.AiSimulation,
            SaveId: new SaveIdValue("save-1"),
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await system.Advance(state)
        );
        exception.Message.Should().Be("AI phase failed");
    }

    [Fact]
    public async Task Advance_publishes_GameTurnStarted_event_at_start_of_first_turn()
    {
        // Arrange
        var eventBus = new CapturingEventBus();
        var engine = new DummyEventEngine();
        var ai = new DummyAICoordinator();
        var time = new FakeTime();
        var system = new GameTurnSystem(engine, ai, eventBus, time);
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.Resolution,
            SaveId: new SaveIdValue("save-1"),
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act
        var next = await system.Advance(state);

        // Assert - RED phase: this will fail because GameTurnSystem doesn't publish events yet
        eventBus.Published.Should().ContainSingle(e => GetEventType(e) == "core.game_turn.started");
    }

    [Fact]
    public async Task Advance_publishes_GameTurnPhaseChanged_when_transitioning_resolution_to_player()
    {
        // Arrange
        var eventBus = new CapturingEventBus();
        var engine = new DummyEventEngine();
        var ai = new DummyAICoordinator();
        var time = new FakeTime();
        var system = new GameTurnSystem(engine, ai, eventBus, time);
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.Resolution,
            SaveId: new SaveIdValue("save-1"),
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act
        var next = await system.Advance(state);

        // Assert - RED phase: expects phase change event from Resolution to Player
        var phaseChangeEvent = eventBus.Published.SingleOrDefault(e => GetEventType(e) == "core.game_turn.phase_changed");
        phaseChangeEvent.Should().NotBeNull();
        var phaseChanged = phaseChangeEvent!.Data as Game.Core.Contracts.GameLoop.GameTurnPhaseChanged;
        phaseChanged.Should().NotBeNull();
        phaseChanged!.PreviousPhase.Should().Be("Resolution");
        phaseChanged.CurrentPhase.Should().Be("Player");
    }

    [Fact]
    public async Task Advance_publishes_GameWeekAdvanced_when_completing_full_turn_cycle()
    {
        // Arrange
        var eventBus = new CapturingEventBus();
        var engine = new DummyEventEngine();
        var ai = new DummyAICoordinator();
        var time = new FakeTime();
        var system = new GameTurnSystem(engine, ai, eventBus, time);
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.AiSimulation,
            SaveId: new SaveIdValue("save-1"),
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act
        var next = await system.Advance(state);

        // Assert - RED phase: expects week advanced event when transitioning from AI to next week
        var weekAdvancedEvent = eventBus.Published.SingleOrDefault(e => GetEventType(e) == "core.game_turn.week_advanced");
        weekAdvancedEvent.Should().NotBeNull();
        var weekAdvanced = weekAdvancedEvent!.Data as Game.Core.Contracts.GameLoop.GameWeekAdvanced;
        weekAdvanced.Should().NotBeNull();
        weekAdvanced!.PreviousWeek.Should().Be(1);
        weekAdvanced.CurrentWeek.Should().Be(2);
    }

    [Fact]
    public async Task Full_turn_cycle_publishes_correct_event_sequence()
    {
        // Arrange
        var eventBus = new CapturingEventBus();
        var engine = new DummyEventEngine();
        var ai = new DummyAICoordinator();
        var time = new FakeTime();
        var system = new GameTurnSystem(engine, ai, eventBus, time);
        var startState = system.StartNewWeek("save-t2");

        // Act - execute complete Resolution → Player → AiSimulation cycle
        var afterResolution = await system.Advance(startState);
        var afterPlayer = await system.Advance(afterResolution);
        var afterAi = await system.Advance(afterPlayer);

        // Assert - RED phase: expects 4 events in correct sequence
        eventBus.Published.Should().HaveCount(4);

        // Event 1: GameTurnStarted at beginning
        GetEventType(eventBus.Published[0]).Should().Be("core.game_turn.started");

        // Event 2: PhaseChanged (Resolution → Player)
        GetEventType(eventBus.Published[1]).Should().Be("core.game_turn.phase_changed");
        var phaseChange1 = eventBus.Published[1].Data as Game.Core.Contracts.GameLoop.GameTurnPhaseChanged;
        phaseChange1!.PreviousPhase.Should().Be("Resolution");
        phaseChange1.CurrentPhase.Should().Be("Player");

        // Event 3: PhaseChanged (Player → AiSimulation)
        GetEventType(eventBus.Published[2]).Should().Be("core.game_turn.phase_changed");
        var phaseChange2 = eventBus.Published[2].Data as Game.Core.Contracts.GameLoop.GameTurnPhaseChanged;
        phaseChange2!.PreviousPhase.Should().Be("Player");
        phaseChange2.CurrentPhase.Should().Be("AiSimulation");

        // Event 4: WeekAdvanced (Week 1 → Week 2)
        GetEventType(eventBus.Published[3]).Should().Be("core.game_turn.week_advanced");
        var weekAdvanced = eventBus.Published[3].Data as Game.Core.Contracts.GameLoop.GameWeekAdvanced;
        weekAdvanced!.PreviousWeek.Should().Be(1);
        weekAdvanced.CurrentWeek.Should().Be(2);
    }

    // =====================================================================
    // SaveIdValue Validation Tests (NG-0040: F001 Security Finding)
    // ADR-0019: Godot Security Baseline - Input Validation with Whitelists
    // =====================================================================

    [Theory]
    [InlineData("")]  // Empty string
    [InlineData("   ")]  // Whitespace only
    [InlineData(null)]  // Null
    public void StartNewWeek_RejectsNullOrEmptySaveId(string? invalidSaveId)
    {
        // Arrange
        var system = CreateSystem();

        // Act & Assert - RED: This will fail because StartNewWeek currently accepts string without validation
        // Expected: Should throw ArgumentException for invalid SaveId
        var exception = Assert.Throws<ArgumentException>(() => system.StartNewWeek(invalidSaveId!));
        exception.Message.Should().Contain("SaveId");
    }

    [Theory]
    [InlineData("a")]  // Minimum valid length (1 char)
    [InlineData("valid-save-123")]  // Valid with hyphens
    [InlineData("ABC_def-789")]  // Valid with underscores and hyphens
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123")]  // Maximum valid length (64 chars)
    public void StartNewWeek_AcceptsValidSaveId(string validSaveId)
    {
        // Arrange
        var system = CreateSystem();

        // Act - RED: This will fail because StartNewWeek signature needs to change
        var state = system.StartNewWeek(validSaveId);

        // Assert
        state.SaveId.Should().NotBeNull();
        state.SaveId.ToString().Should().Be(validSaveId);  // SaveIdValue should have meaningful ToString()
    }

    [Theory]
    [InlineData("01234567890123456789012345678901234567890123456789012345678901234")]  // 65 chars - too long
    [InlineData("this-is-a-very-long-save-id-that-exceeds-the-maximum-allowed-length-of-64-characters")]  // Way too long
    public void StartNewWeek_RejectsOverlongSaveId(string overlongSaveId)
    {
        // Arrange
        var system = CreateSystem();

        // Act & Assert - RED: Should reject SaveIds longer than 64 characters
        var exception = Assert.Throws<ArgumentException>(() => system.StartNewWeek(overlongSaveId));
        exception.Message.Should().Contain("Must match [a-zA-Z0-9_-]{1,64}");
    }

    [Theory]
    [InlineData("'; DROP TABLE saves--")]  // SQL injection attempt
    [InlineData("1' UNION SELECT * FROM users--")]  // SQL union injection
    [InlineData("admin'--")]  // SQL comment injection
    public void StartNewWeek_RejectsSqlInjectionPatterns(string sqlInjectionPattern)
    {
        // Arrange
        var system = CreateSystem();

        // Act & Assert - RED: Should reject SaveIds with SQL special characters
        var exception = Assert.Throws<ArgumentException>(() => system.StartNewWeek(sqlInjectionPattern));
        exception.Message.Should().Contain("SaveId");
    }

    [Theory]
    [InlineData("../../etc/passwd")]  // Unix path traversal
    [InlineData("..\\..\\Windows\\System32")]  // Windows path traversal
    [InlineData("../../../secrets")]  // Relative path traversal
    public void StartNewWeek_RejectsPathTraversalPatterns(string pathTraversalPattern)
    {
        // Arrange
        var system = CreateSystem();

        // Act & Assert - RED: Should reject SaveIds with path traversal patterns
        var exception = Assert.Throws<ArgumentException>(() => system.StartNewWeek(pathTraversalPattern));
        exception.Message.Should().Contain("SaveId");
    }

    [Theory]
    [InlineData("save@#$%")]  // Special characters
    [InlineData("save id with spaces")]  // Spaces
    [InlineData("save\nid\r\nwith\nnewlines")]  // Newlines
    [InlineData("save<script>alert('xss')</script>")]  // XSS attempt
    [InlineData("save|id&cmd")]  // Shell metacharacters
    public void StartNewWeek_RejectsInvalidCharacters(string invalidCharsPattern)
    {
        // Arrange
        var system = CreateSystem();

        // Act & Assert - RED: Should only accept [a-zA-Z0-9_-]{1,64}
        var exception = Assert.Throws<ArgumentException>(() => system.StartNewWeek(invalidCharsPattern));
        exception.Message.Should().Contain("SaveId");
    }

    [Fact]
    public void GameTurnState_SaveId_ShouldUseSaveIdValueType()
    {
        // Arrange & Act - RED: This will fail because GameTurnState.SaveId is currently string
        // We want to verify that SaveId is of type SaveIdValue, not string
        var stateType = typeof(GameTurnState);
        var saveIdProperty = stateType.GetProperty("SaveId");

        // Assert
        saveIdProperty.Should().NotBeNull();
        saveIdProperty!.PropertyType.Should().Be(typeof(SaveIdValue),
            "SaveId should use SaveIdValue type for type-safe validation (ADR-0019)");
    }
}
