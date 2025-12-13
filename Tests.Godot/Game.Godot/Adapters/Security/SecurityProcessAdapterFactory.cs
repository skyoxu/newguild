using Game.Core.Services;
using Godot;
using System;

namespace Game.Godot.Adapters.Security;

/// <summary>
/// Factory for creating SecurityProcessAdapter instances with different configurations.
/// Provides factory methods for default setup, custom whitelists, and testing scenarios.
/// Follows ADR-0007 dependency injection and factory pattern.
/// </summary>
public partial class SecurityProcessAdapterFactory : RefCounted
{
    // Static methods for internal C# use

    /// <summary>
    /// Creates a SecurityProcessAdapter with default allowed commands (git, dotnet, py, python, python3).
    /// </summary>
    /// <param name="bus">Event bus instance for audit events</param>
    /// <param name="auditLogPath">Optional path to audit log file (JSONL format)</param>
    /// <returns>SecurityProcessAdapter instance</returns>
    public static SecurityProcessAdapter CreateWithDefaults(InMemoryEventBus bus, string? auditLogPath = null)
    {
        if (bus == null)
            throw new ArgumentNullException(nameof(bus));
        return new SecurityProcessAdapter(bus, auditLogPath);
    }

    /// <summary>
    /// Creates a SecurityProcessAdapter with a custom command whitelist.
    /// </summary>
    /// <param name="bus">Event bus instance for audit events</param>
    /// <param name="allowedCommands">Array of allowed command names (e.g., ["git", "dotnet"])</param>
    /// <param name="auditLogPath">Optional path to audit log file (JSONL format)</param>
    /// <returns>SecurityProcessAdapter instance</returns>
    public static SecurityProcessAdapter CreateWithWhitelist(
        InMemoryEventBus bus,
        string[] allowedCommands,
        string? auditLogPath = null)
    {
        if (bus == null)
            throw new ArgumentNullException(nameof(bus));
        if (allowedCommands == null)
            throw new ArgumentNullException(nameof(allowedCommands));
        return new SecurityProcessAdapter(bus, allowedCommands, auditLogPath);
    }

    /// <summary>
    /// Creates a SecurityProcessAdapter for testing purposes.
    /// Assumes GD_SECURE_MODE=0 and SECURITY_TEST_MODE=1 for test scenarios.
    /// </summary>
    /// <param name="bus">Event bus instance for audit events</param>
    /// <param name="auditLogPath">Optional path to audit log file (JSONL format)</param>
    /// <returns>SecurityProcessAdapter instance</returns>
    public static SecurityProcessAdapter CreateForTesting(InMemoryEventBus bus, string? auditLogPath = null)
    {
        if (bus == null)
            throw new ArgumentNullException(nameof(bus));
        return new SecurityProcessAdapter(bus, auditLogPath);
    }

    // Instance methods for GDScript access
    // GDScript cannot call static methods directly, so these instance wrappers are provided.

    /// <summary>
    /// Instance method for GDScript access - creates with default configuration.
    /// </summary>
    /// <param name="bus">Event bus instance for audit events</param>
    /// <param name="auditLogPath">Optional path to audit log file (JSONL format)</param>
    /// <returns>SecurityProcessAdapter instance</returns>
    public SecurityProcessAdapter CreateWithDefaultsInstance(InMemoryEventBus bus, string? auditLogPath = null)
    {
        return CreateWithDefaults(bus, auditLogPath);
    }

    /// <summary>
    /// Instance method for GDScript access - creates with custom whitelist.
    /// </summary>
    /// <param name="bus">Event bus instance for audit events</param>
    /// <param name="allowedCommands">Array of allowed command names</param>
    /// <param name="auditLogPath">Optional path to audit log file (JSONL format)</param>
    /// <returns>SecurityProcessAdapter instance</returns>
    public SecurityProcessAdapter CreateWithWhitelistInstance(
        InMemoryEventBus bus,
        string[] allowedCommands,
        string? auditLogPath = null)
    {
        return CreateWithWhitelist(bus, allowedCommands, auditLogPath);
    }

    /// <summary>
    /// Instance method for GDScript access - creates for testing.
    /// </summary>
    /// <param name="bus">Event bus instance for audit events</param>
    /// <param name="auditLogPath">Optional path to audit log file (JSONL format)</param>
    /// <returns>SecurityProcessAdapter instance</returns>
    public SecurityProcessAdapter CreateForTestingInstance(InMemoryEventBus bus, string? auditLogPath = null)
    {
        return CreateForTesting(bus, auditLogPath);
    }

