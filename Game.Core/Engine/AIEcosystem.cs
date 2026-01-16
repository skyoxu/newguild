using System;
using System.Collections.Generic;
using Game.Core.Contracts;
using Game.Core.Contracts.AI;
using Game.Core.Contracts.Guild;
using Game.Core.Domain.Turn;
using Game.Core.Ports;

namespace Game.Core.Engine;

public sealed record AIEcosystemInput(
    SaveIdValue SaveId,
    int Week,
    string GuildId,
    int CurrentMembers,
    int MaxMembers,
    int CandidateCount
);

public sealed class AIEcosystem
{
    private readonly int _seed;
    private readonly ITime _time;
    private readonly IIdGenerator _idGenerator;

    public AIEcosystem(ITime time, IIdGenerator idGenerator, int seed = 0)
    {
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        _seed = seed;
    }

    public IReadOnlyList<DomainEvent> Advance(AIEcosystemInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Week <= 0)
            throw new ArgumentOutOfRangeException(nameof(input), "Week must be >= 1.");

        if (string.IsNullOrWhiteSpace(input.GuildId))
            throw new ArgumentException("GuildId cannot be empty.", nameof(input));

        if (input.MaxMembers <= 0)
            throw new ArgumentOutOfRangeException(nameof(input), "MaxMembers must be > 0.");

        if (input.CurrentMembers < 0 || input.CurrentMembers > input.MaxMembers)
            throw new ArgumentOutOfRangeException(nameof(input), "CurrentMembers must be within [0, MaxMembers].");

        if (input.CandidateCount < 0)
            throw new ArgumentOutOfRangeException(nameof(input), "CandidateCount must be >= 0.");

        var now = _time.UtcNowOffset;
        var events = new List<DomainEvent>(capacity: 3);

        var shouldLeave = input.CurrentMembers > 0 && ((input.Week + _seed) % 2 == 0);
        var shouldJoin = input.CandidateCount > 0 && input.CurrentMembers < input.MaxMembers && ((input.Week + _seed) % 3 == 0);

        if (shouldJoin)
        {
            var candidateId = $"npc-{((input.Week + _seed) % 9) + 1}";
            var joined = new GuildMemberJoined(
                UserId: candidateId,
                GuildId: input.GuildId,
                JoinedAt: now,
                Role: "member");

            events.Add(Wrap(GuildMemberJoined.EventType, joined, now));
        }

        if (shouldLeave)
        {
            var memberId = $"npc-{((input.Week + _seed + 1) % 9) + 1}";
            var left = new GuildMemberLeft(
                UserId: memberId,
                GuildId: input.GuildId,
                LeftAt: now,
                Reason: "ai_ecosystem");

            events.Add(Wrap(GuildMemberLeft.EventType, left, now));
        }

        var summary = $"seed={_seed};week={input.Week};join={shouldJoin};leave={shouldLeave};candidates={input.CandidateCount};members={input.CurrentMembers}/{input.MaxMembers}";
        var step = new AiEcosystemStepCompleted(
            SaveId: input.SaveId.Value,
            Week: input.Week,
            Summary: summary,
            CompletedAt: now);

        events.Add(Wrap(AiEcosystemStepCompleted.EventType, step, now));

        return events;
    }

    public IReadOnlyList<DomainEvent> Advance(GameTurnState? state)
    {
        if (state is null)
            return Array.Empty<DomainEvent>();

        var input = new AIEcosystemInput(
            SaveId: state.SaveId,
            Week: state.Week,
            GuildId: "temp-guild-id",
            CurrentMembers: 1,
            MaxMembers: 10,
            CandidateCount: 1
        );

        return Advance(input);
    }

    private DomainEvent Wrap(string type, object data, DateTimeOffset now)
    {
        return new DomainEvent(
            Type: type,
            Source: nameof(AIEcosystem),
            Data: data,
            Timestamp: now.UtcDateTime,
            Id: _idGenerator.NewId()
        );
    }
}
