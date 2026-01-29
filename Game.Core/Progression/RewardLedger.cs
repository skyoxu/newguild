using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;

namespace Game.Core.Progression;

public sealed class RewardGrant
{
    public string GrantId { get; }
    public string GuildId { get; }
    public string SourceType { get; }
    public string SourceId { get; }
    public Dictionary<string, int> Rewards { get; }

    public RewardGrant(
        string grantId,
        string guildId,
        string sourceType,
        string sourceId,
        Dictionary<string, int> rewards
    )
    {
        GrantId = grantId;
        GuildId = guildId;
        SourceType = sourceType;
        SourceId = sourceId;
        Rewards = rewards is null
            ? new Dictionary<string, int>()
            : new Dictionary<string, int>(rewards);
    }
}

public sealed class RewardLedger
{
    private readonly List<RewardGrant> _grants = new();
    private readonly HashSet<string> _grantIds = new(StringComparer.Ordinal);

    public void Record(RewardGrant grant)
    {
        if (grant is null)
            throw new ArgumentNullException(nameof(grant));
        if (string.IsNullOrWhiteSpace(grant.GrantId))
            throw new ArgumentException("GrantId is required.", nameof(grant));
        if (string.IsNullOrWhiteSpace(grant.GuildId))
            throw new ArgumentException("GuildId is required.", nameof(grant));

        if (!_grantIds.Add(grant.GrantId))
            return;

        var rewards = grant.Rewards ?? new Dictionary<string, int>();
        var copy = new RewardGrant(
            grantId: grant.GrantId,
            guildId: grant.GuildId,
            sourceType: grant.SourceType,
            sourceId: grant.SourceId,
            rewards: new Dictionary<string, int>(rewards)
        );

        _grants.Add(copy);
    }

    public IReadOnlyList<RewardGrant> Replay()
    {
        return _grants
            .Select(grant => new RewardGrant(
                grantId: grant.GrantId,
                guildId: grant.GuildId,
                sourceType: grant.SourceType,
                sourceId: grant.SourceId,
                rewards: new Dictionary<string, int>(grant.Rewards)
            ))
            .ToList();
    }

    public string Save()
    {
        return JsonSerializer.Serialize(_grants);
    }

    public static RewardLedger Load(string data)
    {
        if (string.IsNullOrWhiteSpace(data))
            return new RewardLedger();

        List<RewardGrant> grants;
        try
        {
            grants = JsonSerializer.Deserialize<List<RewardGrant>>(data) ?? new List<RewardGrant>();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Reward ledger data is invalid.", ex);
        }
        var ledger = new RewardLedger();
        foreach (var grant in grants)
        {
            if (grant is null)
                throw new InvalidOperationException("Reward ledger data is invalid.");
            try
            {
                ledger.Record(grant);
            }
            catch (ArgumentNullException ex)
            {
                throw new InvalidOperationException("Reward ledger data is invalid.", ex);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException("Reward ledger data is invalid.", ex);
            }
        }
        return ledger;
    }
}
