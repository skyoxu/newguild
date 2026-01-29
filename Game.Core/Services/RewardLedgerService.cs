using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Core.Contracts;
using Game.Core.Contracts.Engine;
using Game.Core.Contracts.Media;
using Game.Core.Contracts.Raid;
using Game.Core.Contracts.Recruitment;
using Game.Core.Ports;
using Game.Core.Progression;

namespace Game.Core.Services;

public sealed class RewardLedgerService : IDisposable
{
    public const string RewardTypeScore = "score";
    public const string RewardTypeReputation = "reputation";

    private const int RaidSuccessReputationDelta = 1;
    private const int MediaBeatReputationDelta = 1;
    private const int RecruitmentAcceptedScore = 5;
    private const int RecruitmentAcceptedReputationDelta = 2;
    private const int MinReputation = 0;
    private const int MaxReputation = 100;

    private readonly IEventBus _eventBus;
    private readonly ITime _time;
    private readonly IIdGenerator _idGenerator;
    private readonly object _gate = new();
    private RewardLedger _ledger = new();
    private readonly Dictionary<string, int> _reputationByGuild = new(StringComparer.Ordinal);
    private int _scoreTotal;
    private IDisposable? _subscription;

    public RewardLedgerService(IEventBus eventBus, ITime? time = null, IIdGenerator? idGenerator = null)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _time = time ?? new SystemTime();
        _idGenerator = idGenerator ?? new GuidIdGenerator();
    }

    public IDisposable Start()
    {
        if (_subscription != null)
            return _subscription;

        _subscription = _eventBus.Subscribe(HandleAsync);
        return _subscription;
    }

    public void Stop()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    public void Dispose()
    {
        Stop();
    }

    public IReadOnlyList<RewardGrant> Replay() => _ledger.Replay();

    public string Save() => _ledger.Save();

    public async Task LoadAsync(string data)
    {
        var loaded = RewardLedger.Load(data);
        lock (_gate)
        {
            _ledger = loaded;
            _scoreTotal = 0;
            _reputationByGuild.Clear();
        }

        await ReplayAsync().ConfigureAwait(false);
    }

    public async Task ReplayAsync()
    {
        List<RewardGrant> grants;
        lock (_gate)
        {
            _scoreTotal = 0;
            _reputationByGuild.Clear();
            grants = _ledger.Replay().ToList();
        }

        foreach (var grant in grants)
            await ApplyGrantAsync(grant, record: false).ConfigureAwait(false);
    }

    private Task HandleAsync(DomainEvent evt)
    {
        if (evt == null)
            return Task.CompletedTask;

        if (evt.Type == RaidResolved.EventType && evt.Data is RaidResolved raid)
        {
            var grant = BuildRaidGrant(raid, evt.Id);
            return grant == null ? Task.CompletedTask : ApplyGrantAsync(grant, record: true);
        }

        if (evt.Type == MediaBeatTriggered.EventType && evt.Data is MediaBeatTriggered beat)
        {
            var grant = BuildMediaGrant(beat, evt.Id);
            return ApplyGrantAsync(grant, record: true);
        }

        if (evt.Type == RecruitmentOfferResolved.EventType && evt.Data is RecruitmentOfferResolved offer)
        {
            var grant = BuildRecruitmentGrant(offer, evt.Id);
            return grant == null ? Task.CompletedTask : ApplyGrantAsync(grant, record: true);
        }

        return Task.CompletedTask;
    }

    private RewardGrant? BuildRaidGrant(RaidResolved raid, string eventId)
    {
        var rewards = new Dictionary<string, int>(StringComparer.Ordinal);
        if (raid.RewardPoints > 0)
            rewards[RewardTypeScore] = raid.RewardPoints;
        if (raid.RewardPoints > 0)
            rewards[RewardTypeReputation] = RaidSuccessReputationDelta;

        if (rewards.Count == 0)
            return null;

        return new RewardGrant(
            grantId: NormalizeEventId(eventId),
            guildId: raid.GuildId,
            sourceType: RaidResolved.EventType,
            sourceId: raid.RaidId,
            rewards: rewards);
    }

    private RewardGrant BuildMediaGrant(MediaBeatTriggered beat, string eventId)
    {
        var rewards = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [RewardTypeReputation] = MediaBeatReputationDelta
        };

        return new RewardGrant(
            grantId: NormalizeEventId(eventId),
            guildId: beat.GuildId,
            sourceType: MediaBeatTriggered.EventType,
            sourceId: beat.BeatId,
            rewards: rewards);
    }

    private RewardGrant? BuildRecruitmentGrant(RecruitmentOfferResolved offer, string eventId)
    {
        if (!string.Equals(offer.Decision, "accepted", StringComparison.OrdinalIgnoreCase))
            return null;

        var rewards = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [RewardTypeScore] = RecruitmentAcceptedScore,
            [RewardTypeReputation] = RecruitmentAcceptedReputationDelta
        };

        return new RewardGrant(
            grantId: NormalizeEventId(eventId),
            guildId: offer.GuildId,
            sourceType: RecruitmentOfferResolved.EventType,
            sourceId: offer.OfferId,
            rewards: rewards);
    }

    private async Task ApplyGrantAsync(RewardGrant grant, bool record)
    {
        if (grant == null)
            throw new ArgumentNullException(nameof(grant));

        int scoreDelta;
        int reputationDelta;
        int newScore;
        int oldReputation;
        int newReputation;
        lock (_gate)
        {
            if (record)
                _ledger.Record(grant);

            scoreDelta = grant.Rewards.TryGetValue(RewardTypeScore, out var sd) ? sd : 0;
            reputationDelta = grant.Rewards.TryGetValue(RewardTypeReputation, out var rd) ? rd : 0;

            if (scoreDelta != 0)
                _scoreTotal += scoreDelta;

            if (reputationDelta != 0)
            {
                oldReputation = _reputationByGuild.TryGetValue(grant.GuildId, out var current)
                    ? current
                    : MinReputation;
                newReputation = Clamp(oldReputation + reputationDelta);
                _reputationByGuild[grant.GuildId] = newReputation;
            }
            else
            {
                oldReputation = 0;
                newReputation = 0;
            }

            newScore = _scoreTotal;
        }

        if (scoreDelta != 0)
        {
            var scoreEvent = new ScoreChanged(newScore, scoreDelta);
            await PublishAsync(ScoreChanged.EventType, scoreEvent).ConfigureAwait(false);
        }

        if (reputationDelta != 0)
        {
            var repEvent = new ReputationChanged(
                GuildId: grant.GuildId,
                OldValue: oldReputation,
                NewValue: newReputation,
                Reason: grant.SourceType,
                ChangedAt: _time.UtcNowOffset);
            await PublishAsync(ReputationChanged.EventType, repEvent).ConfigureAwait(false);
        }
    }

    private Task PublishAsync(string type, object data)
    {
        var now = _time.UtcNowOffset;
        var evt = new DomainEvent(
            Type: type,
            Source: nameof(RewardLedgerService),
            Data: data,
            Timestamp: now.UtcDateTime,
            Id: _idGenerator.NewId());
        return _eventBus.PublishAsync(evt);
    }

    private static int Clamp(int value)
    {
        if (value < MinReputation)
            return MinReputation;
        if (value > MaxReputation)
            return MaxReputation;
        return value;
    }

    private string NormalizeEventId(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
            return _idGenerator.NewId();

        return eventId;
    }
}
