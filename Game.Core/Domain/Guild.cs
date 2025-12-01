using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core.Domain;

/// <summary>
/// Guild aggregate root entity.
/// Manages guild lifecycle, membership, and role assignments.
/// Follows ADR-0018 (Game.Core layer: pure C# domain logic, zero Godot dependencies).
/// </summary>
public class Guild
{
    public string GuildId { get; private set; }
    public string CreatorId { get; private set; }
    public string Name { get; private set; }
    public List<GuildMember> Members { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Private parameterless constructor for database reconstruction.
    /// Use ReconstructFromDatabase() static factory method instead.
    /// </summary>
    private Guild()
    {
        // Empty constructor for object initializer in ReconstructFromDatabase
        GuildId = string.Empty;
        CreatorId = string.Empty;
        Name = string.Empty;
        Members = new List<GuildMember>();
        CreatedAt = DateTimeOffset.MinValue;
    }

    /// <summary>
    /// Creates a new guild with the creator as the first admin member.
    /// </summary>
    /// <param name="guildId">Unique guild identifier</param>
    /// <param name="creatorId">User ID of the guild creator</param>
    /// <param name="name">Guild name</param>
    /// <exception cref="ArgumentException">Thrown when any parameter is null or whitespace</exception>
    public Guild(string guildId, string creatorId, string name)
    {
        if (string.IsNullOrWhiteSpace(guildId))
            throw new ArgumentException("公会ID不能为空", nameof(guildId));
        if (string.IsNullOrWhiteSpace(creatorId))
            throw new ArgumentException("创建者ID不能为空", nameof(creatorId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("公会名称不能为空", nameof(name));

        GuildId = guildId;
        CreatorId = creatorId;
        Name = name;
        CreatedAt = DateTimeOffset.UtcNow;
        Members = new List<GuildMember>
        {
            new GuildMember(creatorId, GuildRole.Admin)
        };
    }

    /// <summary>
    /// Reconstructs a Guild from database data without using reflection.
    /// Used by repositories for hydrating entities from storage.
    /// </summary>
    /// <param name="guildId">Unique guild identifier</param>
    /// <param name="creatorId">User ID of the guild creator</param>
    /// <param name="name">Guild name</param>
    /// <param name="createdAt">Original creation timestamp from database</param>
    /// <param name="members">Full member list from database</param>
    /// <returns>Reconstructed Guild entity</returns>
    /// <exception cref="ArgumentException">Thrown when any parameter is invalid</exception>
    public static Guild ReconstructFromDatabase(
        string guildId,
        string creatorId,
        string name,
        DateTimeOffset createdAt,
        IReadOnlyList<GuildMember> members)
    {
        if (string.IsNullOrWhiteSpace(guildId))
            throw new ArgumentException("公会ID不能为空", nameof(guildId));
        if (string.IsNullOrWhiteSpace(creatorId))
            throw new ArgumentException("创建者ID不能为空", nameof(creatorId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("公会名称不能为空", nameof(name));
        if (members == null || members.Count == 0)
            throw new ArgumentException("成员列表不能为空", nameof(members));

        return new Guild
        {
            GuildId = guildId,
            CreatorId = creatorId,
            Name = name,
            CreatedAt = createdAt,
            Members = new List<GuildMember>(members)
        };
    }

    /// <summary>
    /// Adds a new member to the guild.
    /// </summary>
    /// <param name="userId">User ID to add</param>
    /// <param name="role">Role to assign</param>
    /// <returns>True if member was added; false if user already exists in guild</returns>
    /// <exception cref="ArgumentException">Thrown when userId is null or whitespace</exception>
    public bool AddMember(string userId, GuildRole role)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("用户ID不能为空", nameof(userId));

        if (Members.Any(m => m.UserId == userId))
            return false;

        Members.Add(new GuildMember(userId, role));
        return true;
    }

    /// <summary>
    /// Removes a member from the guild.
    /// Creator cannot be removed.
    /// </summary>
    /// <param name="userId">User ID to remove</param>
    /// <returns>True if member was removed; false if user is creator or not found</returns>
    /// <exception cref="ArgumentException">Thrown when userId is null or whitespace</exception>
    public bool RemoveMember(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("用户ID不能为空", nameof(userId));

        // Creator cannot be removed
        if (userId == CreatorId)
            return false;

        var member = Members.FirstOrDefault(m => m.UserId == userId);
        if (member == null)
            return false;

        Members.Remove(member);
        return true;
    }

    /// <summary>
    /// Changes a member's role.
    /// </summary>
    /// <param name="userId">User ID whose role to change</param>
    /// <param name="newRole">New role to assign</param>
    /// <returns>True if role was changed; false if user not found</returns>
    /// <exception cref="ArgumentException">Thrown when userId is null or whitespace</exception>
    public bool ChangeRole(string userId, GuildRole newRole)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("用户ID不能为空", nameof(userId));

        var memberIndex = Members.FindIndex(m => m.UserId == userId);
        if (memberIndex == -1)
            return false;

        // Record is immutable, so replace with new instance
        Members[memberIndex] = new GuildMember(userId, newRole);
        return true;
    }
}
