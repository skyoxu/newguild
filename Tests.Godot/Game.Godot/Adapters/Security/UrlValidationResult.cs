using Godot;
using System;

namespace Game.Godot.Adapters.Security;

/// <summary>
/// Validation result for URL security checks.
/// Supports both property access and array-style indexing for GDScript interop.
/// </summary>
/// <remarks>
/// Design Rationale (Godot C# Interop):
/// - C# tuples cannot cross language boundaries to GDScript
/// - GDScript tests expect array-style access: result[0] and result[1]
/// - This class provides indexer to enable array-like access from GDScript
/// - Inherits RefCounted to be callable from GDScript
/// </remarks>
public partial class UrlValidationResult : RefCounted
{
    /// <summary>
    /// Whether the URL is allowed based on security rules.
    /// </summary>
    public bool IsAllowed { get; }

    /// <summary>
    /// Rejection reason if URL was not allowed. Null if allowed.
    /// </summary>
    public string? RejectionReason { get; }

    /// <summary>
    /// Creates a new validation result.
    /// </summary>
    /// <param name="isAllowed">Whether URL is allowed</param>
    /// <param name="rejectionReason">Rejection reason if not allowed</param>
    public UrlValidationResult(bool isAllowed, string? rejectionReason)
    {
        IsAllowed = isAllowed;
        RejectionReason = rejectionReason;
    }

    /// <summary>
    /// Indexer to support GDScript array-style access: result[0], result[1]
    /// NOTE: C# indexers don't expose to GDScript. Use Get(index) method instead.
    /// </summary>
    /// <param name="index">0 for IsAllowed (bool), 1 for RejectionReason (string?)</param>
    /// <returns>Value at the specified index</returns>
    /// <exception cref="ArgumentOutOfRangeException">Index must be 0 or 1</exception>
    public Variant this[int index]
    {
        get
        {
            return index switch
            {
                0 => Variant.From(IsAllowed),
                1 => RejectionReason != null ? Variant.From(RejectionReason) : default,
                _ => throw new ArgumentOutOfRangeException(nameof(index), "Index must be 0 (IsAllowed) or 1 (RejectionReason)")
            };
        }
    }

    /// <summary>
    /// Explicit method for GDScript to access indexed values.
    /// C# indexers don't generate Godot bindings, so this method provides GDScript-callable access.
    /// </summary>
    /// <param name="index">0 for IsAllowed (bool), 1 for RejectionReason (string?)</param>
    /// <returns>Value at the specified index</returns>
    /// <exception cref="ArgumentOutOfRangeException">Index must be 0 or 1</exception>
    public Variant Get(int index)
    {
        return this[index]; // Delegate to indexer implementation
    }
}
