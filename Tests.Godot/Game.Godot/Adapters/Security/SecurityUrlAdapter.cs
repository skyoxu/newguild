using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Game.Core.Interfaces;
using Godot;

namespace Game.Godot.Adapters.Security;

/// <summary>
/// Godot adapter implementing URL security validation with HTTPS-only whitelist enforcement.
/// Blocks dangerous schemes and enforces ALLOWED_EXTERNAL_HOSTS whitelist per ADR-0019.
/// </summary>
/// <remarks>
/// Security Guarantees (ADR-0019):
/// - Rejects ALL URLs when whitelist is null/empty (SSRF CWE-918 prevention)
/// - Enforces HTTPS-only scheme (blocks http://, file://, javascript:, data:, blob:)
/// - Domain whitelist enforcement via ALLOWED_EXTERNAL_HOSTS
/// - Audit logging to security-audit.jsonl with required fields
///
/// This adapter isolates Godot-specific security enforcement from Core business logic,
/// following ADR-0007 Ports and Adapters pattern.
/// </remarks>
public sealed partial class SecurityUrlAdapter : RefCounted, ISecurityUrlValidator
{
    private readonly IReadOnlyList<string>? _allowedHosts;
    private readonly string _auditLogPath;

    /// <summary>
    /// Parameterless constructor for Godot GDScript compatibility.
    /// Required for GDScript .new() instantiation.
    /// </summary>
    public SecurityUrlAdapter() : this(null, null) { }

