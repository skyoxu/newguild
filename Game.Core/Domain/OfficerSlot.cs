namespace Game.Core.Domain;

/// <summary>
/// Officer slots available for guild leadership assignments.
/// Follows ADR-0018 (pure C# domain logic, zero Godot dependencies).
/// </summary>
public enum OfficerSlot
{
    /// <summary>
    /// Leads tactical decisions and field command.
    /// </summary>
    Commander = 0,

    /// <summary>
    /// Oversees treasury and finances.
    /// </summary>
    Treasurer = 1,
}
