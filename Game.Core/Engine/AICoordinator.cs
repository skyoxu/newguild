using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Contracts;
using Game.Core.Contracts.AI;
using Game.Core.Domain.Turn;
using Game.Core.Ports;
using Game.Core.Ports.AI;
using Game.Core.Services;

namespace Game.Core.Engine;

public interface IAICoordinator
{
    IReadOnlyList<DomainEvent> GenerateAiEvents(GameTurnState state);
}

public sealed class AICoordinator : IAICoordinator
{
    private const string JoinIntentType = "core.guild.member.join";

    private readonly IAiWorldStatePort _world;
    private readonly IIdGenerator _idGenerator;

    public AICoordinator()
        : this(new NullAiWorldStatePort(), new GuidIdGenerator())
    {
    }

    public AICoordinator(IAiWorldStatePort world, IIdGenerator idGenerator)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
    }

    public IReadOnlyList<DomainEvent> GenerateAiEvents(GameTurnState state)
    {
        if (state.Phase != GameTurnPhase.AiSimulation)
            return Array.Empty<DomainEvent>();

        var snapshot = _world.GetSnapshot(state.SaveId, state.Week);
        var now = state.CurrentTime;
        const string source = nameof(AICoordinator);

        var decisions = BuildJoinDecisions(snapshot);
        var winners = ResolveConflicts(snapshot, decisions);

        var affinityDeltas = BuildAffinityDeltas(snapshot, winners, decisions);
        var joins = winners.Select(w => new AiMemberJoinedGuild(w.ActorId, w.TargetId)).ToList();
        var delta = affinityDeltas.Count == 0 && joins.Count == 0
            ? AiWorldDelta.Empty
            : new AiWorldDelta(affinityDeltas, joins);

        _world.Apply(state.SaveId, state.Week, delta);

        var events = new List<DomainEvent>(capacity: 2 + winners.Count);

        events.Add(Wrap(
            type: AiCycleStarted.EventType,
            source: source,
            data: new AiCycleStarted(
                SaveId: state.SaveId.Value,
                Week: state.Week,
                StartedAt: now
            ),
            now: now,
            id: _idGenerator.NewId()
        ));

        foreach (var intent in winners)
        {
            events.Add(Wrap(
                type: AiIntentIssued.EventType,
                source: source,
                data: new AiIntentIssued(
                    SaveId: state.SaveId.Value,
                    Week: state.Week,
                    IntentId: _idGenerator.NewId(),
                    IntentType: intent.IntentType,
                    ActorId: intent.ActorId,
                    TargetId: intent.TargetId,
                    IssuedAt: now
                ),
                now: now,
                id: _idGenerator.NewId()
            ));
        }

        events.Add(Wrap(
            type: AiCycleCompleted.EventType,
            source: source,
            data: new AiCycleCompleted(
                SaveId: state.SaveId.Value,
                Week: state.Week,
                IntentsIssued: winners.Count,
                CompletedAt: now
            ),
            now: now,
            id: _idGenerator.NewId()
        ));

        return events;
    }

    private static List<AiDecision> BuildJoinDecisions(AiWorldSnapshot snapshot)
    {
        var decisions = new List<AiDecision>(snapshot.Members.Count);

        foreach (var (_, member) in snapshot.Members)
        {
            if (!string.IsNullOrWhiteSpace(member.CurrentGuildId))
                continue;

            var best = FindBestJoinTarget(snapshot, member.MemberId);
            if (best is null)
                continue;

            decisions.Add(new AiDecision(
                ActorId: member.MemberId,
                TargetId: best,
                IntentType: JoinIntentType
            ));
        }

        return decisions;
    }

    private static string? FindBestJoinTarget(AiWorldSnapshot snapshot, string memberId)
    {
        snapshot.AffinityByMember.TryGetValue(memberId, out var affinityMap);

        var bestGuildId = (string?)null;
        var bestScore = int.MinValue;

        foreach (var (guildId, guild) in snapshot.Guilds)
        {
            if (guild.CurrentMembers >= guild.MaxMembers)
                continue;

            var score = 0;
            if (affinityMap is not null && affinityMap.TryGetValue(guildId, out var affinityScore))
                score = affinityScore;

            if (score > bestScore)
            {
                bestScore = score;
                bestGuildId = guildId;
                continue;
            }

            if (score == bestScore && bestGuildId is not null && string.CompareOrdinal(guildId, bestGuildId) < 0)
                bestGuildId = guildId;
        }

        return bestGuildId;
    }

    private static List<AiDecision> ResolveConflicts(AiWorldSnapshot snapshot, List<AiDecision> decisions)
    {
        var byTarget = decisions
            .GroupBy(d => d.TargetId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var winners = new List<AiDecision>(byTarget.Count);

        foreach (var (targetId, contenders) in byTarget)
        {
            var winner = contenders[0];
            var winnerScore = GetAffinity(snapshot, winner.ActorId, targetId);

            for (var i = 1; i < contenders.Count; i++)
            {
                var contender = contenders[i];
                var score = GetAffinity(snapshot, contender.ActorId, targetId);

                if (score > winnerScore)
                {
                    winner = contender;
                    winnerScore = score;
                    continue;
                }

                if (score == winnerScore && string.CompareOrdinal(contender.ActorId, winner.ActorId) < 0)
                    winner = contender;
            }

            winners.Add(winner);
        }

        winners.Sort((a, b) => string.CompareOrdinal(a.ActorId, b.ActorId));
        return winners;
    }

    private static List<AiAffinityDelta> BuildAffinityDeltas(
        AiWorldSnapshot snapshot,
        List<AiDecision> winners,
        List<AiDecision> allDecisions)
    {
        var winnerSet = new HashSet<string>(winners.Select(w => $"{w.ActorId}|{w.TargetId}"), StringComparer.Ordinal);
        var deltas = new List<AiAffinityDelta>(allDecisions.Count);

        foreach (var decision in allDecisions)
        {
            var key = $"{decision.ActorId}|{decision.TargetId}";
            var delta = winnerSet.Contains(key) ? 1 : -1;
            deltas.Add(new AiAffinityDelta(decision.ActorId, decision.TargetId, delta));
        }

        return deltas;
    }

    private static int GetAffinity(AiWorldSnapshot snapshot, string memberId, string guildId)
    {
        if (!snapshot.AffinityByMember.TryGetValue(memberId, out var map))
            return 0;
        return map.TryGetValue(guildId, out var score) ? score : 0;
    }

    private static DomainEvent Wrap(string type, string source, object? data, DateTimeOffset now, string id)
    {
        return new DomainEvent(
            Type: type,
            Source: source,
            Data: data,
            Timestamp: now.UtcDateTime,
            Id: id
        );
    }

    private sealed record AiDecision(
        string ActorId,
        string TargetId,
        string IntentType
    );

    private sealed class NullAiWorldStatePort : IAiWorldStatePort
    {
        public AiWorldSnapshot GetSnapshot(SaveIdValue saveId, int week) => AiWorldSnapshot.Empty;
        public void Apply(SaveIdValue saveId, int week, AiWorldDelta delta) { }
    }
}