    /// <summary>
    /// Initializes URL security adapter with optional whitelist configuration.
    /// </summary>
    /// <param name="allowedHosts">
    /// Whitelist of allowed domain names. When null or empty, ALL external URLs are rejected
    /// to prevent SSRF attacks (CWE-918, CVSS 8.6). Required for production use.
    /// </param>
    /// <param name="auditLogPath">
    /// Path to security audit JSONL file. Defaults to logs/ci/{date}/security-audit.jsonl.
    /// Creates parent directories if they don't exist.
    /// </param>
    public SecurityUrlAdapter(IReadOnlyList<string>? allowedHosts = null, string? auditLogPath = null)
    {
        _allowedHosts = allowedHosts;

        _auditLogPath = ResolveAuditLogPath(auditLogPath);

        // Ensure audit log directory exists
        var directory = Path.GetDirectoryName(_auditLogPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string ResolveAuditLogPath(string? auditLogPath)
    {
        // Default audit log path follows ADR-0019 logging convention.
        var path = string.IsNullOrWhiteSpace(auditLogPath)
            ? Path.Combine("logs", "ci", DateTime.UtcNow.ToString("yyyy-MM-dd"), "security-audit.jsonl")
            : auditLogPath;

        // Convert Godot virtual paths (user://, res://) to absolute filesystem paths for .NET IO APIs.
        if (path.StartsWith("user://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectSettings.GlobalizePath(path);
        }

        // For filesystem-relative paths, resolve under the project root (res://).
        var baseDir = ProjectSettings.GlobalizePath("res://");
        try
        {
            return Path.GetFullPath(path, baseDir);
        }
        catch
        {
            return Path.GetFullPath(path);
        }
    }

    /// <summary>
    /// Explicit interface implementation - returns IReadOnlyList to satisfy interface contract.
    /// GDScript code should access the public AllowedHosts property instead.
    /// </summary>
    IReadOnlyList<string>? ISecurityUrlValidator.AllowedHosts => _allowedHosts;

    /// <summary>
    /// Public property for GDScript access - returns Godot.Collections.Array.
    /// Gets the list of allowed external hosts from whitelist.
    /// </summary>
    /// <value>
    /// Godot Array of allowed domain names. When null,
    /// all external URLs must be rejected to prevent SSRF attacks.
    /// </value>
    public global::Godot.Collections.Array? AllowedHosts
    {
        get
        {
            if (_allowedHosts == null)
                return null;

            var result = new global::Godot.Collections.Array();
            foreach (var host in _allowedHosts)
            {
                result.Add(host);
            }
            return result;
        }
    }

    /// <inheritdoc />
    public bool IsUrlAllowed(string url)
    {
        // Validate without audit logging (simple bool check)
        return ValidateCore(url, out _);
    }

    /// <summary>
    /// Explicit interface implementation - returns tuple to satisfy interface contract.
    /// </summary>
    (bool IsAllowed, string? RejectionReason) ISecurityUrlValidator.ValidateAndAudit(string url, string caller)
    {
        var result = ValidateAndAudit(url, caller);
        return (result.IsAllowed, result.RejectionReason);
    }

    /// <summary>
    /// Public method for GDScript access - returns UrlValidationResult that supports indexing.
    /// Validates URL and logs rejection to security audit if denied.
    /// </summary>
    /// <param name="url">URL to validate</param>
    /// <param name="caller">Caller context for audit logging</param>
    /// <returns>Validation result with IsAllowed and RejectionReason accessible via indexing</returns>
    public UrlValidationResult ValidateAndAudit(string url, string caller)
    {
        // Perform validation with detailed reason
        bool isAllowed = ValidateCore(url, out string? rejectionReason);

        // Audit log rejection per ADR-0004/ADR-0019 requirements
        if (!isAllowed && rejectionReason != null)
        {
            WriteAuditLog(url, rejectionReason, caller);
        }

        return new UrlValidationResult(isAllowed, rejectionReason);
    }

    /// <summary>
    /// Core validation logic implementing security checks.
    /// </summary>
    /// <param name="url">URL to validate</param>
    /// <param name="rejectionReason">Human-readable reason if rejected</param>
    /// <returns>True if URL passes all security checks, false otherwise</returns>
    private bool ValidateCore(string url, out string? rejectionReason)
    {
        rejectionReason = null;

        // Check 1: Null/empty URL
        if (string.IsNullOrWhiteSpace(url))
        {
            rejectionReason = "URL is null or empty";
            return false;
        }

        // Check 2: SSRF Prevention - Reject when whitelist not configured (CWE-918)
        // This is the PRIMARY defense against Server-Side Request Forgery attacks
        if (_allowedHosts == null || _allowedHosts.Count == 0)
        {
            rejectionReason = "External URL whitelist not configured (SSRF prevention)";
            return false;
        }

        // Check 3: Parse URI
        Uri uri;
        try
        {
            uri = new Uri(url, UriKind.Absolute);
        }
        catch (UriFormatException)
        {
            rejectionReason = "Invalid URI format";
            return false;
        }

        // Check 4: Block dangerous schemes (ADR-0019)
        // Prevents XSS, file system access, and code execution attacks
        var dangerousSchemes = new[] { "javascript", "data", "blob", "file" };
        if (dangerousSchemes.Contains(uri.Scheme.ToLowerInvariant()))
        {
            rejectionReason = $"Dangerous scheme blocked: {uri.Scheme}";
            return false;
        }

        // Check 5: Enforce HTTPS-only (no HTTP downgrade attacks)
        if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            rejectionReason = $"Non-HTTPS scheme rejected: {uri.Scheme} (only HTTPS allowed)";
            return false;
        }

        // Check 6: Domain whitelist enforcement
        if (!_allowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
        {
            rejectionReason = $"Domain not in whitelist: {uri.Host}";
            return false;
        }

        // All checks passed
        return true;
    }

    /// <summary>
    /// Writes rejection event to security audit JSONL file per ADR-0004/ADR-0019.
    /// </summary>
    /// <param name="url">Rejected URL</param>
    /// <param name="reason">Rejection reason</param>
    /// <param name="caller">Caller context for traceability</param>
    private void WriteAuditLog(string url, string reason, string caller)
    {
        try
        {
            // Construct audit entry with all 5 required fields (ADR-0019)
            var auditEntry = new
            {
                ts = DateTime.UtcNow.ToString("o"),  // ISO 8601 timestamp
                action = "security.url.rejected",     // ADR-0004 dot-separated format
                reason = reason,
                target = url,
                caller = caller
            };

            // Serialize to single-line JSON (JSONL format)
            string jsonLine = JsonSerializer.Serialize(auditEntry) + System.Environment.NewLine;

            // Append to audit log (thread-safe file locking)
            File.AppendAllText(_auditLogPath, jsonLine);
        }
        catch (Exception ex)
        {
            // Audit logging failure should not break security validation
            // Log to stderr for CI visibility but don't throw
            Console.Error.WriteLine($"[SecurityUrlAdapter] Failed to write audit log: {ex.Message}");
        }
    }
}
