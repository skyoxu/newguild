using Godot;
using System;

namespace Game.Godot.Adapters.Security;

/// <summary>
/// Result class for file validation that supports cross-language access.
/// Bridges C# tuple returns and GDScript indexing.
/// Provides structured validation results with reason details.
/// </summary>
public partial class FileValidationResult : RefCounted
{
    /// <summary>
    /// Whether the file path is allowed for the requested access mode.
    /// </summary>
    public bool IsAllowed { get; }

    /// <summary>
    /// Detailed rejection reason if validation failed, null if allowed.
    /// </summary>
    public string? RejectionReason { get; }

    /// <summary>
    /// Constructs a validation result from tuple values.
    /// </summary>
    /// <param name="isAllowed">Whether the path is allowed</param>
    /// <param name="rejectionReason">Reason for rejection (null if allowed)</param>
    public FileValidationResult(bool isAllowed, string? rejectionReason)
    {
        IsAllowed = isAllowed;
        RejectionReason = rejectionReason;
    }

    /// <summary>
    /// Indexer for C# tuple-like access.
    /// Index 0 = IsAllowed (bool)
    /// Index 1 = RejectionReason (string?)
    /// </summary>
    public object? this[int index]
    {
        get
        {
            return index switch
            {
                0 => IsAllowed,
                1 => RejectionReason,
                _ => throw new ArgumentOutOfRangeException(nameof(index), $"Valid indices are 0 (IsAllowed) or 1 (RejectionReason), got {index}")
            };
        }
    }

    /// <summary>
    /// Explicit method for GDScript access (indexers not exported to GDScript).
    /// Returns value at specified index: 0 = IsAllowed, 1 = RejectionReason.
    /// </summary>
    /// <param name="index">Index (0 or 1)</param>
    /// <returns>Validation result component</returns>
    public object? Get(int index)
    {
        return this[index];
    }
}
