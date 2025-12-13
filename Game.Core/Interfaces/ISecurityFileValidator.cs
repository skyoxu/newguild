namespace Game.Core.Interfaces;

/// <summary>
/// Access mode for file operations.
/// </summary>
public enum FileAccessMode
{
    /// <summary>
    /// Read-only access (allowed for res:// and user://)
    /// </summary>
    Read,

    /// <summary>
    /// Write access (only allowed for user://)
    /// </summary>
    Write
}

/// <summary>
/// Core interface for file path security validation.
/// Implementations must enforce:
/// - res:// protocol: read-only access to game resources
/// - user:// protocol: read-write access to user data directory
/// - Path traversal prevention (CWE-22)
/// - Absolute path rejection
/// - Extension whitelist enforcement
/// - File size limits
/// </summary>
public interface ISecurityFileValidator
{
    /// <summary>
    /// Validates if a file path is allowed for the specified access mode.
    /// </summary>
    /// <param name="path">File path to validate (must start with res:// or user://)</param>
    /// <param name="mode">Requested access mode (Read or Write)</param>
    /// <returns>True if path is allowed, false otherwise</returns>
    bool IsPathAllowed(string path, FileAccessMode mode);

    /// <summary>
    /// Validates path and writes audit log on rejection.
    /// </summary>
    /// <param name="path">File path to validate</param>
    /// <param name="mode">Requested access mode</param>
    /// <param name="caller">Calling context for audit trail</param>
    /// <returns>Tuple of (IsAllowed, RejectionReason or null)</returns>
    (bool IsAllowed, string? RejectionReason) ValidateAndAudit(string path, FileAccessMode mode, string caller);

    /// <summary>
    /// Normalizes path to canonical form for validation.
    /// Converts to lowercase and unifies path separators.
    /// </summary>
    /// <param name="path">Raw file path</param>
    /// <returns>Normalized path string</returns>
    string NormalizePath(string path);
}
