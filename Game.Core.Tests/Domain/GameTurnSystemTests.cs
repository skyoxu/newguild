using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Game.Contracts.GameLoop;
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

    private sealed class FakeTime : ITime
    {
        public double DeltaSeconds => 0.016; // Mock 16ms (60 FPS)
        public DateTimeOffset UtcNowOffset => DateTimeOffset.UtcNow;
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
        var eventBus = new CapturingEventBus();
        var time = new FakeTime();
        return new GameTurnSystem(engine, eventBus, time);
    }

    private static string? GetEventType(DomainEvent e)
    {
        return e.Type;
    }

    [Fact]
    public void Should_StartNewWeek_Initialize_Week_And_Phase()
    {
        // Arrange
        var system = CreateSystem();
        var saveId = new SaveIdValue("save-1");

        // Act
        var state = system.StartNewWeek(saveId);

        // Assert
        state.Week.Should().Be(1);
        state.Phase.Should().Be(GameTurnPhase.Resolution);
        state.SaveId.Should().Be(saveId);
        state.CurrentTime.Should().BeOnOrAfter(DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void Should_StartNewWeek_Require_SaveIdValue_As_Input_Type()
    {
        var method = typeof(GameTurnSystem).GetMethod(nameof(GameTurnSystem.StartNewWeek));
        method.Should().NotBeNull();

        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(SaveIdValue));
    }

    [Fact]
    public async Task Should_Advance_From_Resolution_To_Player_Phase()
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
    public async Task Should_Advance_From_Player_To_Ai_Phase()
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
    public async Task Should_Advance_From_Ai_Phase_To_Next_Week_Resolution()
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
    public async Task Should_Advance_Week_Cycle_From_NewWeek_To_WeekTwo_Resolution()
    {
        // Arrange
        var system = CreateSystem();
        var saveId = new SaveIdValue("save-t2");

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
    public async Task Should_PropagateException_When_Advance_FromResolutionPhase()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Resolution phase failed");
        var faultingEngine = new FaultingEventEngine(expectedException);
        var eventBus = new CapturingEventBus();
        var time = new FakeTime();
        var system = new GameTurnSystem(faultingEngine, eventBus, time);
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
    public async Task Should_PropagateException_When_Advance_FromPlayerPhase()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Player phase failed");
        var faultingEngine = new FaultingEventEngine(expectedException);
        var eventBus = new CapturingEventBus();
        var time = new FakeTime();
        var system = new GameTurnSystem(faultingEngine, eventBus, time);
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
    public async Task Should_PropagateException_When_Advance_FromAiPhase()
    {
        // Arrange
        var expectedException = new InvalidOperationException("AI phase failed");
        var faultingEngine = new FaultingEventEngine(expectedException);
        var eventBus = new CapturingEventBus();
        var time = new FakeTime();
        var system = new GameTurnSystem(faultingEngine, eventBus, time);
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

    // ACC:T45.7
    [Fact]
    public async Task Should_Publish_GameTurnStarted_When_Advance_FirstTurn()
    {
        // Arrange
        var eventBus = new CapturingEventBus();
        var engine = new DummyEventEngine();
        var time = new FakeTime();
        var system = new GameTurnSystem(engine, eventBus, time);
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
    public async Task Should_NotPublish_GameTurnStarted_When_WeekOne_NotResolutionPhase()
    {
        // Arrange
        var eventBus = new CapturingEventBus();
        var engine = new DummyEventEngine();
        var time = new FakeTime();
        var system = new GameTurnSystem(engine, eventBus, time);
        var state = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.Player,
            SaveId: new SaveIdValue("save-1"),
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act
        _ = await system.Advance(state);

        // Assert
        eventBus.Published.Should().NotContain(e => GetEventType(e) == "core.game_turn.started");
    }

    [Fact]
    public async Task Should_NotPublish_GameTurnStarted_When_Week_GreaterThanOne()
    {
        // Arrange
        var eventBus = new CapturingEventBus();
        var engine = new DummyEventEngine();
        var time = new FakeTime();
        var system = new GameTurnSystem(engine, eventBus, time);
        var state = new GameTurnState(
            Week: 2,
            Phase: GameTurnPhase.Resolution,
            SaveId: new SaveIdValue("save-2"),
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act
        _ = await system.Advance(state);

        // Assert
        eventBus.Published.Should().NotContain(e => GetEventType(e) == "core.game_turn.started");
    }

    [Fact]
    public async Task Should_Keep_State_Unchanged_When_Advance_Phase_Unknown()
    {
        // Arrange
        var eventBus = new CapturingEventBus();
        var engine = new DummyEventEngine();
        var time = new FakeTime();
        var system = new GameTurnSystem(engine, eventBus, time);
        var state = new GameTurnState(
            Week: 3,
            Phase: (GameTurnPhase)999,
            SaveId: new SaveIdValue("save-unknown"),
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act
        var next = await system.Advance(state);

        // Assert
        next.Should().Be(state);
        eventBus.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_Publish_GameTurnPhaseChanged_When_Resolution_To_Player()
    {
        // Arrange
        var eventBus = new CapturingEventBus();
        var engine = new DummyEventEngine();
        var time = new FakeTime();
        var system = new GameTurnSystem(engine, eventBus, time);
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
        var phaseChanged = phaseChangeEvent!.Data as Game.Contracts.GameLoop.GameTurnPhaseChanged;
        phaseChanged.Should().NotBeNull();
        phaseChanged!.PreviousPhase.Should().Be("Resolution");
        phaseChanged.CurrentPhase.Should().Be("Player");
    }

    [Fact]
    public async Task Should_Publish_GameWeekAdvanced_When_Completing_FullTurnCycle()
    {
        // Arrange
        var eventBus = new CapturingEventBus();
        var engine = new DummyEventEngine();
        var time = new FakeTime();
        var system = new GameTurnSystem(engine, eventBus, time);
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
        var weekAdvanced = weekAdvancedEvent!.Data as Game.Contracts.GameLoop.GameWeekAdvanced;
        weekAdvanced.Should().NotBeNull();
        weekAdvanced!.PreviousWeek.Should().Be(1);
        weekAdvanced.CurrentWeek.Should().Be(2);
    }

    // ACC:T45.6
    [Fact]
    public async Task Should_Publish_Correct_Event_Sequence_When_FullTurnCycle()
    {
        // Arrange
        var eventBus = new CapturingEventBus();
        var engine = new DummyEventEngine();
        var time = new FakeTime();
        var system = new GameTurnSystem(engine, eventBus, time);
        var startState = system.StartNewWeek(new SaveIdValue("save-t2"));

        // Act - execute complete Resolution -> Player -> AiSimulation cycle
        var afterResolution = await system.Advance(startState);
        var afterPlayer = await system.Advance(afterResolution);
        var afterAi = await system.Advance(afterPlayer);

        // Assert - RED phase: expects 4 events in correct sequence
        eventBus.Published.Should().HaveCount(4);

        // Event 1: GameTurnStarted at beginning
        GetEventType(eventBus.Published[0]).Should().Be("core.game_turn.started");

        // Event 2: PhaseChanged (Resolution -> Player)
        GetEventType(eventBus.Published[1]).Should().Be("core.game_turn.phase_changed");
        var phaseChange1 = eventBus.Published[1].Data as Game.Contracts.GameLoop.GameTurnPhaseChanged;
        phaseChange1!.PreviousPhase.Should().Be("Resolution");
        phaseChange1.CurrentPhase.Should().Be("Player");

        // Event 3: PhaseChanged (Player -> AiSimulation)
        GetEventType(eventBus.Published[2]).Should().Be("core.game_turn.phase_changed");
        var phaseChange2 = eventBus.Published[2].Data as Game.Contracts.GameLoop.GameTurnPhaseChanged;
        phaseChange2!.PreviousPhase.Should().Be("Player");
        phaseChange2.CurrentPhase.Should().Be("AiSimulation");

        // Event 4: WeekAdvanced (Week 1 -> Week 2)
        GetEventType(eventBus.Published[3]).Should().Be("core.game_turn.week_advanced");
        var weekAdvanced = eventBus.Published[3].Data as Game.Contracts.GameLoop.GameWeekAdvanced;
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
    public void Should_Reject_NullOrEmpty_SaveId_When_StartNewWeek(string? invalidSaveId)
    {
        // Arrange & Act
        var exception = Assert.Throws<ArgumentException>(() => _ = new SaveIdValue(invalidSaveId!));

        // Assert
        exception.Message.Should().Contain("SaveId");
    }

    [Theory]
    [InlineData("a")]  // Minimum valid length (1 char)
    [InlineData("valid-save-123")]  // Valid with hyphens
    [InlineData("ABC_def-789")]  // Valid with underscores and hyphens
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123")]  // Maximum valid length (64 chars)
    public void Should_Accept_Valid_SaveId_When_StartNewWeek(string validSaveId)
    {
        // Arrange
        var system = CreateSystem();
        var saveId = new SaveIdValue(validSaveId);

        // Act
        var state = system.StartNewWeek(saveId);

        // Assert
        state.SaveId.Should().Be(saveId);
    }

    [Theory]
    [InlineData("01234567890123456789012345678901234567890123456789012345678901234")]  // 65 chars - too long
    [InlineData("this-is-a-very-long-save-id-that-exceeds-the-maximum-allowed-length-of-64-characters")]  // Way too long
    public void Should_Reject_Overlong_SaveId_When_StartNewWeek(string overlongSaveId)
    {
        // Act
        var exception = Assert.Throws<ArgumentException>(() => _ = new SaveIdValue(overlongSaveId));

        // Assert
        exception.Message.Should().Contain("[a-zA-Z0-9_-]{1,64}");
    }

    [Theory]
    [InlineData("'; DROP TABLE saves--")]  // SQL injection attempt
    [InlineData("1' UNION SELECT * FROM users--")]  // SQL union injection
    [InlineData("admin'--")]  // SQL comment injection
    public void Should_Reject_SqlInjection_Patterns_When_StartNewWeek(string sqlInjectionPattern)
    {
        // Act
        var exception = Assert.Throws<ArgumentException>(() => _ = new SaveIdValue(sqlInjectionPattern));

        // Assert
        exception.Message.Should().Contain("[a-zA-Z0-9_-]{1,64}");
    }

    [Theory]
    [InlineData("../../etc/passwd")]  // Unix path traversal
    [InlineData("..\\..\\Windows\\System32")]  // Windows path traversal
    [InlineData("../../../secrets")]  // Relative path traversal
    public void Should_Reject_PathTraversal_Patterns_When_StartNewWeek(string pathTraversalPattern)
    {
        // Act
        var exception = Assert.Throws<ArgumentException>(() => _ = new SaveIdValue(pathTraversalPattern));

        // Assert
        exception.Message.Should().Contain("[a-zA-Z0-9_-]{1,64}");
    }

    [Theory]
    [InlineData("save@#$%")]  // Special characters
    [InlineData("save id with spaces")]  // Spaces
    [InlineData("save\nid\r\nwith\nnewlines")]  // Newlines
    [InlineData("save<script>alert('xss')</script>")]  // XSS attempt
    [InlineData("save|id&cmd")]  // Shell metacharacters
    public void Should_Reject_InvalidCharacters_When_StartNewWeek(string invalidCharsPattern)
    {
        // Act
        var exception = Assert.Throws<ArgumentException>(() => _ = new SaveIdValue(invalidCharsPattern));

        // Assert
        exception.Message.Should().Contain("[a-zA-Z0-9_-]{1,64}");
    }

    [Fact]
    public void Should_Use_SaveIdValueType_For_GameTurnState_SaveId()
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

    // ACC:T45.2
    [Fact]
    public void Should_NotRequire_IAICoordinator_Dependency_In_GameTurnSystem()
    {
        // Arrange
        var constructor = typeof(GameTurnSystem)
            .GetConstructors()
            .Single();

        // Act
        var parameterTypes = constructor
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToList();

        // Assert
        parameterTypes.Should().Contain(typeof(IEventEngine));
        parameterTypes.Should().Contain(typeof(IEventBus));
        parameterTypes.Should().Contain(typeof(ITime));
        parameterTypes.Should().NotContain(typeof(IAICoordinator),
            "GameTurnSystem constructor should not require IAICoordinator dependency");
    }

    // ACC:T45.3
    [Fact]
    public async Task Should_Publish_GameTurnStarted_Per_NewState_Without_HiddenFlag()
    {
        // Arrange
        var eventBus = new CapturingEventBus();
        var engine = new DummyEventEngine();
        var time = new FakeTime();
        var system = new GameTurnSystem(engine, eventBus, time);

        var firstState = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.Resolution,
            SaveId: new SaveIdValue("save-first"),
            CurrentTime: DateTimeOffset.UtcNow
        );

        var secondState = new GameTurnState(
            Week: 1,
            Phase: GameTurnPhase.Resolution,
            SaveId: new SaveIdValue("save-second"),
            CurrentTime: DateTimeOffset.UtcNow
        );

        // Act
        _ = await system.Advance(firstState);
        _ = await system.Advance(secondState);

        // Assert
        eventBus.Published
            .Count(eventItem => GetEventType(eventItem) == GameTurnStarted.EventType)
            .Should().Be(2,
                "start event emission should be derived from input state, not hidden mutable instance flags");
    }

    // ACC:T45.4
    [Fact]
    public void Should_Use_DateTimeOffset_For_DomainEvent_Timestamp()
    {
        // Arrange
        var timestampProperty = typeof(DomainEvent).GetProperty(nameof(DomainEvent.Timestamp));

        // Assert
        timestampProperty.Should().NotBeNull();
        timestampProperty!.PropertyType.Should().Be(typeof(DateTimeOffset));
    }

    // ACC:T45.1
    // ACC:T45.5
    // ACC:T45.8
    [Fact]
    public void Should_Converge_GameLoop_Interfaces_To_Ports_Namespace()
    {
        // Arrange
        var coreAssembly = typeof(GameTurnSystem).Assembly;

        // Act
        var engineNamespace = typeof(GameTurnSystem).Namespace;
        var eventEngineNamespace = typeof(EventEngine).Namespace;
        var aiCoordinatorNamespace = typeof(AICoordinator).Namespace;

        var interfaceTypes = coreAssembly.GetTypes()
            .Where(type => type.IsInterface)
            .Where(type => type.Name is nameof(IGameTurnSystem) or nameof(IEventEngine) or nameof(IAICoordinator) or nameof(IEventBus))
            .ToList();

        // Assert
        engineNamespace.Should().Be("Game.Core.Engine");
        eventEngineNamespace.Should().Be("Game.Core.Engine");
        aiCoordinatorNamespace.Should().Be("Game.Core.Engine");

        interfaceTypes.Should().HaveCount(4);
        interfaceTypes.Should().OnlyContain(type => type.Namespace == "Game.Core.Ports");

        object.ReferenceEquals(typeof(IGameTurnSystem).Assembly, coreAssembly).Should().BeTrue();
        object.ReferenceEquals(typeof(IEventEngine).Assembly, coreAssembly).Should().BeTrue();
        object.ReferenceEquals(typeof(IAICoordinator).Assembly, coreAssembly).Should().BeTrue();
        object.ReferenceEquals(typeof(IEventBus).Assembly, coreAssembly).Should().BeTrue();
    }
}
