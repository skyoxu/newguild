using Godot;

namespace Game.Godot.Adapters.Security;

/// <summary>
/// Result container for process validation that supports cross-language access (C# and GDScript).
/// Provides both tuple-like indexing and property access patterns.
/// </summary>
public partial class ProcessValidationResult : RefCounted
{
    /// <summary>
    /// Whether the process execution is allowed
    /// </summary>
    public bool IsAllowed { get; }

    /// <summary>
    /// Reason for rejection if IsAllowed is false, empty string otherwise.
    /// Note: C# null is converted to empty string when crossing C#-GDScript boundary.
    /// </summary>
    public string RejectionReason { get; }

    public ProcessValidationResult(bool isAllowed, string? rejectionReason)
    {
        IsAllowed = isAllowed;
        // Convert null to empty string for GDScript compatibility
        // Godot's C#-GDScript interop converts string? null to empty string anyway
        RejectionReason = rejectionReason ?? string.Empty;
    }

    /// <summary>
    /// Indexer for tuple-like access (not exported to GDScript).
    /// C# code can use result[0] and result[1].
    /// </summary>
    public object? this[int index]
    {
        get
        {
            return index switch
            {
                0 => IsAllowed,
                1 => RejectionReason,
                _ => throw new System.ArgumentOutOfRangeException(nameof(index), "Index must be 0 or 1")
            };
        }
    }

    /// <summary>
    /// Explicit Get method for GDScript access (C# indexers don't export to Godot).
    /// GDScript code should use result.Get(0) and result.Get(1).
    /// </summary>
    public object? Get(int index)
    {
        return this[index];
    }
}
