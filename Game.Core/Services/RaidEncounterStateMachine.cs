using System;
using System.Collections.Generic;
using Game.Core.Contracts;
using Game.Core.Contracts.Raid;
using Game.Core.Ports;

namespace Game.Core.Services;

public enum RaidEncounterPhase
{
    Entering,
    Combat,
    Resolution,
    Failed,
    Completed
}

public sealed class RaidEncounterStateMachine
{
    private const string DefaultSource = "game.core/raid-encounter";
    private const int DefaultSuccessRewardPoints = 10;

    private readonly List<DomainEvent> _pendingEvents = new();
    private readonly ITime _time;
    private readonly IIdGenerator _idGenerator;

    private string? _raidId;
    private string? _guildId;
    private int _week;
    private string? _encounterId;

    public RaidEncounterPhase Phase { get; private set; } = RaidEncounterPhase.Entering;

    public RaidEncounterStateMachine(ITime? time = null, IIdGenerator? idGenerator = null)
    {
        _time = time ?? new SystemTime();
        _idGenerator = idGenerator ?? new GuidIdGenerator();
    }

    public void Start(string raidId, string guildId, int week, string encounterId)
    {
        if (string.IsNullOrWhiteSpace(raidId))
            throw new ArgumentException("raidId must be non-empty", nameof(raidId));
        if (string.IsNullOrWhiteSpace(guildId))
            throw new ArgumentException("guildId must be non-empty", nameof(guildId));
        if (week <= 0)
            throw new ArgumentOutOfRangeException(nameof(week), "week must be >= 1");
        if (string.IsNullOrWhiteSpace(encounterId))
            throw new ArgumentException("encounterId must be non-empty", nameof(encounterId));

        _pendingEvents.Clear();

        _raidId = raidId.Trim();
        _guildId = guildId.Trim();
        _week = week;
        _encounterId = encounterId.Trim();
        Phase = RaidEncounterPhase.Entering;

        var now = _time.UtcNowOffset;
        Enqueue(
            RaidScheduled.EventType,
            new RaidScheduled(
                RaidId: _raidId,
                GuildId: _guildId,
                Week: _week,
                EncounterId: _encounterId,
                ScheduledAt: now
            ));
    }

    public IReadOnlyList<DomainEvent> DequeueEvents()
    {
        var snapshot = _pendingEvents.ToArray();
        _pendingEvents.Clear();
        return snapshot;
    }

    public bool Advance()
    {
        EnsureStarted();

        if (Phase is RaidEncounterPhase.Failed or RaidEncounterPhase.Completed)
            return false;

        Phase = Phase switch
        {
            RaidEncounterPhase.Entering => RaidEncounterPhase.Combat,
            RaidEncounterPhase.Combat => RaidEncounterPhase.Resolution,
            RaidEncounterPhase.Resolution => Complete(),
            _ => throw new ArgumentOutOfRangeException(nameof(Phase), Phase, "Unknown encounter phase")
        };

        return true;
    }

    public bool Fail()
    {
        EnsureStarted();

        if (Phase is RaidEncounterPhase.Failed or RaidEncounterPhase.Completed)
            return false;

        Phase = RaidEncounterPhase.Failed;

        var now = _time.UtcNowOffset;
        Enqueue(
            RaidResolved.EventType,
            new RaidResolved(
                RaidId: _raidId!,
                GuildId: _guildId!,
                Week: _week,
                Result: RaidResolved.ResultFailed,
                RewardPoints: 0,
                ResolvedAt: now
            ));
        return true;
    }

    private RaidEncounterPhase Complete()
    {
        Phase = RaidEncounterPhase.Completed;

        var now = _time.UtcNowOffset;
        var rewardPoints = ComputeSuccessRewardPoints(week: _week);
        Enqueue(
            RaidResolved.EventType,
            new RaidResolved(
                RaidId: _raidId!,
                GuildId: _guildId!,
                Week: _week,
                Result: RaidResolved.ResultSuccess,
                RewardPoints: rewardPoints,
                ResolvedAt: now
            ));

        return Phase;
    }

    private static int ComputeSuccessRewardPoints(int week)
    {
        if (week < 1)
            week = 1;

        // Minimal deterministic reward rule for T17: always grant a small, fixed amount on success.
        // Rationale: makes "reward payout" observable and testable without introducing inventory/currency systems yet.
        return DefaultSuccessRewardPoints;
    }

    private void Enqueue(string type, object data)
    {
        var now = _time.UtcNowOffset;
        _pendingEvents.Add(new DomainEvent(
            Type: type,
            Source: DefaultSource,
            Data: data,
            Timestamp: now.UtcDateTime,
            Id: _idGenerator.NewId()
        ));
    }

    private void EnsureStarted()
    {
        if (_raidId is null || _guildId is null || _encounterId is null)
            throw new InvalidOperationException("Encounter state machine not started. Call Start(...) first.");
    }
}
