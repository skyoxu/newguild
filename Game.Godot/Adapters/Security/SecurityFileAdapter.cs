using Game.Core.Interfaces;
using Game.Core.Services;
using Game.Core.Contracts;
using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Game.Godot.Adapters.Security;

/// <summary>
/// Adapter layer implementation of file path security validation.
/// Enforces res:// (read-only) and user:// (read-write) protocol restrictions.
/// Prevents path traversal (CWE-22), absolute paths, and enforces extension whitelist.
/// </summary>
public partial class SecurityFileAdapter : RefCounted, ISecurityFileValidator
{
    private readonly IEventBus _bus;
    private readonly string[]? _allowedExtensions;
    private readonly long _maxFileSize;
    private readonly string? _auditLogPath;

    private static readonly string[] PathTraversalPatterns = new[]
    {
        "..",
        "%2e%2e",
        "%252e%252e"
    };

    private static readonly string[] DefaultAllowedExtensions = new[]
    {
        ".txt",
        ".json",
        ".cfg",
        ".dat",
        ".sav"
    };

    private const long DefaultMaxFileSize = 10 * 1024 * 1024; // 10MB

    public SecurityFileAdapter(InMemoryEventBus bus, string? auditLogPath = null)
        : this(bus, DefaultAllowedExtensions, DefaultMaxFileSize, auditLogPath)
    {
    }

    public SecurityFileAdapter(
        InMemoryEventBus bus,
        string[] allowedExtensions,
        long maxFileSize = DefaultMaxFileSize,
        string? auditLogPath = null)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _allowedExtensions = allowedExtensions ?? throw new ArgumentNullException(nameof(allowedExtensions));
        _maxFileSize = maxFileSize;

