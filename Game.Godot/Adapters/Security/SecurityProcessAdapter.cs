using Game.Core.Interfaces;
using Game.Core.Services;
using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Game.Godot.Adapters.Security;

/// <summary>
/// Adapter layer implementation of process execution security validation.
/// Enforces command whitelist and argument sanitization to prevent CWE-532.
/// Supports three execution modes: Secure (blocks all), Dev (whitelist), Test (audit only).
/// </summary>
public partial class SecurityProcessAdapter : RefCounted, ISecurityProcessValidator
{
    private readonly IEventBus _bus;
    private readonly string[]? _allowedCommands;
    private readonly string? _auditLogPath;
    private readonly bool _isSecureMode;
    private readonly bool _isTestMode;

    private static readonly string[] DefaultAllowedCommands = new[]
    {
        "git",
        "dotnet",
        "py",
        "python",
        "python3"
    };

    private static readonly string[] SensitiveParameterPatterns = new[]
    {
        "--password",
        "--token",
        "--api-key",
        "--secret",
        "-p",
        "/p"
    };

    private const int DefaultTimeoutSeconds = 30;
    private const long MaxOutputBytes = 1 * 1024 * 1024; // 1MB

    public SecurityProcessAdapter(InMemoryEventBus bus, string? auditLogPath = null)
        : this(bus, DefaultAllowedCommands, auditLogPath)
    {
    }

    public SecurityProcessAdapter(
        InMemoryEventBus bus,
        string[] allowedCommands,
        string? auditLogPath = null)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _allowedCommands = allowedCommands;

        // Convert Godot virtual path (user://) to absolute filesystem path for .NET File API
        if (!string.IsNullOrWhiteSpace(auditLogPath))
        {
            _auditLogPath = ProjectSettings.GlobalizePath(auditLogPath);
        }
        else
        {
            _auditLogPath = auditLogPath;
        }

