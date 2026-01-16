using System;
using System.Linq;
using FluentAssertions;
using Game.Core.Contracts;
using Game.Core.Domain.Turn;
using Game.Core.Engine;
using Game.Core.Ports;
using Xunit;

namespace Game.Core.Tests.Services;

public class AIEcosystemDeterminismAndEventsTests
{
    private sealed class FixedTime : ITime
    {
        public FixedTime(DateTimeOffset now) => UtcNowOffset = now;
        public double DeltaSeconds => 0.0;
        public DateTimeOffset UtcNowOffset { get; }
    }

    private sealed class SequenceIdGenerator : IIdGenerator
    {
        private int _next;
        public SequenceIdGenerator(int start = 0) => _next = start;
        public string NewId() => $"id-{_next++:D4}";
    }

    // ACC:T15.1
    [Fact]
    public void Should_Be_Deterministic_For_Same_Seed_And_Input()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var saveId = new SaveIdValue("save-1");

        var input = new AIEcosystemInput(
            SaveId: saveId,
            Week: 1,
            GuildId: "g-1",
            CurrentMembers: 1,
            MaxMembers: 10,
            CandidateCount: 0);

        var a = new AIEcosystem(new FixedTime(now), new SequenceIdGenerator(), seed: 1);
        var b = new AIEcosystem(new FixedTime(now), new SequenceIdGenerator(), seed: 1);

        var eventsA = a.Advance(input);
        var eventsB = b.Advance(input);

        eventsA.Should().Equal(eventsB);
    }

    // ACC:T15.3
    [Fact]
    public void Should_Not_Reference_Godot_Assembly_From_AIEcosystem()
    {
        var referenced = typeof(AIEcosystem).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        referenced.Should().NotContain(n => n.StartsWith("Godot", StringComparison.OrdinalIgnoreCase));
    }

    // ACC:T15.4
    [Fact]
    public void Should_Handle_Boundary_Scenarios_Without_Throwing()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var saveId = new SaveIdValue("save-1");
        var ecosystem = new AIEcosystem(new FixedTime(now), new SequenceIdGenerator(), seed: 1);

        // empty guild: CurrentMembers=0 should never emit GuildMemberLeft
        var empty = ecosystem.Advance(new AIEcosystemInput(saveId, 1, "g-1", 0, 10, 0));
        empty.Should().OnlyContain(e => e.Type != "core.guild.member.left");

        // full guild: CurrentMembers==MaxMembers should never emit GuildMemberJoined
        var full = ecosystem.Advance(new AIEcosystemInput(saveId, 1, "g-1", 10, 10, 9));
        full.Should().OnlyContain(e => e.Type != "core.guild.member.joined");

        // no candidates: CandidateCount=0 should never emit GuildMemberJoined
        var none = ecosystem.Advance(new AIEcosystemInput(saveId, 1, "g-1", 1, 10, 0));
        none.Should().OnlyContain(e => e.Type != "core.guild.member.joined");
    }

    [Fact]
    public void Should_Emit_GuildMemberJoined_When_Candidates_Available_And_Not_Full()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var saveId = new SaveIdValue("save-1");
        var ecosystem = new AIEcosystem(new FixedTime(now), new SequenceIdGenerator(), seed: 1);

        // (Week + seed) % 3 == 0 => join; with seed=1, Week=2 triggers join.
        var events = ecosystem.Advance(new AIEcosystemInput(saveId, 2, "g-1", 1, 10, 1));

        events.Should().ContainSingle(e => e.Type == "core.guild.member.joined");
        events.Should().ContainSingle(e => e.Type == "core.ai.ecosystem.step.completed");
    }

    [Fact]
    public void Should_Emit_GuildMemberLeft_When_Guild_Has_Members()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var saveId = new SaveIdValue("save-1");
        var ecosystem = new AIEcosystem(new FixedTime(now), new SequenceIdGenerator(), seed: 1);

        // (Week + seed) % 2 == 0 => leave; with seed=1, Week=1 triggers leave.
        var events = ecosystem.Advance(new AIEcosystemInput(saveId, 1, "g-1", 1, 10, 0));

        events.Should().ContainSingle(e => e.Type == "core.guild.member.left");
        events.Should().ContainSingle(e => e.Type == "core.ai.ecosystem.step.completed");
    }

    [Fact]
    public void Should_Always_Emit_StepCompleted()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var saveId = new SaveIdValue("save-1");
        var ecosystem = new AIEcosystem(new FixedTime(now), new SequenceIdGenerator(), seed: 1);

        var events = ecosystem.Advance(new AIEcosystemInput(saveId, 1, "g-1", 1, 10, 0));
        events.Should().ContainSingle(e => e.Type == "core.ai.ecosystem.step.completed");
    }
}