        // Convert Godot virtual path (user://) to absolute filesystem path for .NET File API
        if (!string.IsNullOrWhiteSpace(auditLogPath))
        {
            _auditLogPath = ProjectSettings.GlobalizePath(auditLogPath);
        }
        else
        {
            _auditLogPath = auditLogPath;
        }
    }

    /// <summary>
    /// Explicit interface implementation - satisfies Core layer contract.
    /// </summary>
    bool ISecurityFileValidator.IsPathAllowed(string path, FileAccessMode mode)
    {
        return IsPathAllowedCore(path, mode);
    }

    /// <summary>
    /// Public method for GDScript access.
    /// </summary>
    public bool IsPathAllowed(string path, int mode)
    {
        return IsPathAllowedCore(path, (FileAccessMode)mode);
    }

    /// <summary>
    /// Explicit interface implementation - satisfies Core layer contract.
    /// </summary>
    (bool IsAllowed, string? RejectionReason) ISecurityFileValidator.ValidateAndAudit(
        string path,
        FileAccessMode mode,
        string caller)
    {
        return ValidateAndAuditCore(path, mode, caller);
    }

    /// <summary>
    /// Public method for GDScript access.
    /// Returns FileValidationResult for cross-language compatibility.
    /// </summary>
    public FileValidationResult ValidateAndAudit(string path, int mode, string caller)
    {
        var (isAllowed, rejectionReason) = ValidateAndAuditCore(path, (FileAccessMode)mode, caller);
        return new FileValidationResult(isAllowed, rejectionReason);
    }

    /// <summary>
    /// Explicit interface implementation - satisfies Core layer contract.
    /// </summary>
    string ISecurityFileValidator.NormalizePath(string path)
    {
        return NormalizePath(path);
    }

    /// <summary>
    /// Public method for GDScript access.
    /// </summary>
    public new string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        // Convert to lowercase for case-insensitive comparison
        var normalized = path.ToLowerInvariant();

        // Unify path separators to forward slash
        normalized = normalized.Replace('\\', '/');

        // URL decode to catch encoded traversal attempts
        normalized = System.Web.HttpUtility.UrlDecode(normalized);

        return normalized;
    }

    private bool IsPathAllowedCore(string path, FileAccessMode mode)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var normalized = NormalizePath(path);

        // Check for path traversal patterns
        if (ContainsPathTraversal(normalized))
            return false;

        // Check for absolute paths (Windows and Unix)
        if (IsAbsolutePath(normalized))
            return false;

        // Validate protocol prefix
        if (normalized.StartsWith("res://"))
        {
            // res:// is read-only
            if (mode == FileAccessMode.Write)
                return false;
        }
        else if (normalized.StartsWith("user://"))
        {
            // user:// allows both read and write
        }
        else
        {
            // Only res:// and user:// are allowed
            return false;
        }

        // Validate file extension
        if (!HasAllowedExtension(normalized))
            return false;

        // File size check is deferred until actual file access
        // (we can't check size for paths that don't exist yet for write operations)

        return true;
    }

    private (bool IsAllowed, string? RejectionReason) ValidateAndAuditCore(
        string path,
        FileAccessMode mode,
        string caller)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            var reason = "Path is null or empty";
            WriteAuditLog(path ?? string.Empty, reason, caller);
            return (false, reason);
        }

        var normalized = NormalizePath(path);

        // Check for path traversal
        if (ContainsPathTraversal(normalized))
        {
            var reason = "Path contains traversal pattern";
            WriteAuditLog(path, reason, caller);
            return (false, reason);
        }

        // Check for absolute paths
        if (IsAbsolutePath(normalized))
        {
            var reason = "Absolute path rejected";
            WriteAuditLog(path, reason, caller);
            return (false, reason);
        }

        // Validate protocol
        if (normalized.StartsWith("res://"))
        {
            if (mode == FileAccessMode.Write)
            {
                var reason = "Write access denied for res:// protocol";
                WriteAuditLog(path, reason, caller);
                return (false, reason);
            }
        }
        else if (normalized.StartsWith("user://"))
        {
            // user:// allows both read and write
        }
        else
        {
            var reason = "Invalid protocol (must be res:// or user://)";
            WriteAuditLog(path, reason, caller);
            return (false, reason);
        }

        // Validate extension
        if (!HasAllowedExtension(normalized))
        {
            var reason = $"File extension not allowed (allowed: {string.Join(", ", _allowedExtensions ?? DefaultAllowedExtensions)})";
            WriteAuditLog(path, reason, caller);
            return (false, reason);
        }

        // All validations passed
        return (true, null);
    }

    private bool ContainsPathTraversal(string normalizedPath)
    {
        foreach (var pattern in PathTraversalPatterns)
        {
            if (normalizedPath.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private bool IsAbsolutePath(string normalizedPath)
    {
        // Windows absolute paths: C:/, D:/, etc.
        if (Regex.IsMatch(normalizedPath, @"^[a-z]:/"))
            return true;

        // Unix absolute paths
        if (normalizedPath.StartsWith("/etc/") ||
            normalizedPath.StartsWith("/tmp/") ||
            normalizedPath.StartsWith("/var/") ||
            normalizedPath.StartsWith("/sys/") ||
            normalizedPath.StartsWith("/proc/"))
            return true;

        return false;
    }

    private bool HasAllowedExtension(string normalizedPath)
    {
        var extensions = _allowedExtensions ?? DefaultAllowedExtensions;
        var fileExtension = Path.GetExtension(normalizedPath).ToLowerInvariant();

        return extensions.Any(ext => ext.Equals(fileExtension, StringComparison.OrdinalIgnoreCase));
    }

    private void WriteAuditLog(string path, string reason, string caller)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_auditLogPath))
            {
                GD.PushWarning($"[SecurityFileAdapter] Failed to write audit log: No audit log path configured");
                return;
            }

            var logEntry = new
            {
                ts = DateTime.UtcNow.ToString("o"),
                action = "security.file.rejected",
                reason = reason,
                target = path,
                caller = caller
            };

            var logLine = System.Text.Json.JsonSerializer.Serialize(logEntry);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(_auditLogPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Append to JSONL file
            File.AppendAllText(_auditLogPath, logLine + System.Environment.NewLine);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[SecurityFileAdapter] Failed to write audit log: {ex.Message}");
        }
    }
}
