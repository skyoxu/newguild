using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Core.Domain;
using Game.Core.Repositories;

namespace Game.Core.Tests.Repositories;

/// <summary>
/// In-memory implementation of IGuildRepository for testing.
/// Follows ADR-0018 (pure C# implementation, zero Godot dependencies).
/// </summary>
public class InMemoryGuildRepository : IGuildRepository
{
    private readonly Dictionary<string, Guild> _guilds = new();

    public Task<Guild> CreateAsync(Guild guild)
    {
        _guilds[guild.GuildId] = guild;
        return Task.FromResult(guild);
    }

    public Task<Guild?> GetByIdAsync(string guildId)
    {
        _guilds.TryGetValue(guildId, out var guild);
        return Task.FromResult(guild);
    }

    public Task<Guild> UpdateAsync(Guild guild)
    {
        _guilds[guild.GuildId] = guild;
        return Task.FromResult(guild);
    }

    public Task<bool> DeleteAsync(string guildId)
    {
        return Task.FromResult(_guilds.Remove(guildId));
    }

    public Task<IReadOnlyList<Guild>> GetAllAsync()
    {
        var all = _guilds.Values.ToList();
        return Task.FromResult<IReadOnlyList<Guild>>(all);
    }

    public Task<IReadOnlyList<Guild>> FindByMemberAsync(string userId)
    {
        var guilds = _guilds.Values
            .Where(g => g.Members.Any(m => m.UserId == userId))
            .ToList();
        return Task.FromResult<IReadOnlyList<Guild>>(guilds);
    }
}
