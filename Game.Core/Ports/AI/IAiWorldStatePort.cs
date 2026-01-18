using System;
using System.Collections.Generic;
using Game.Core.Domain.Turn;

namespace Game.Core.Ports.AI;

public interface IAiWorldStatePort
{
    AiWorldSnapshot GetSnapshot(SaveIdValue saveId, int week);
    void Apply(SaveIdValue saveId, int week, AiWorldDelta delta);
}

public sealed record AiWorldGuild(
    string GuildId,
    int CurrentMembers,
    int MaxMembers
);

public sealed record AiWorldMember(
    string MemberId,
    string? CurrentGuildId
);

public sealed record AiAffinityDelta(
    string MemberId,
    string GuildId,
    int Delta
);

public sealed record AiMemberJoinedGuild(
    string MemberId,
    string GuildId
);

public sealed record AiWorldDelta(
    IReadOnlyList<AiAffinityDelta> AffinityDeltas,
    IReadOnlyList<AiMemberJoinedGuild> MemberJoins
)
{
    public static AiWorldDelta Empty { get; } = new(
        AffinityDeltas: Array.Empty<AiAffinityDelta>(),
        MemberJoins: Array.Empty<AiMemberJoinedGuild>()
    );

    public bool IsEmpty => AffinityDeltas.Count == 0 && MemberJoins.Count == 0;
}

public sealed record AiWorldSnapshot(
    IReadOnlyDictionary<string, AiWorldGuild> Guilds,
    IReadOnlyDictionary<string, AiWorldMember> Members,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> AffinityByMember
)
{
    public static AiWorldSnapshot Empty { get; } = new(
        Guilds: new Dictionary<string, AiWorldGuild>(StringComparer.Ordinal),
        Members: new Dictionary<string, AiWorldMember>(StringComparer.Ordinal),
        AffinityByMember: new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.Ordinal)
    );
}
