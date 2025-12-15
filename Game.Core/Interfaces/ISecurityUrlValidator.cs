using System.Collections.Generic;

namespace Game.Core.Interfaces;

/// <summary>
/// Port interface for URL security validation.
/// Enforces HTTPS-only scheme and domain whitelist policies per ADR-0019.
/// </summary>
/// <remarks>
/// This interface enables dependency inversion by allowing the Core layer
/// to depend on abstractions rather than concrete Godot implementations.
/// Implementations must:
/// - Enforce HTTPS-only scheme (reject http://, file://, javascript:, data:, etc.)
/// - Enforce ALLOWED_EXTERNAL_HOSTS whitelist
/// - Reject all URLs when whitelist is null/empty (prevents SSRF CWE-918)
/// - Log rejections to security audit with all 5 required fields
/// </remarks>
public interface ISecurityUrlValidator
{
    /// <summary>
    /// Validates that a URL is allowed based on security policy.
    /// </summary>
    /// <param name="url">URL to validate (must be HTTPS and in whitelist)</param>
    /// <returns>True if URL is allowed, false otherwise</returns>
    /// <remarks>
    /// Rejection criteria:
    /// - Non-HTTPS schemes (http, file, javascript, data, etc.)
    /// - Domains not in ALLOWED_EXTERNAL_HOSTS whitelist
    /// - Null or empty URLs
    /// - Any URL when whitelist is null/empty (SSRF prevention)
    /// </remarks>
    bool IsUrlAllowed(string url);

    /// <summary>
    /// Gets the list of allowed external hosts from whitelist.
    /// </summary>
    /// <value>
    /// Read-only list of allowed domain names. When null or empty,
    /// all external URLs must be rejected to prevent SSRF attacks.
    /// </value>
    IReadOnlyList<string>? AllowedHosts { get; }

    /// <summary>
    /// Validates URL and logs rejection to security audit if denied.
    /// </summary>
    /// <param name="url">URL to validate</param>
    /// <param name="caller">Caller context for audit logging (e.g., method name, component)</param>
    /// <returns>
    /// Tuple containing:
    /// - IsAllowed: True if URL passes validation
    /// - RejectionReason: Human-readable reason if rejected, null if allowed
    /// </returns>
    /// <remarks>
    /// When URL is rejected, this method must write to security-audit.jsonl with:
    /// - ts: ISO 8601 timestamp
    /// - action: "security.url.rejected" (ADR-0004 format)
    /// - reason: Specific rejection reason
    /// - target: The rejected URL
    /// - caller: Caller context from parameter
    /// </remarks>
    (bool IsAllowed, string? RejectionReason) ValidateAndAudit(string url, string caller);
}

