using System;

namespace Game.Core.Domain;

/// <summary>
/// Value object representing a guild member.
/// Follows ADR-0018 (pure C# domain logic, zero Godot dependencies).
/// </summary>
public record GuildMember
{
    public string UserId { get; init; }
    public GuildRole Role { get; init; }

    public GuildMember(string userId, GuildRole role)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("用户ID不能为空", nameof(userId));

        UserId = userId;
        Role = role;
    }

    /// <summary>
    /// Value equality based on UserId only (role changes don't affect identity).
    /// </summary>
    public virtual bool Equals(GuildMember? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return UserId == other.UserId;
    }

    public override int GetHashCode()
    {
        return UserId.GetHashCode();
    }
}
