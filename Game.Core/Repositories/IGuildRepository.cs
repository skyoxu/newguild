using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Core.Domain;

namespace Game.Core.Repositories;

/// <summary>
/// Repository interface for Guild aggregate persistence.
/// Follows ADR-0018 (pure C# interface, zero Godot dependencies).
/// Implementations should use godot-sqlite adapter (ADR-0023).
/// </summary>
public interface IGuildRepository
{
    /// <summary>
    /// Creates a new guild in the repository.
    /// </summary>
    /// <param name="guild">Guild entity to create</param>
    /// <returns>Created guild with any repository-assigned values</returns>
    Task<Guild> CreateAsync(Guild guild);

    /// <summary>
    /// Retrieves a guild by its unique identifier.
    /// </summary>
    /// <param name="guildId">Guild identifier</param>
    /// <returns>Guild if found, null otherwise</returns>
    Task<Guild?> GetByIdAsync(string guildId);

    /// <summary>
    /// Updates an existing guild in the repository.
    /// </summary>
    /// <param name="guild">Guild entity with updated values</param>
    /// <returns>Updated guild</returns>
    Task<Guild> UpdateAsync(Guild guild);

    /// <summary>
    /// Deletes a guild from the repository.
    /// </summary>
    /// <param name="guildId">Guild identifier</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteAsync(string guildId);

    /// <summary>
    /// Retrieves all guilds in the repository.
    /// </summary>
    /// <returns>Read-only list of all guilds</returns>
    Task<IReadOnlyList<Guild>> GetAllAsync();

    /// <summary>
    /// Finds guilds where the specified user is a member.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>Read-only list of guilds containing the user</returns>
    Task<IReadOnlyList<Guild>> FindByMemberAsync(string userId);
}
