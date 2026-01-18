using System;
using System.Collections.Generic;
using Game.Core.Domain.Turn;
using Game.Core.Ports.AI;

namespace Game.Core.Services;

public sealed class InMemoryAiWorldStatePort : IAiWorldStatePort
{
    private readonly object _gate = new();
    private readonly Dictionary<string, AiWorldGuild> _guilds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AiWorldMember> _members = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, int>> _affinityByMember = new(StringComparer.Ordinal);
    private SaveIdValue? _saveId;

    public void Seed(SaveIdValue saveId, int week, AiWorldSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(saveId);
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (_gate)
        {
            _saveId = saveId;
            _guilds.Clear();
            _members.Clear();
            _affinityByMember.Clear();

            foreach (var (id, g) in snapshot.Guilds)
                _guilds[id] = g;
            foreach (var (id, m) in snapshot.Members)
                _members[id] = m;
            foreach (var (memberId, map) in snapshot.AffinityByMember)
            {
                var copy = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var (guildId, score) in map)
                    copy[guildId] = score;
                _affinityByMember[memberId] = copy;
            }
        }
    }

    public AiWorldSnapshot GetSnapshot(SaveIdValue saveId, int week)
    {
        ArgumentNullException.ThrowIfNull(saveId);

        lock (_gate)
        {
            if (_saveId is not null && _saveId != saveId)
                return AiWorldSnapshot.Empty;

            var guilds = new Dictionary<string, AiWorldGuild>(_guilds, StringComparer.Ordinal);
            var members = new Dictionary<string, AiWorldMember>(_members, StringComparer.Ordinal);

            var affinity = new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.Ordinal);
            foreach (var (memberId, map) in _affinityByMember)
                affinity[memberId] = new Dictionary<string, int>(map, StringComparer.Ordinal);

            return new AiWorldSnapshot(guilds, members, affinity);
        }
    }

    public void Apply(SaveIdValue saveId, int week, AiWorldDelta delta)
    {
        ArgumentNullException.ThrowIfNull(saveId);
        ArgumentNullException.ThrowIfNull(delta);
        if (delta.IsEmpty)
            return;

        lock (_gate)
        {
            _saveId ??= saveId;
            if (_saveId != saveId)
                return;

            foreach (var d in delta.AffinityDeltas)
            {
                if (!_affinityByMember.TryGetValue(d.MemberId, out var map))
                {
                    map = new Dictionary<string, int>(StringComparer.Ordinal);
                    _affinityByMember[d.MemberId] = map;
                }

                map.TryGetValue(d.GuildId, out var current);
                map[d.GuildId] = current + d.Delta;
            }

            foreach (var join in delta.MemberJoins)
            {
                if (!_members.TryGetValue(join.MemberId, out var member))
                    continue;

                if (!_guilds.TryGetValue(join.GuildId, out var guild))
                    continue;

                if (guild.CurrentMembers >= guild.MaxMembers)
                    continue;

                if (!string.IsNullOrWhiteSpace(member.CurrentGuildId))
                    continue;

                _members[join.MemberId] = member with { CurrentGuildId = join.GuildId };
                _guilds[join.GuildId] = guild with { CurrentMembers = guild.CurrentMembers + 1 };
            }
        }
    }
}

