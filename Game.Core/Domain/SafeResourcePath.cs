using System;

namespace Game.Core.Domain;

/// <summary>
/// Value object representing a validated Godot resource path.
/// Ensures type-safety and prevents path traversal attacks at compile time.
/// Per ADR-0019: Only res:// (read-only) and user:// (read-write) paths allowed.
/// </summary>
public sealed record SafeResourcePath
{
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
    /// Validates prefix (res:// or user://) and rejects path traversal.
    /// </summary>
    /// <param name="path">Path string to validate</param>
    /// <returns>SafeResourcePath if valid, null otherwise</returns>
    public static SafeResourcePath? FromString(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var trimmed = path.Trim();

        // Check for res:// prefix (read-only resources)
        if (trimmed.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
        {
            if (ContainsPathTraversal(trimmed))
                return null;
            return new SafeResourcePath(trimmed, PathType.ReadOnly);
        }

        // Check for user:// prefix (read-write user data)
        if (trimmed.StartsWith("user://", StringComparison.OrdinalIgnoreCase))
        {
            if (ContainsPathTraversal(trimmed))
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
    /// Checks if path contains path traversal sequences (../).
    /// </summary>
    private static bool ContainsPathTraversal(string path)
        => path.Contains("../");

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
