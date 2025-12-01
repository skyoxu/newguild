namespace Game.Core.Domain;

/// <summary>
/// Guild member role enumeration.
/// Follows ADR-0018 (domain layer without Godot dependencies).
/// </summary>
public enum GuildRole
{
    /// <summary>
    /// Regular guild member with basic permissions.
    /// </summary>
    Member,

    /// <summary>
    /// Guild administrator with elevated permissions.
    /// </summary>
    Admin
}