        // Read environment variables for execution mode
        _isSecureMode = System.Environment.GetEnvironmentVariable("GD_SECURE_MODE") == "1";
        _isTestMode = System.Environment.GetEnvironmentVariable("SECURITY_TEST_MODE") == "1";
    }

    /// <summary>
    /// Explicit interface implementation - satisfies Core layer contract.
    /// </summary>
    bool ISecurityProcessValidator.IsExecutionAllowed(string command, string[] arguments)
    {
        return IsExecutionAllowedCore(command, arguments);
    }

    /// <summary>
    /// Public method for GDScript access.
    /// </summary>
    public bool IsExecutionAllowed(string command, global::Godot.Collections.Array arguments)
    {
        var argsArray = ConvertGodotArrayToStringArray(arguments);
        return IsExecutionAllowedCore(command, argsArray);
    }

    /// <summary>
    /// Explicit interface implementation - satisfies Core layer contract.
    /// </summary>
    (bool IsAllowed, string? RejectionReason) ISecurityProcessValidator.ValidateAndAudit(
        string command,
        string[] arguments,
        string caller)
    {
        return ValidateAndAuditCore(command, arguments, caller);
    }

    /// <summary>
    /// Public method for GDScript access.
    /// Returns ProcessValidationResult for cross-language compatibility.
    /// </summary>
    public ProcessValidationResult ValidateAndAudit(string command, global::Godot.Collections.Array arguments, string caller)
    {
        var argsArray = ConvertGodotArrayToStringArray(arguments);
        var (isAllowed, rejectionReason) = ValidateAndAuditCore(command, argsArray, caller);
        return new ProcessValidationResult(isAllowed, rejectionReason);
    }

    /// <summary>
    /// Explicit interface implementation - satisfies Core layer contract.
    /// </summary>
    string ISecurityProcessValidator.SanitizeArguments(string[] arguments)
    {
        return SanitizeArgumentsCore(arguments);
    }

    /// <summary>
    /// Public method for GDScript access.
    /// </summary>
    public string SanitizeArguments(global::Godot.Collections.Array arguments)
    {
        var argsArray = ConvertGodotArrayToStringArray(arguments);
        return SanitizeArgumentsCore(argsArray);
    }

    private bool IsExecutionAllowedCore(string command, string[] arguments)
    {
        // Secure mode: reject all process execution
        if (_isSecureMode)
            return false;

        // Test mode: allow for auditing purposes
        if (_isTestMode)
            return true;

        // Development mode: check whitelist
        if (string.IsNullOrWhiteSpace(command))
            return false;

        // Extract command name from path
        var commandName = Path.GetFileNameWithoutExtension(command).ToLowerInvariant();

        // Check if command is in whitelist
        if (_allowedCommands == null || _allowedCommands.Length == 0)
            return false;

        return _allowedCommands.Any(allowed =>
            allowed.Equals(commandName, StringComparison.OrdinalIgnoreCase));
    }

    private (bool IsAllowed, string? RejectionReason) ValidateAndAuditCore(
        string command,
        string[] arguments,
        string caller)
    {
        // Secure mode check
        if (_isSecureMode)
        {
            var reason = "All process execution blocked in secure mode (GD_SECURE_MODE=1)";
            WriteAuditLog(command, arguments, reason, caller);
            return (false, reason);
        }

        // Test mode: audit but allow
        if (_isTestMode)
        {
            WriteAuditLog(command, arguments, "Test mode: execution audited but allowed", caller);
            return (true, null);
        }

        // Validate command is not null/empty
        if (string.IsNullOrWhiteSpace(command))
        {
            var reason = "Command is null or empty";
            WriteAuditLog(command ?? string.Empty, arguments, reason, caller);
            return (false, reason);
        }

        // Check for absolute path commands FIRST (security risk - check before whitelist)
        if (Path.IsPathRooted(command) && !IsSystemPathCommand(command))
        {
            var reason = "Absolute path commands not allowed outside system PATH";
            WriteAuditLog(command, arguments, reason, caller);
            return (false, reason);
        }

        // Validate command is in whitelist
        var commandName = Path.GetFileNameWithoutExtension(command).ToLowerInvariant();

        if (_allowedCommands == null || _allowedCommands.Length == 0)
        {
            var reason = "No commands allowed (empty whitelist)";
            WriteAuditLog(command, arguments, reason, caller);
            return (false, reason);
        }

        var isWhitelisted = _allowedCommands.Any(allowed =>
            allowed.Equals(commandName, StringComparison.OrdinalIgnoreCase));

        if (!isWhitelisted)
        {
            var reason = $"Command not in whitelist (allowed: {string.Join(", ", _allowedCommands)})";
            WriteAuditLog(command, arguments, reason, caller);
            return (false, reason);
        }

        // All validations passed
        return (true, null);
    }

    private string SanitizeArgumentsCore(string[] arguments)
    {
        if (arguments == null || arguments.Length == 0)
            return string.Empty;

        var sanitizedArgs = new System.Collections.Generic.List<string>();

        for (int i = 0; i < arguments.Length; i++)
        {
            var arg = arguments[i];

            // Check if current argument is a sensitive parameter
            var isSensitiveParam = SensitiveParameterPatterns.Any(pattern =>
                arg.StartsWith(pattern, StringComparison.OrdinalIgnoreCase));

            if (isSensitiveParam)
            {
                // Check if value is in same argument (--password=secret) or next argument
                if (arg.Contains('='))
                {
                    // Format: --password=value
                    var parts = arg.Split('=', 2);
                    sanitizedArgs.Add($"{parts[0]}=***");
                }
                else
                {
                    // Format: --password value (value in next argument)
                    sanitizedArgs.Add(arg);
                    if (i + 1 < arguments.Length)
                    {
                        sanitizedArgs.Add("***");
                        i++; // Skip next argument as we've already processed it
                    }
                }
            }
            else
            {
                sanitizedArgs.Add(arg);
            }
        }

        return string.Join(" ", sanitizedArgs);
    }

    private bool IsSystemPathCommand(string command)
    {
        var pathEnv = System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var paths = pathEnv.Split(Path.PathSeparator);

        // For absolute paths, check if the command's directory is in system PATH
        if (Path.IsPathRooted(command))
        {
            var commandDir = Path.GetDirectoryName(command);
            if (string.IsNullOrEmpty(commandDir))
                return false;

            // Normalize the command directory for comparison
            commandDir = Path.GetFullPath(commandDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            foreach (var path in paths)
            {
                try
                {
                    var normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (string.Equals(commandDir, normalizedPath, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch
                {
                    // Ignore invalid paths
                }
            }

            return false;
        }

        // For relative paths, check if executable exists in any PATH directory
        foreach (var path in paths)
        {
            try
            {
                var fullPath = Path.Combine(path, command);
                if (File.Exists(fullPath) || File.Exists(fullPath + ".exe"))
                    return true;
            }
            catch
            {
                // Ignore invalid paths
            }
        }

        return false;
    }

    private void WriteAuditLog(string command, string[] arguments, string reason, string caller)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_auditLogPath))
            {
                GD.PushWarning($"[SecurityProcessAdapter] Failed to write audit log: No audit log path configured");
                return;
            }

            // Sanitize arguments before logging (prevent CWE-532)
            var sanitizedArgs = SanitizeArgumentsCore(arguments);

            var logEntry = new
            {
                ts = DateTime.UtcNow.ToString("o"),
                action = "security.process.rejected",
                reason = reason,
                target = $"{command} {sanitizedArgs}",
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
            GD.PushWarning($"[SecurityProcessAdapter] Failed to write audit log: {ex.Message}");
        }
    }

    private string[] ConvertGodotArrayToStringArray(global::Godot.Collections.Array godotArray)
    {
        if (godotArray == null || godotArray.Count == 0)
            return Array.Empty<string>();

        var result = new string[godotArray.Count];
        for (int i = 0; i < godotArray.Count; i++)
        {
            var item = godotArray[i];
            result[i] = item.Obj != null ? item.ToString() : string.Empty;
        }
        return result;
    }
}
