using System;

namespace Game.Core.Domain;

/// <summary>
/// Value object representing a validated Godot resource path.
/// Ensures type-safety and prevents path traversal attacks at compile time.
/// Per ADR-0019: Only res:// (read-only) and user:// (read-write) paths allowed.
/// Extension whitelist and path traversal prevention implemented.
/// </summary>
public sealed record SafeResourcePath
{
    /// <summary>
    /// Maximum allowed path length (Windows MAX_PATH compatibility: 260 characters).
    /// Prevents buffer overflow and excessive filesystem operations.
    /// </summary>
    private const int MaxPathLength = 260;

    /// <summary>
    /// Allowed file extensions for game data files per ADR-0019.
    /// Extensions for configuration, save files, and data persistence.
    /// </summary>
    private static readonly string[] AllowedExtensions =
    {
        ".json",   // Configuration and save data
        ".txt",    // Text files and logs
        ".dat",    // Binary data files
        ".save",   // Save game files
        ".cfg",    // Configuration files
        ".db",     // SQLite database file
        ".sqlite", // SQLite database file
        ".sqlite3",// SQLite database file
        ".xml",    // Structured data
        ".ini"     // Legacy configuration
    };

    /// <summary>
    /// The validated path string (res:// or user://).
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Path type (ReadOnly for res://, ReadWrite for user://).
    /// </summary>
    public PathType Type { get; }

    private SafeResourcePath(string path, PathType type)
    {
        Value = path;
        Type = type;
    }

    /// <summary>
    /// Creates a SafeResourcePath from a string path.
    /// Validates prefix (res:// or user://), rejects path traversal, and enforces extension whitelist.
    /// </summary>
    /// <param name="path">Path string to validate</param>
    /// <returns>SafeResourcePath if valid, null otherwise</returns>
    public static SafeResourcePath? FromString(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var trimmed = path.Trim();
        trimmed = trimmed.Replace('\\', '/');

        // Normalize common Windows-typed variants ("user:\", "user:/") to Godot style ("user://").
        if (trimmed.StartsWith("user:/", StringComparison.OrdinalIgnoreCase) && !trimmed.StartsWith("user://", StringComparison.OrdinalIgnoreCase))
            trimmed = "user://" + trimmed.Substring("user:/".Length);
        if (trimmed.StartsWith("res:/", StringComparison.OrdinalIgnoreCase) && !trimmed.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
            trimmed = "res://" + trimmed.Substring("res:/".Length);

        // Reject excessively long paths (Windows MAX_PATH compatibility)
        if (trimmed.Length > MaxPathLength)
            return null;

        // Check for res:// prefix (read-only resources)
        if (trimmed.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
        {
            if (ContainsPathTraversal(trimmed))
                return null;
            // No extension whitelist for res:// - developer-controlled resources can be any type (.png, .ogg, .tscn, etc.)
            return new SafeResourcePath(trimmed, PathType.ReadOnly);
        }

        // Check for user:// prefix (read-write user data)
        if (trimmed.StartsWith("user://", StringComparison.OrdinalIgnoreCase))
        {
            if (ContainsPathTraversal(trimmed))
                return null;
            if (!HasAllowedExtension(trimmed))
                return null;
            return new SafeResourcePath(trimmed, PathType.ReadWrite);
        }

        // Reject all other paths (absolute paths, relative paths, etc.)
        return null;
    }

    /// <summary>
    /// Creates a read-only resource path (res://).
    /// </summary>
    public static SafeResourcePath? ResPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        var path = relativePath.StartsWith("res://")
            ? relativePath
            : $"res://{relativePath.TrimStart('/')}";

        return FromString(path);
    }

    /// <summary>
    /// Creates a read-write user data path (user://).
    /// </summary>
    public static SafeResourcePath? UserPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        var path = relativePath.StartsWith("user://")
            ? relativePath
            : $"user://{relativePath.TrimStart('/')}";

        return FromString(path);
    }

    /// <summary>
    /// Checks if path contains path traversal sequences.
    /// Detects: ../ (forward), ..\ (backslash), and URL-encoded variants.
    /// Implements OWASP Path Traversal Prevention best practices.
    /// </summary>
    private static bool ContainsPathTraversal(string path)
    {
        // Normalize to lowercase for case-insensitive comparison
        var normalized = path.ToLowerInvariant();

        // Check for direct path traversal patterns (both forward and back slashes)
        if (normalized.Contains("../") || normalized.Contains("..\\"))
            return true;

        // Check for URL-encoded variants (single encoding)
        // %2e = dot, %2f = forward slash, %5c = backslash
        // Uses System.Net.WebUtility (available in .NET Standard 2.0+)
        var decoded = System.Net.WebUtility.UrlDecode(normalized);
        if (decoded != normalized && (decoded.Contains("../") || decoded.Contains("..\\")))
            return true;

        // Check for double-encoded variants (defense in depth)
        // Prevents %252e%252e bypass attempts
        var doubleDecoded = System.Net.WebUtility.UrlDecode(decoded);
        if (doubleDecoded != decoded && (doubleDecoded.Contains("../") || doubleDecoded.Contains("..\\")))
            return true;

        return false;
    }

    /// <summary>
    /// Checks if path has an allowed file extension.
    /// Case-insensitive matching per OWASP security best practices.
    /// </summary>
    private static bool HasAllowedExtension(string path)
    {
        // Extract extension from path (after last dot)
        var lastDotIndex = path.LastIndexOf('.');
        if (lastDotIndex < 0)
            return false; // No extension - reject

        var extension = path.Substring(lastDotIndex).ToLowerInvariant();

        // Check against whitelist (case-insensitive)
        foreach (var allowed in AllowedExtensions)
        {
            if (extension.Equals(allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false; // Extension not in whitelist
    }

    /// <summary>
    /// Implicit conversion to string for easy usage.
    /// </summary>
    public static implicit operator string(SafeResourcePath path)
        => path.Value;

    public override string ToString() => Value;
}

/// <summary>
/// Path type enum for SafeResourcePath.
/// </summary>
public enum PathType
{
    /// <summary>
    /// Read-only resource path (res://)
    /// </summary>
    ReadOnly,

    /// <summary>
    /// Read-write user data path (user://)
    /// </summary>
    ReadWrite
}
