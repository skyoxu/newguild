using Game.Core.Contracts;
using Game.Core.Services;
using Godot;
using System;

namespace Game.Godot.Adapters.Security;

/// <summary>
/// Factory for creating SecurityFileAdapter instances with various configurations.
/// Provides static factory methods for common scenarios and custom setups.
/// Follows ADR-0007 dependency injection and factory pattern.
/// </summary>
public partial class SecurityFileAdapterFactory : RefCounted
{
    /// <summary>
    /// Creates SecurityFileAdapter with default configuration.
    /// - Default extensions: .txt, .json, .cfg, .dat, .sav
    /// - Default max file size: 10MB
    /// - No audit logging
    /// </summary>
    /// <param name="bus">Event bus for security events (required)</param>
    /// <returns>SecurityFileAdapter instance with defaults</returns>
    public static SecurityFileAdapter CreateDefault(InMemoryEventBus bus)
    {
        if (bus == null)
            throw new ArgumentNullException(nameof(bus));

        return new SecurityFileAdapter(bus);
    }

    /// <summary>
    /// Creates SecurityFileAdapter with custom extension whitelist.
    /// Uses default max file size (10MB) and no audit logging.
    /// </summary>
    /// <param name="bus">Event bus for security events (required)</param>
    /// <param name="allowedExtensions">Custom file extension whitelist (e.g., [".txt", ".json"])</param>
    /// <returns>SecurityFileAdapter instance with custom extensions</returns>
    public static SecurityFileAdapter CreateWithExtensions(InMemoryEventBus bus, string[] allowedExtensions)
    {
        if (bus == null)
            throw new ArgumentNullException(nameof(bus));
        if (allowedExtensions == null)
            throw new ArgumentNullException(nameof(allowedExtensions));
        if (allowedExtensions.Length == 0)
            throw new ArgumentException("Extension whitelist cannot be empty", nameof(allowedExtensions));

        return new SecurityFileAdapter(bus, allowedExtensions);
    }

    /// <summary>
    /// Creates SecurityFileAdapter with audit logging enabled.
    /// Uses default extensions and max file size.
    /// </summary>
    /// <param name="bus">Event bus for security events (required)</param>
    /// <param name="auditLogPath">Path to JSONL audit log file (e.g., "user://logs/security-audit.jsonl")</param>
    /// <returns>SecurityFileAdapter instance with audit logging</returns>
    public static SecurityFileAdapter CreateWithAudit(InMemoryEventBus bus, string auditLogPath)
    {
        if (bus == null)
            throw new ArgumentNullException(nameof(bus));
        if (string.IsNullOrWhiteSpace(auditLogPath))
            throw new ArgumentException("Audit log path cannot be null or empty", nameof(auditLogPath));

        return new SecurityFileAdapter(bus, auditLogPath);
    }

    /// <summary>
    /// Creates SecurityFileAdapter with full custom configuration.
    /// </summary>
    /// <param name="bus">Event bus for security events (required)</param>
    /// <param name="allowedExtensions">Custom file extension whitelist</param>
    /// <param name="maxFileSize">Maximum file size in bytes</param>
    /// <param name="auditLogPath">Path to JSONL audit log file (optional)</param>
    /// <returns>SecurityFileAdapter instance with full customization</returns>
    public static SecurityFileAdapter CreateCustom(
        InMemoryEventBus bus,
        string[] allowedExtensions,
        long maxFileSize,
        string? auditLogPath = null)
    {
        if (bus == null)
            throw new ArgumentNullException(nameof(bus));
        if (allowedExtensions == null)
            throw new ArgumentNullException(nameof(allowedExtensions));
        if (allowedExtensions.Length == 0)
            throw new ArgumentException("Extension whitelist cannot be empty", nameof(allowedExtensions));
        if (maxFileSize <= 0)
            throw new ArgumentException("Max file size must be positive", nameof(maxFileSize));

        return new SecurityFileAdapter(bus, allowedExtensions, maxFileSize, auditLogPath);
    }

    /// <summary>
    /// Creates SecurityFileAdapter for testing scenarios.
    /// - Restricted extensions: .txt only
    /// - Small file size limit: 1MB
    /// - Audit logging enabled to specified path
    /// </summary>
    /// <param name="bus">Event bus for security events (required)</param>
    /// <param name="auditLogPath">Path to test audit log file (required for testing)</param>
    /// <returns>SecurityFileAdapter instance configured for testing</returns>
    public static SecurityFileAdapter CreateForTesting(InMemoryEventBus bus, string auditLogPath)
    {
        if (bus == null)
            throw new ArgumentNullException(nameof(bus));
        if (string.IsNullOrWhiteSpace(auditLogPath))
            throw new ArgumentException("Test audit log path required", nameof(auditLogPath));

        var testExtensions = new[] { ".txt" };
        const long testMaxSize = 1024 * 1024; // 1MB

        return new SecurityFileAdapter(bus, testExtensions, testMaxSize, auditLogPath);
    }

    /// <summary>
    /// Instance method for GDScript access - creates default configuration.
    /// GDScript cannot call static methods directly, so this instance wrapper is provided.
    /// </summary>
    /// <param name="bus">Event bus for security events</param>
    /// <returns>SecurityFileAdapter instance with defaults</returns>
    public SecurityFileAdapter CreateDefaultInstance(InMemoryEventBus bus)
    {
        return CreateDefault(bus);
    }

