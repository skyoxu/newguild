using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core.Domain;

/// <summary>
/// Guild aggregate root entity.
/// Manages guild lifecycle, membership, and role assignments.
/// Follows ADR-0018 (Game.Core layer: pure C# domain logic, zero Godot dependencies).
/// Thread-safe for concurrent member operations.
/// </summary>
public class Guild
{
    private readonly object _memberLock = new object();
    private readonly object _officerLock = new object();
    private readonly Dictionary<OfficerSlot, string> _officerAssignments = new();
    private static bool IsValidRole(GuildRole role) => role is GuildRole.Member or GuildRole.Admin;

    public string GuildId { get; private set; }
    public string CreatorId { get; private set; }
    public string Name { get; private set; }
    public List<GuildMember> Members { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyDictionary<OfficerSlot, string> OfficerAssignments => _officerAssignments;

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
            throw new ArgumentException("GuildId cannot be empty.", nameof(guildId));
        if (string.IsNullOrWhiteSpace(creatorId))
            throw new ArgumentException("CreatorId cannot be empty.", nameof(creatorId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

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
    /// Creates a guild without a creator for deterministic persistence tests.
    /// </summary>
    /// <param name="guildId">Unique guild identifier</param>
    /// <param name="name">Guild name</param>
    /// <param name="createdAt">Creation timestamp</param>
    /// <exception cref="ArgumentException">Thrown when any parameter is invalid</exception>
    public Guild(string guildId, string name, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(guildId))
            throw new ArgumentException("GuildId cannot be empty.", nameof(guildId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        if (createdAt == DateTimeOffset.MinValue)
            throw new ArgumentException("CreatedAt cannot be default value.", nameof(createdAt));

        GuildId = guildId;
        CreatorId = "system";
        Name = name;
        CreatedAt = createdAt;
        Members = new List<GuildMember>();
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
            throw new ArgumentException("GuildId cannot be empty.", nameof(guildId));
        if (string.IsNullOrWhiteSpace(creatorId))
            throw new ArgumentException("CreatorId cannot be empty.", nameof(creatorId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        if (members == null || members.Count == 0)
            throw new ArgumentException("Members cannot be empty.", nameof(members));

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
    /// Thread-safe operation.
    /// </summary>
    /// <param name="userId">User ID to add</param>
    /// <param name="role">Role to assign</param>
    /// <returns>True if member was added; false if user already exists in guild</returns>
    /// <exception cref="ArgumentException">Thrown when userId is null or whitespace</exception>
    public bool AddMember(string userId, GuildRole role)
    {
        if (!IsValidRole(role))
            return false;
        return AddMember(new GuildMember(userId, role));
    }

    /// <summary>
    /// Adds a new member to the guild.
    /// Thread-safe operation.
    /// </summary>
    /// <param name="member">Member to add</param>
    /// <returns>True if member was added; false if user already exists in guild</returns>
    public bool AddMember(GuildMember member)
    {
        if (member == null) throw new ArgumentNullException(nameof(member));
        if (!IsValidRole(member.Role))
            return false;

        lock (_memberLock)
        {
            if (Members.Any(m => m.UserId == member.UserId))
                return false;

            Members.Add(member);
            return true;
        }
    }

    /// <summary>
    /// Removes a member from the guild.
    /// Creator cannot be removed.
    /// Thread-safe operation.
    /// </summary>
    /// <param name="userId">User ID to remove</param>
    /// <returns>True if member was removed; false if user is creator or not found</returns>
    /// <exception cref="ArgumentException">Thrown when userId is null or whitespace</exception>
    public bool RemoveMember(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));

        // Creator cannot be removed
        if (userId == CreatorId)
            return false;

        lock (_memberLock)
        {
            var member = Members.FirstOrDefault(m => m.UserId == userId);
            if (member == null)
                return false;

            Members.Remove(member);
            return true;
        }
    }

    /// <summary>
    /// Changes a member's role.
    /// Thread-safe operation.
    /// </summary>
    /// <param name="userId">User ID whose role to change</param>
    /// <param name="newRole">New role to assign</param>
    /// <returns>True if role was changed; false if user not found</returns>
    /// <exception cref="ArgumentException">Thrown when userId is null or whitespace</exception>
    public bool ChangeRole(string userId, GuildRole newRole)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));

        if (!IsValidRole(newRole))
            return false;

        lock (_memberLock)
        {
            var memberIndex = Members.FindIndex(m => m.UserId == userId);
            if (memberIndex == -1)
                return false;

            // Record is immutable, so replace with new instance
            var existing = Members[memberIndex];
            Members[memberIndex] = new GuildMember(existing.UserId, existing.DisplayName, newRole);
            return true;
        }
    }

    /// <summary>
    /// Assigns a guild member to a specific officer slot.
    /// </summary>
    /// <param name="slot">Officer slot to assign</param>
    /// <param name="userId">Member user ID</param>
    /// <returns>True if assignment succeeded; false if slot is occupied or member not found</returns>
    public bool AssignOfficer(OfficerSlot slot, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));

        lock (_officerLock)
        {
            if (_officerAssignments.ContainsKey(slot))
                return false;

            if (!Members.Any(m => m.UserId == userId))
                return false;

            _officerAssignments[slot] = userId;
            return true;
        }
    }

    /// <summary>
    /// Revokes an existing officer assignment from a specific slot.
    /// </summary>
    /// <param name="slot">Officer slot to revoke</param>
    /// <param name="userId">User ID that was revoked from the slot</param>
    /// <returns>True if an assignment existed and was removed; false otherwise</returns>
    public bool TryRevokeOfficer(OfficerSlot slot, out string? userId)
    {
        lock (_officerLock)
        {
            if (!_officerAssignments.TryGetValue(slot, out var existing))
            {
                userId = null;
                return false;
            }

            _officerAssignments.Remove(slot);
            userId = existing;
            return true;
        }
    }

    /// <summary>
    /// Retrieves the user ID assigned to an officer slot.
    /// </summary>
    /// <param name="slot">Officer slot to query</param>
    /// <returns>User ID if assigned; null otherwise</returns>
    public string? GetOfficerAssignment(OfficerSlot slot)
    {
        lock (_officerLock)
        {
            return _officerAssignments.TryGetValue(slot, out var userId) ? userId : null;
        }
    }

    internal void RestoreOfficerAssignments(IReadOnlyDictionary<OfficerSlot, string> assignments)
    {
        if (assignments == null) throw new ArgumentNullException(nameof(assignments));
        lock (_officerLock)
        {
            _officerAssignments.Clear();
            foreach (var entry in assignments)
            {
                _officerAssignments[entry.Key] = entry.Value;
            }
        }
    }
}
