using Game.Core.Domain;

namespace Game.Core.Ports;

/// <summary>
/// Port for loading resources with type-safe path validation.
/// Implementations must validate paths per ADR-0019 security baseline.
/// </summary>
public interface IResourceLoader
{
    /// <summary>
    /// Loads text content from a validated resource path.
    /// </summary>
    /// <param name="path">SafeResourcePath (res:// or user://)</param>
    /// <returns>Text content if successful, null otherwise</returns>
    string? LoadText(SafeResourcePath path);

    /// <summary>
    /// Loads binary content from a validated resource path.
    /// </summary>
    /// <param name="path">SafeResourcePath (res:// or user://)</param>
    /// <returns>Binary content if successful, null otherwise</returns>
    byte[]? LoadBytes(SafeResourcePath path);
}