    /// <summary>
    /// Instance method for GDScript access - creates with custom extensions.
    /// </summary>
    /// <param name="bus">Event bus for security events</param>
    /// <param name="allowedExtensions">Custom file extension whitelist</param>
    /// <returns>SecurityFileAdapter instance with custom extensions</returns>
    public SecurityFileAdapter CreateWithExtensionsInstance(InMemoryEventBus bus, string[] allowedExtensions)
    {
        return CreateWithExtensions(bus, allowedExtensions);
    }

    /// <summary>
    /// Instance method for GDScript access - creates with audit logging.
    /// </summary>
    /// <param name="bus">Event bus for security events</param>
    /// <param name="auditLogPath">Path to JSONL audit log file</param>
    /// <returns>SecurityFileAdapter instance with audit logging</returns>
    public SecurityFileAdapter CreateWithAuditInstance(InMemoryEventBus bus, string auditLogPath)
    {
        return CreateWithAudit(bus, auditLogPath);
    }

    /// <summary>
    /// Instance method for GDScript access - creates with full custom configuration.
    /// </summary>
    /// <param name="bus">Event bus for security events</param>
    /// <param name="allowedExtensions">Custom file extension whitelist</param>
    /// <param name="maxFileSize">Maximum file size in bytes</param>
    /// <param name="auditLogPath">Path to JSONL audit log file (optional)</param>
    /// <returns>SecurityFileAdapter instance with full customization</returns>
    public SecurityFileAdapter CreateCustomInstance(
        InMemoryEventBus bus,
        string[] allowedExtensions,
        long maxFileSize,
        string? auditLogPath = null)
    {
        return CreateCustom(bus, allowedExtensions, maxFileSize, auditLogPath);
    }

    /// <summary>
    /// Instance method for GDScript access - creates for testing scenarios.
    /// </summary>
    /// <param name="bus">Event bus for security events</param>
    /// <param name="auditLogPath">Path to test audit log file</param>
    /// <returns>SecurityFileAdapter instance configured for testing</returns>
    public SecurityFileAdapter CreateForTestingInstance(InMemoryEventBus bus, string auditLogPath)
    {
        return CreateForTesting(bus, auditLogPath);
    }

    // Overloads accepting EventBusAdapter for GDScript compatibility

    /// <summary>
    /// Creates default SecurityFileAdapter from EventBusAdapter wrapper.
    /// </summary>
    /// <param name="busAdapter">EventBusAdapter wrapper (GDScript-compatible)</param>
    /// <returns>SecurityFileAdapter instance with defaults</returns>
    public SecurityFileAdapter CreateDefaultInstance(EventBusAdapter busAdapter)
    {
        if (busAdapter == null)
            throw new ArgumentNullException(nameof(busAdapter));
        return CreateDefault(busAdapter.GetBus());
    }

    /// <summary>
    /// Creates SecurityFileAdapter with custom extensions from EventBusAdapter.
    /// </summary>
    /// <param name="busAdapter">EventBusAdapter wrapper</param>
    /// <param name="allowedExtensions">Custom file extension whitelist</param>
    /// <returns>SecurityFileAdapter instance with custom extensions</returns>
    public SecurityFileAdapter CreateWithExtensionsInstance(EventBusAdapter busAdapter, string[] allowedExtensions)
    {
        if (busAdapter == null)
            throw new ArgumentNullException(nameof(busAdapter));
        return CreateWithExtensions(busAdapter.GetBus(), allowedExtensions);
    }

    /// <summary>
    /// Creates SecurityFileAdapter with audit logging from EventBusAdapter.
    /// </summary>
    /// <param name="busAdapter">EventBusAdapter wrapper</param>
    /// <param name="auditLogPath">Path to audit log file</param>
    /// <returns>SecurityFileAdapter instance with audit logging</returns>
    public SecurityFileAdapter CreateWithAuditInstance(EventBusAdapter busAdapter, string auditLogPath)
    {
        if (busAdapter == null)
            throw new ArgumentNullException(nameof(busAdapter));
        return CreateWithAudit(busAdapter.GetBus(), auditLogPath);
    }

    /// <summary>
    /// Creates fully custom SecurityFileAdapter from EventBusAdapter.
    /// </summary>
    /// <param name="busAdapter">EventBusAdapter wrapper</param>
    /// <param name="allowedExtensions">Custom file extension whitelist</param>
    /// <param name="maxFileSize">Maximum file size in bytes</param>
    /// <param name="auditLogPath">Path to audit log file (optional)</param>
    /// <returns>SecurityFileAdapter instance with full customization</returns>
    public SecurityFileAdapter CreateCustomInstance(
        EventBusAdapter busAdapter,
        string[] allowedExtensions,
        long maxFileSize,
        string? auditLogPath = null)
    {
        if (busAdapter == null)
            throw new ArgumentNullException(nameof(busAdapter));
        return CreateCustom(busAdapter.GetBus(), allowedExtensions, maxFileSize, auditLogPath);
    }

    /// <summary>
    /// Creates test-configured SecurityFileAdapter from EventBusAdapter.
    /// </summary>
    /// <param name="busAdapter">EventBusAdapter wrapper</param>
    /// <param name="auditLogPath">Path to test audit log file</param>
    /// <returns>SecurityFileAdapter instance configured for testing</returns>
    public SecurityFileAdapter CreateForTestingInstance(EventBusAdapter busAdapter, string auditLogPath)
    {
        if (busAdapter == null)
            throw new ArgumentNullException(nameof(busAdapter));
        return CreateForTesting(busAdapter.GetBus(), auditLogPath);
    }
}