    /// <summary>
    /// Instance method for GDScript access - creates from Godot array.
    /// Converts Godot.Collections.Array to string[] internally.
    /// </summary>
    /// <param name="bus">Event bus instance for audit events</param>
    /// <param name="godotArray">Godot array of allowed command names</param>
    /// <param name="auditLogPath">Optional path to audit log file (JSONL format)</param>
    /// <returns>SecurityProcessAdapter instance</returns>
    public SecurityProcessAdapter CreateFromGodotArrayInstance(
        InMemoryEventBus bus,
        global::Godot.Collections.Array godotArray,
        string? auditLogPath = null)
    {
        if (bus == null)
            throw new ArgumentNullException(nameof(bus));
        if (godotArray == null || godotArray.Count == 0)
        {
            return new SecurityProcessAdapter(bus, Array.Empty<string>(), auditLogPath);
        }

        var allowedCommands = new string[godotArray.Count];
        for (int i = 0; i < godotArray.Count; i++)
        {
            var item = godotArray[i];
            allowedCommands[i] = item.Obj != null ? item.ToString() : string.Empty;
        }

        return new SecurityProcessAdapter(bus, allowedCommands, auditLogPath);
    }

    // EventBusAdapter overloads for GDScript compatibility

    /// <summary>
    /// Creates default SecurityProcessAdapter from EventBusAdapter wrapper.
    /// </summary>
    /// <param name="busAdapter">EventBusAdapter wrapper (GDScript-compatible)</param>
    /// <param name="auditLogPath">Optional path to audit log file</param>
    /// <returns>SecurityProcessAdapter instance</returns>
    public SecurityProcessAdapter CreateWithDefaultsInstance(EventBusAdapter busAdapter, string? auditLogPath = null)
    {
        if (busAdapter == null)
            throw new ArgumentNullException(nameof(busAdapter));
        return CreateWithDefaults(busAdapter.GetBus(), auditLogPath);
    }

    /// <summary>
    /// Creates SecurityProcessAdapter with custom whitelist from EventBusAdapter.
    /// </summary>
    /// <param name="busAdapter">EventBusAdapter wrapper</param>
    /// <param name="allowedCommands">Array of allowed command names</param>
    /// <param name="auditLogPath">Optional path to audit log file</param>
    /// <returns>SecurityProcessAdapter instance</returns>
    public SecurityProcessAdapter CreateWithWhitelistInstance(
        EventBusAdapter busAdapter,
        string[] allowedCommands,
        string? auditLogPath = null)
    {
        if (busAdapter == null)
            throw new ArgumentNullException(nameof(busAdapter));
        return CreateWithWhitelist(busAdapter.GetBus(), allowedCommands, auditLogPath);
    }

    /// <summary>
    /// Creates test-configured SecurityProcessAdapter from EventBusAdapter.
    /// </summary>
    /// <param name="busAdapter">EventBusAdapter wrapper</param>
    /// <param name="auditLogPath">Optional path to audit log file</param>
    /// <returns>SecurityProcessAdapter instance</returns>
    public SecurityProcessAdapter CreateForTestingInstance(EventBusAdapter busAdapter, string? auditLogPath = null)
    {
        if (busAdapter == null)
            throw new ArgumentNullException(nameof(busAdapter));
        return CreateForTesting(busAdapter.GetBus(), auditLogPath);
    }

    /// <summary>
    /// Creates SecurityProcessAdapter from Godot array and EventBusAdapter.
    /// </summary>
    /// <param name="busAdapter">EventBusAdapter wrapper</param>
    /// <param name="godotArray">Godot array of allowed command names</param>
    /// <param name="auditLogPath">Optional path to audit log file</param>
    /// <returns>SecurityProcessAdapter instance</returns>
    public SecurityProcessAdapter CreateFromGodotArrayInstance(
        EventBusAdapter busAdapter,
        global::Godot.Collections.Array godotArray,
        string? auditLogPath = null)
    {
        if (busAdapter == null)
            throw new ArgumentNullException(nameof(busAdapter));
        return CreateFromGodotArrayInstance(busAdapter.GetBus(), godotArray, auditLogPath);
    }
}
